"""
Transcritor - interface de mesa para o WhisperX.
"""
import glob
import json
import os
import shutil
import subprocess
import sys
import tempfile
import threading
import time

import webview
from webview.dom import DOMEventHandler

APP_DIR = os.path.dirname(os.path.abspath(__file__))
BASE_DIR = os.path.dirname(APP_DIR)
UI_DIR = os.path.join(APP_DIR, "ui")
CONFIG_PATH = os.path.join(APP_DIR, "config.json")
TOKEN_PATH = os.path.join(BASE_DIR, "hf_token.txt")
LOG_PATH = os.path.join(APP_DIR, "ultimo_registro.txt")
WORKER = os.path.join(APP_DIR, "worker.py")

# Layout portatil/instalado: runtime\python (Python embutido) e runtime\ffmpeg.
# Layout de desenvolvimento: venv\ criado pelo instalar.ps1.
RUNTIME_DIR = os.path.join(BASE_DIR, "runtime")
RUNTIME_PY = os.path.join(RUNTIME_DIR, "python", "python.exe")
if os.path.exists(RUNTIME_PY):
    VENV_PY = RUNTIME_PY
    # Modelos ficam dentro da pasta do programa (portatil de verdade)
    os.environ.setdefault("HF_HOME", os.path.join(BASE_DIR, "models", "huggingface"))
    os.environ.setdefault("TORCH_HOME", os.path.join(BASE_DIR, "models", "torch"))
    os.environ.setdefault("NLTK_DATA", os.path.join(BASE_DIR, "models", "nltk_data"))
    try:
        os.makedirs(os.environ["NLTK_DATA"], exist_ok=True)
    except OSError:
        pass
else:
    VENV_PY = os.path.join(BASE_DIR, "venv", "Scripts", "python.exe")

MEDIA_EXT = {
    ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".flv", ".m4v", ".mpg", ".mpeg", ".ts", ".3gp",
    ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".opus", ".wma", ".aiff", ".aif",
}

DEFAULT_CONFIG = {
    "quality": "precise",          # precise | fast
    "device": "cuda",              # cuda | cpu
    "language": "pt",
    "diarize": False,
    "speakers_mode": "auto",       # auto | exact | range
    "num_speakers": 2,
    "min_speakers": 2,
    "max_speakers": 4,
    "vocabulary": "",
    "formats": ["txt", "readable", "docx", "srt"],
    "output_mode": "beside",       # beside | folder
    "output_folder": "",
    "batch_size": 8,
    "vad_method": "pyannote",
}

NO_WINDOW = 0x08000000  # CREATE_NO_WINDOW


def find_tool(name):
    local = os.path.join(RUNTIME_DIR, "ffmpeg", name + ".exe")
    if os.path.exists(local):
        return local
    p = shutil.which(name)
    if p:
        return p
    pattern = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Packages",
                           "Gyan.FFmpeg*", "*", "bin", name + ".exe")
    hits = glob.glob(pattern)
    return hits[0] if hits else None


def ensure_webview2_runtime():
    """Se a instalacao padrao do WebView2 estiver vazia (comum quando o Edge foi removido),
    aponta, apenas para este processo, para uma copia valida do runtime."""
    if os.environ.get("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER"):
        return
    pf86 = os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")
    default = glob.glob(os.path.join(pf86, "Microsoft", "EdgeWebView", "Application", "*", "msedgewebview2.exe"))
    if default:
        return
    candidates = []
    for root in (os.path.join(pf86, "Microsoft", "EdgeCore"),
                 os.path.join(os.environ.get("ProgramFiles", r"C:\Program Files"), "Microsoft", "EdgeCore")):
        for exe in glob.glob(os.path.join(root, "*", "msedgewebview2.exe")):
            folder = os.path.dirname(exe)
            ver = os.path.basename(folder)
            try:
                key = tuple(int(x) for x in ver.split("."))
            except ValueError:
                key = (0,)
            candidates.append((key, folder))
    if candidates:
        os.environ["WEBVIEW2_BROWSER_EXECUTABLE_FOLDER"] = max(candidates)[1]


ensure_webview2_runtime()

FFMPEG = find_tool("ffmpeg")
FFPROBE = find_tool("ffprobe")
if FFMPEG:
    os.environ["PATH"] = os.path.dirname(FFMPEG) + os.pathsep + os.environ.get("PATH", "")


