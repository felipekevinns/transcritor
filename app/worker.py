"""
Worker de transcricao.

Roda em um processo separado da interface. Recebe o caminho de um arquivo
JSON com o trabalho, processa os arquivos em sequencia e reporta o progresso
em linhas JSON no stdout, prefixadas com "@@" (o restante do stdout/stderr e
log livre das bibliotecas).
"""
import gc
import json
import os
import sys
import time
import traceback
import warnings

warnings.filterwarnings("ignore")
os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")
os.environ.setdefault("TOKENIZERS_PARALLELISM", "false")

try:
    sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
    sys.stderr.reconfigure(encoding="utf-8", line_buffering=True)
except Exception:
    pass


def emit(kind, **data):
    data["type"] = kind
    sys.stdout.write("@@" + json.dumps(data, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def is_oom(exc):
    msg = str(exc).lower()
    return "out of memory" in msg or "cuda_error_out_of_memory" in msg or exc.__class__.__name__ == "OutOfMemoryError"


def fmt_ts(seconds, with_ms=False):
    seconds = max(0.0, float(seconds or 0))
    h = int(seconds // 3600)
    m = int((seconds % 3600) // 60)
    s = int(seconds % 60)
    if with_ms:
        return f"{h:02d}:{m:02d}:{s:02d}.{int((seconds % 1) * 1000):03d}"
    return f"{h:02d}:{m:02d}:{s:02d}"


def relabel_speakers(result):
    """SPEAKER_00 -> Falante 1, na ordem em que aparecem."""
    mapping = {}

    def name(raw):
        if raw is None:
            return None
        if raw not in mapping:
            mapping[raw] = f"Falante {len(mapping) + 1}"
        return mapping[raw]

    for seg in result.get("segments", []):
        if "speaker" in seg:
            seg["speaker"] = name(seg["speaker"])
        for w in seg.get("words", []) or []:
            if "speaker" in w:
                w["speaker"] = name(w["speaker"])
    return len(mapping)


def build_blocks(result):
    """Agrupa segmentos consecutivos do mesmo falante em blocos de leitura."""
    blocks = []
    for seg in result.get("segments", []):
        text = (seg.get("text") or "").strip()
        if not text:
            continue
        spk = seg.get("speaker")
        if blocks and spk is not None and blocks[-1]["speaker"] == spk:
            blocks[-1]["text"] += " " + text
            blocks[-1]["end"] = seg.get("end", blocks[-1]["end"])
        else:
            blocks.append({"speaker": spk, "start": seg.get("start", 0), "end": seg.get("end", 0), "text": text})
    return blocks


def write_readable_txt(result, path, title):
    blocks = build_blocks(result)
    has_spk = any(b["speaker"] for b in blocks)
    lines = [title, ""]
    for b in blocks:
        if has_spk:
            lines.append(f"[{fmt_ts(b['start'])}] {b['speaker'] or 'Falante'}")
            lines.append(b["text"])
            lines.append("")
        else:
            lines.append(f"[{fmt_ts(b['start'])}] {b['text']}")
    with open(path, "w", encoding="utf-8-sig") as f:
        f.write("\n".join(lines).rstrip() + "\n")


def write_docx(result, path, title, meta):
    from docx import Document
    from docx.shared import Pt, RGBColor
    from docx.enum.text import WD_ALIGN_PARAGRAPH

    doc = Document()
    style = doc.styles["Normal"]
    style.font.name = "Calibri"
    style.font.size = Pt(11)

    h = doc.add_paragraph()
    run = h.add_run(title)
    run.font.size = Pt(18)
    run.font.name = "Cambria"
    run.bold = False

    m = doc.add_paragraph()
    mr = m.add_run(meta)
    mr.font.size = Pt(9)
    mr.font.color.rgb = RGBColor(0x6F, 0x6B, 0x63)

    blocks = build_blocks(result)
    has_spk = any(b["speaker"] for b in blocks)
    for b in blocks:
        p = doc.add_paragraph()
        p.paragraph_format.space_after = Pt(8)
        ts = p.add_run(fmt_ts(b["start"]) + "   ")
        ts.font.size = Pt(9)
        ts.font.color.rgb = RGBColor(0x8A, 0x85, 0x7B)
        if has_spk:
            sp = p.add_run((b["speaker"] or "Falante") + "\n")
            sp.bold = True
        p.add_run(b["text"])
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
    doc.save(path)


def main():
    job_path = sys.argv[1]
    with open(job_path, "r", encoding="utf-8") as f:
        job = json.load(f)

    emit("status", message="Carregando bibliotecas")
    import torch
    import whisperx
    from whisperx.utils import get_writer

    device = job.get("device", "cuda")
    if device == "cuda" and not torch.cuda.is_available():
        emit("warning", message="Placa de vídeo indisponível. Usando o processador.")
        device = "cpu"

    if device == "cuda":
        compute_type = job.get("compute_type") or "float16"
        threads = 4
    else:
        compute_type = "int8"
        threads = int(job.get("threads") or os.cpu_count() or 4)
        torch.set_num_threads(threads)

    batch_size = int(job.get("batch_size") or (8 if device == "cuda" else 4))
    language = job.get("language") or None
    model_name = job.get("model", "large-v3")
    diarize = bool(job.get("diarize"))
    hf_token = job.get("hf_token") or None
    vocab = (job.get("vocabulary") or "").strip() or None
    formats = job.get("formats") or ["txt", "srt"]

    asr_options = {
        "initial_prompt": vocab,
        "beam_size": 5,
        "condition_on_previous_text": False,
    }

    emit("status", message=f"Carregando modelo {model_name}")
    t_load = time.time()
    model = whisperx.load_model(
        model_name,
        device=device,
        compute_type=compute_type,
        language=language,
        asr_options=asr_options,
        vad_method=job.get("vad_method", "pyannote"),
        threads=threads,
    )
    emit("log", message=f"Modelo carregado em {time.time() - t_load:.1f}s ({device}, {compute_type}, lote {batch_size})")

    writer_args = {"highlight_words": False, "max_line_count": None, "max_line_width": None}
    weights = (0.70, 0.15, 0.15) if diarize else (0.80, 0.20, 0.0)

    for idx, item in enumerate(job["files"]):
        path = item["path"]
        out_dir = item["output_dir"]
        base = os.path.splitext(os.path.basename(path))[0]
        t0 = time.time()

        def progress(stage, pct, offset):
            w = {"transcricao": weights[0], "alinhamento": weights[1], "falantes": weights[2]}[stage]
            emit("progress", index=idx, stage=stage, stage_pct=round(pct, 1),
                 file_pct=round((offset + w * pct / 100) * 100, 1), elapsed=round(time.time() - t0, 1))

        try:
            os.makedirs(out_dir, exist_ok=True)
            emit("file_start", index=idx)
            emit("progress", index=idx, stage="audio", stage_pct=0, file_pct=0, elapsed=0)
            audio = whisperx.load_audio(path)
            duration = len(audio) / 16000.0

            # Transcricao, com reducao automatica do lote se faltar memoria
            bs = batch_size
            while True:
                try:
                    result = model.transcribe(
                        audio, batch_size=bs, language=language,
                        progress_callback=lambda p: progress("transcricao", p, 0.0),
                    )
                    break
                except Exception as e:
                    if device == "cuda" and is_oom(e) and bs > 1:
                        bs = max(1, bs // 2)
                        gc.collect(); torch.cuda.empty_cache()
                        emit("warning", index=idx, message=f"Memória da placa cheia. Tentando com lote {bs}.")
                        continue
                    raise

            lang = result.get("language") or language or "pt"

            # Alinhamento palavra por palavra
            if result.get("segments"):
                try:
                    progress("alinhamento", 0, weights[0])
                    align_model, meta = whisperx.load_align_model(language_code=lang, device=device)
                    result = whisperx.align(
                        result["segments"], align_model, meta, audio, device,
                        return_char_alignments=False,
                        progress_callback=lambda p: progress("alinhamento", p, weights[0]),
                    )
                    del align_model
                except ValueError as e:
                    emit("warning", index=idx, message=f"Sem alinhamento para o idioma '{lang}'. Horários aproximados.")
                gc.collect()
                if device == "cuda":
                    torch.cuda.empty_cache()

            # Identificacao de falantes
            n_speakers = 0
            if diarize:
                progress("falantes", 0, weights[0] + weights[1])
                from whisperx.diarize import DiarizationPipeline, assign_word_speakers
                dmodel = DiarizationPipeline(token=hf_token, device=device)
                kw = {}
                if job.get("num_speakers"):
                    kw["num_speakers"] = int(job["num_speakers"])
                else:
                    if job.get("min_speakers"):
                        kw["min_speakers"] = int(job["min_speakers"])
                    if job.get("max_speakers"):
                        kw["max_speakers"] = int(job["max_speakers"])
                segs = dmodel(audio, progress_callback=lambda p: progress("falantes", p, weights[0] + weights[1]), **kw)
                result = assign_word_speakers(segs, result)
                del dmodel
                gc.collect()
                if device == "cuda":
                    torch.cuda.empty_cache()
                n_speakers = relabel_speakers(result)

            result["language"] = lang

            # Escrita dos arquivos
            outputs = []
            for fmt in formats:
                if fmt in ("txt", "srt", "vtt", "tsv", "json"):
                    get_writer(fmt, out_dir)(result, path, writer_args)
                    outputs.append(os.path.join(out_dir, f"{base}.{fmt}"))
                elif fmt == "readable":
                    p = os.path.join(out_dir, f"{base} - com horários.txt")
                    write_readable_txt(result, p, base)
                    outputs.append(p)
                elif fmt == "docx":
                    p = os.path.join(out_dir, f"{base}.docx")
                    meta = f"Duração {fmt_ts(duration)} · Idioma {lang} · Modelo {model_name}"
                    if n_speakers:
                        meta += f" · {n_speakers} falantes"
                    write_docx(result, p, base, meta)
                    outputs.append(p)

            elapsed = time.time() - t0
            emit("file_done", index=idx, outputs=outputs, out_dir=out_dir, elapsed=round(elapsed, 1),
                 duration=round(duration, 1), speed=round(duration / elapsed, 1) if elapsed > 0 else None,
                 speakers=n_speakers, language=lang)
        except Exception as e:
            emit("file_error", index=idx, message=str(e) or e.__class__.__name__,
                 detail=traceback.format_exc()[-2000:])
            if device == "cuda":
                gc.collect(); torch.cuda.empty_cache()

    emit("finished")


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        emit("fatal", message=str(e) or e.__class__.__name__, detail=traceback.format_exc()[-3000:])
        sys.exit(1)