def load_config():
    cfg = dict(DEFAULT_CONFIG)
    try:
        with open(CONFIG_PATH, "r", encoding="utf-8") as f:
            cfg.update(json.load(f))
    except Exception:
        # aproveita o vocabulario antigo do .bat, se existir
        try:
            with open(os.path.join(BASE_DIR, "vocabulario.txt"), "r", encoding="utf-8-sig") as f:
                v = f.read().strip()
                if v and not v.startswith("Transcrição em português"):
                    cfg["vocabulary"] = v
        except Exception:
            pass
    return cfg


def read_token():
    try:
        with open(TOKEN_PATH, "r", encoding="utf-8-sig") as f:
            return f.read().strip()
    except Exception:
        return ""


def probe_duration(path):
    if not FFPROBE:
        return None
    try:
        out = subprocess.run(
            [FFPROBE, "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
            capture_output=True, text=True, timeout=30, creationflags=NO_WINDOW,
        )
        return round(float(out.stdout.strip()), 1)
    except Exception:
        return None


def gpu_info():
    try:
        out = subprocess.run(
            ["nvidia-smi", "--query-gpu=name,memory.total", "--format=csv,noheader,nounits"],
            capture_output=True, text=True, timeout=10, creationflags=NO_WINDOW,
        )
        line = out.stdout.strip().splitlines()[0]
        name, mem = [x.strip() for x in line.split(",")]
        return {"available": True, "name": name.replace("NVIDIA ", ""), "memory_gb": round(int(mem) / 1024)}
    except Exception:
        return {"available": False, "name": None, "memory_gb": None}


def cpu_info():
    name = None
    try:
        import winreg
        k = winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"HARDWARE\DESCRIPTION\System\CentralProcessor\0")
        name = winreg.QueryValueEx(k, "ProcessorNameString")[0].strip()
        name = name.replace(" with Radeon Graphics", "")
    except Exception:
        pass
    return {"name": name or "Processador", "threads": os.cpu_count() or 4}


class Api:
    def __init__(self):
        self._window = None
        self._lock = threading.Lock()
        self._proc = None
        self._state = self._empty_state()
        self._log = []

    # ------------------------------------------------------------ helpers
    @staticmethod
    def _empty_state():
        return {"running": False, "status": "", "files": [], "finished": False, "fatal": None,
                "warnings": [], "started_at": None}

    def _file_info(self, path):
        path = os.path.normpath(path)
        return {
            "path": path,
            "name": os.path.basename(path),
            "folder": os.path.dirname(path),
            "duration": probe_duration(path),
        }

    def _expand(self, paths):
        result = []
        for p in paths or []:
            if os.path.isdir(p):
                for root, _, files in os.walk(p):
                    for fn in sorted(files):
                        if os.path.splitext(fn)[1].lower() in MEDIA_EXT:
                            result.append(os.path.join(root, fn))
            elif os.path.isfile(p) and os.path.splitext(p)[1].lower() in MEDIA_EXT:
                result.append(p)
        return [self._file_info(p) for p in result]

    def _add_log(self, line):
        line = line.rstrip()
        if not line:
            return
        with self._lock:
            self._log.append(line)
            if len(self._log) > 400:
                self._log = self._log[-400:]

    # ------------------------------------------------------------ chamadas da interface
    def get_initial(self):
        token = read_token()
        return {
            "config": load_config(),
            "gpu": gpu_info(),
            "cpu": cpu_info(),
            "ffmpeg": bool(FFMPEG),
            "token": {"present": bool(token), "hint": (token[:5] + "…" + token[-3:]) if len(token) > 10 else ""},
        }

    def save_config(self, cfg):
        merged = dict(DEFAULT_CONFIG)
        merged.update(cfg or {})
        with open(CONFIG_PATH, "w", encoding="utf-8") as f:
            json.dump(merged, f, ensure_ascii=False, indent=2)
        return True

    def pick_files(self):
        types = ("Vídeos e áudios (*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.webm;*.m4v;*.mpg;*.mpeg;*.ts;*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma)",
                 "Todos os arquivos (*.*)")
        sel = self._window.create_file_dialog(webview.FileDialog.OPEN, allow_multiple=True, file_types=types)
        return self._expand(list(sel) if sel else [])

    def pick_media_folder(self):
        sel = self._window.create_file_dialog(webview.FileDialog.FOLDER)
        return self._expand([sel[0]] if sel else [])

    def pick_output_folder(self):
        sel = self._window.create_file_dialog(webview.FileDialog.FOLDER)
        return sel[0] if sel else None

    def save_token(self, token):
        token = (token or "").strip()
        if not token.startswith("hf_") or len(token) < 20:
            return {"ok": False, "message": "O token deve começar com hf_."}
        with open(TOKEN_PATH, "w", encoding="utf-8") as f:
            f.write(token)
        return self.check_token()

    def check_token(self):
        token = read_token()
        if not token:
            return {"ok": False, "message": "Nenhum token salvo."}
        try:
            import huggingface_hub.file_download as _hf_fd
            _hf_fd.are_symlinks_supported = lambda cache_dir=None: False
            from huggingface_hub import HfApi, hf_hub_download
            HfApi(token=token).whoami()
        except Exception:
            return {"ok": False, "message": "Token inválido ou sem conexão."}
        try:
            hf_hub_download("pyannote/speaker-diarization-community-1", "config.yaml", token=token)
        except Exception:
            return {"ok": False, "message": "Token válido, mas o acesso ao modelo de falantes não foi liberado."}
        return {"ok": True, "message": "Token válido e modelo liberado."}

    def start(self, job):
        with self._lock:
            if self._proc and self._proc.poll() is None:
                return {"ok": False, "message": "Já existe uma transcrição em andamento."}

        cfg = job["config"]
        self.save_config(cfg)
        files = []
        for f in job["files"]:
            if cfg.get("output_mode") == "folder" and cfg.get("output_folder"):
                out = cfg["output_folder"]
            else:
                out = os.path.join(f["folder"], "transcricoes")
            files.append({"path": f["path"], "output_dir": out})

        device = cfg.get("device", "cuda")
        if cfg.get("quality") == "fast":
            model = "large-v3-turbo"
        else:
            model = "large-v3"

        worker_job = {
            "files": files,
            "model": model,
            "device": device,
            "compute_type": "float16" if device == "cuda" else "int8",
            "batch_size": int(cfg.get("batch_size") or 8) if device == "cuda" else 4,
            "threads": os.cpu_count(),
            "language": None if cfg.get("language") == "auto" else cfg.get("language"),
            "diarize": bool(cfg.get("diarize")),
            "hf_token": read_token() if cfg.get("diarize") else None,
            "vocabulary": cfg.get("vocabulary", ""),
            "formats": cfg.get("formats") or ["txt"],
            "vad_method": cfg.get("vad_method", "pyannote"),
        }
        if cfg.get("diarize"):
            mode = cfg.get("speakers_mode", "auto")
            if mode == "exact":
                worker_job["num_speakers"] = int(cfg.get("num_speakers") or 2)
            elif mode == "range":
                worker_job["min_speakers"] = int(cfg.get("min_speakers") or 1)
                worker_job["max_speakers"] = int(cfg.get("max_speakers") or 4)

        fd, job_path = tempfile.mkstemp(prefix="transcritor_", suffix=".json")
        with os.fdopen(fd, "w", encoding="utf-8") as fh:
            json.dump(worker_job, fh, ensure_ascii=False)

        with self._lock:
            self._log = []
            self._state = self._empty_state()
            self._state["running"] = True
            self._state["started_at"] = time.time()
            self._state["status"] = "Iniciando"
            self._state["files"] = [
                {"status": "queued", "stage": None, "stage_pct": 0, "file_pct": 0, "elapsed": 0,
                 "outputs": [], "out_dir": None, "error": None, "speed": None, "speakers": 0}
                for _ in files
            ]

        env = dict(os.environ)
        env["PYTHONIOENCODING"] = "utf-8"
        env["PYTHONUNBUFFERED"] = "1"
        env["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
        self._proc = subprocess.Popen(
            [VENV_PY, WORKER, job_path],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL,
            env=env, cwd=APP_DIR, creationflags=NO_WINDOW,
            text=True, encoding="utf-8", errors="replace", bufsize=1,
        )
        threading.Thread(target=self._reader, args=(self._proc, job_path), daemon=True).start()
        return {"ok": True}

    def _reader(self, proc, job_path):
        for raw in proc.stdout:
            if raw.startswith("@@"):
                try:
                    ev = json.loads(raw[2:])
                except Exception:
                    self._add_log(raw)
                    continue
                self._handle(ev)
            else:
                # remove barras de progresso do tqdm e ruido
                line = raw.strip()
                if line and "\r" not in raw[:-1] and "it/s]" not in line:
                    self._add_log(line)
        proc.wait()
        with self._lock:
            st = self._state
            st["running"] = False
            if not st["finished"] and not st.get("cancelled") and not st["fatal"]:
                st["fatal"] = "O processo de transcrição terminou inesperadamente. Veja o registro."
            for f in st["files"]:
                if f["status"] in ("queued", "running"):
                    f["status"] = "cancelled" if st.get("cancelled") else "error"
        try:
            os.remove(job_path)
        except Exception:
            pass
        try:
            with open(LOG_PATH, "w", encoding="utf-8") as f:
                f.write("\n".join(self._log))
        except Exception:
            pass

    def _handle(self, ev):
        t = ev.get("type")
        with self._lock:
            st = self._state
            i = ev.get("index")
            f = st["files"][i] if isinstance(i, int) and i < len(st["files"]) else None
            if t == "status":
                st["status"] = ev["message"]
                self._log.append("» " + ev["message"])
            elif t == "log":
                self._log.append(ev["message"])
            elif t == "warning":
                st["warnings"].append(ev["message"])
                self._log.append("Aviso: " + ev["message"])
            elif t == "file_start" and f:
                f["status"] = "running"
                st["status"] = "Transcrevendo"
            elif t == "progress" and f:
                f.update(stage=ev["stage"], stage_pct=ev["stage_pct"], file_pct=ev["file_pct"], elapsed=ev["elapsed"])
            elif t == "file_done" and f:
                f.update(status="done", file_pct=100, outputs=ev["outputs"], out_dir=ev["out_dir"],
                         elapsed=ev["elapsed"], speed=ev.get("speed"), speakers=ev.get("speakers", 0))
                self._log.append(f"Concluído em {ev['elapsed']:.0f}s ({ev.get('speed')}× o tempo real)")
            elif t == "file_error" and f:
                f.update(status="error", error=ev["message"])
                self._log.append("Erro: " + ev["message"])
                self._log.append(ev.get("detail", ""))
            elif t == "finished":
                st["finished"] = True
                st["status"] = "Concluído"
            elif t == "fatal":
                st["fatal"] = ev["message"]
                self._log.append("Erro: " + ev["message"])
                self._log.append(ev.get("detail", ""))

    def get_state(self):
        with self._lock:
            st = json.loads(json.dumps(self._state))
            st["log"] = self._log[-200:]
            if st["started_at"]:
                st["total_elapsed"] = round(time.time() - st["started_at"], 1)
            return st

    def cancel(self):
        proc = self._proc
        if proc and proc.poll() is None:
            with self._lock:
                self._state["cancelled"] = True
                self._state["status"] = "Cancelado"
            subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"],
                           capture_output=True, creationflags=NO_WINDOW)
        return True

    def open_path(self, path):
        if path and os.path.exists(path):
            os.startfile(path)
            return True
        return False

    def reveal(self, path):
        if path and os.path.exists(path):
            subprocess.Popen(["explorer", "/select,", os.path.normpath(path)])
            return True
        return False

    def read_text(self, path, limit=200000):
        try:
            with open(path, "r", encoding="utf-8-sig") as f:
                return f.read(limit)
        except Exception:
            return ""

    def open_log(self):
        if os.path.exists(LOG_PATH):
            os.startfile(LOG_PATH)


def bind_drop(window, api):
    def on_drop(e):
        files = (e.get("dataTransfer") or {}).get("files") or []
        paths = [f.get("pywebviewFullPath") for f in files if f.get("pywebviewFullPath")]
        infos = api._expand(paths)
        window.evaluate_js(f"window.onDroppedFiles({json.dumps(infos, ensure_ascii=False)})")

    def noop(e):
        pass

    window.dom.document.events.dragover += DOMEventHandler(noop, True, True)
    window.dom.document.events.drop += DOMEventHandler(on_drop, True, True)


def set_window_icon(window):
    """Troca o icone do Python pelo do Transcritor na barra de titulo e de tarefas."""
    try:
        from System import Action
        from System.Drawing import Icon as WinIcon
        form = window.native
        ico = WinIcon(os.path.join(APP_DIR, "transcritor.ico"))

        def apply():
            form.Icon = ico
        form.Invoke(Action(apply))
    except Exception:
        pass


def main():
    try:
        # identidade propria na barra de tarefas (nao agrupa com o Python)
        import ctypes
        ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID("FelipeKevin.Transcritor")
    except Exception:
        pass
    api = Api()
    window = webview.create_window(
        "Transcritor",
        url=os.path.join(UI_DIR, "index.html"),
        js_api=api,
        width=1280, height=840, min_size=(1040, 700),
        background_color="#F3F1EC",
        text_select=True,
    )
    api._window = window
    window.events.loaded += lambda: bind_drop(window, api)
    window.events.shown += lambda: set_window_icon(window)

    def on_closing():
        api.cancel()
    window.events.closing += on_closing

    webview.start(gui="edgechromium", debug="--debug" in sys.argv, private_mode=False,
                  storage_path=os.path.join(APP_DIR, ".webview"))


if __name__ == "__main__":
    main()
