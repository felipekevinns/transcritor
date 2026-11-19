"use strict";

// Velocidade aproximada (multiplo do tempo real) medida neste computador
const SPEED = {
  cuda: { precise: 12, fast: 20, diarize: 11 },
  cpu:  { precise: 1.0, fast: 1.4, diarize: 1.6 },
};

const STAGES = {
  audio: "Lendo o áudio",
  transcricao: "Transcrevendo",
  alinhamento: "Alinhando palavras",
  falantes: "Identificando falantes",
};

const $ = (s) => document.querySelector(s);
const $$ = (s) => Array.from(document.querySelectorAll(s));

let api = null;
let cfg = {};
let env = {};
let files = [];
let run = null;        // estado vindo do backend durante/apos execucao
let polling = null;
let previewPath = null;

/* ------------------------------------------------------------ util */

function fmtDur(sec) {
  if (sec == null || isNaN(sec)) return "—";
  sec = Math.round(sec);
  const h = Math.floor(sec / 3600), m = Math.floor((sec % 3600) / 60), s = sec % 60;
  return h ? `${h}:${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}` : `${m}:${String(s).padStart(2, "0")}`;
}

function fmtLong(sec) {
  if (sec == null || isNaN(sec)) return "—";
  sec = Math.max(0, Math.round(sec));
  if (sec < 60) return `${sec} s`;
  const h = Math.floor(sec / 3600), m = Math.round((sec % 3600) / 60);
  if (!h) return `${m} min`;
  return m ? `${h} h ${m} min` : `${h} h`;
}

function el(tag, cls, text) {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text != null) e.textContent = text;
  return e;
}

let toastTimer = null;
function toast(msg) {
  const t = $("#toast");
  t.textContent = msg;
  t.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => (t.hidden = true), 3600);
}

const isRunning = () => !!(run && run.running);

/* ------------------------------------------------------------ configuracao */

let saveTimer = null;
function saveSoon() {
  clearTimeout(saveTimer);
  saveTimer = setTimeout(() => api.save_config(cfg), 400);
}

function applyConfigToUI() {
  $$(".seg[data-key]").forEach((seg) => {
    const key = seg.dataset.key;
    seg.querySelectorAll("button").forEach((b) => b.classList.toggle("active", String(cfg[key]) === b.dataset.value));
  });
  ["language", "batch_size", "vad_method"].forEach((id) => ($("#" + id).value = String(cfg[id])));
  $("#diarize").checked = !!cfg.diarize;
  ["num_speakers", "min_speakers", "max_speakers"].forEach((id) => ($("#" + id).value = cfg[id]));
  $("#vocabulary").value = cfg.vocabulary || "";
  $$("#formats input").forEach((c) => (c.checked = (cfg.formats || []).includes(c.value)));
  $$("input[name=output_mode]").forEach((r) => (r.checked = r.value === cfg.output_mode));
  $("#outputFolderText").textContent = cfg.output_folder || "Nenhuma pasta escolhida";
  refreshDependent();
}

function refreshDependent() {
  // qualidade
  $("#qualityHint").textContent = cfg.quality === "precise"
    ? "O modelo mais preciso. Indicado quando o texto precisa sair o mais correto possível."
    : "Bem mais rápido, com precisão um pouco menor em nomes e termos raros.";

  // dispositivo
  const gpuOk = env.gpu && env.gpu.available;
  const gpuBtn = $('.seg[data-key=device] button[data-value=cuda]');
  gpuBtn.disabled = !gpuOk;
  if (!gpuOk && cfg.device === "cuda") { cfg.device = "cpu"; }
  $$(".seg[data-key=device] button").forEach((b) => b.classList.toggle("active", b.dataset.value === cfg.device));
  $("#deviceHint").textContent = cfg.device === "cuda"
    ? "Recomendado. Um vídeo de 2 horas leva por volta de " + fmtLong(7200 / SPEED.cuda[cfg.quality] + (cfg.diarize ? 7200 / SPEED.cuda.diarize : 0)) + "."
    : "Funciona sem placa de vídeo, mas é bem mais lento: um vídeo de 2 horas leva por volta de " +
      fmtLong(7200 / SPEED.cpu[cfg.quality] + (cfg.diarize ? 7200 / SPEED.cpu.diarize : 0)) +
      (cfg.quality === "precise" ? ". A qualidade Rápida ajuda bastante aqui." : ".");

  // falantes
  $("#speakerBlock").classList.toggle("off", !cfg.diarize);
  $("#exactFields").hidden = cfg.speakers_mode !== "exact";
  $("#rangeFields").hidden = cfg.speakers_mode !== "range";

  // pasta
  $("#folderLine").classList.toggle("off", cfg.output_mode !== "folder");

  renderTotals();
  renderStartButton();
}

function renderToken(t, message, ok) {
  const s = $("#tokenStatus");
  s.innerHTML = "";
  if (t && t.present) {
    s.append("Token do Hugging Face ");
    s.append(el("span", "ok", "configurado"));
  } else {
    s.append("Token do Hugging Face ");
    s.append(el("span", "bad", "não configurado"));
    $("#tokenForm").hidden = false;
  }
  const m = $("#tokenMsg");
  m.textContent = message || (t && t.present ? "" : "Necessário apenas para identificar falantes.");
  m.className = "hint" + (ok === true ? " ok" : ok === false ? " error" : "");
}

/* ------------------------------------------------------------ arquivos */

function addFiles(list) {
  if (isRunning()) { toast("Aguarde a transcrição atual terminar."); return; }
  if (run && !run.running) { resetRun(); }
  const known = new Set(files.map((f) => f.path.toLowerCase()));
  let added = 0;
  (list || []).forEach((f) => {
    if (!known.has(f.path.toLowerCase())) { files.push(f); known.add(f.path.toLowerCase()); added++; }
  });
  if (list && list.length && !added) toast("Esses arquivos já estão na lista.");
  if (list && !list.length) toast("Nenhum vídeo ou áudio encontrado.");
  renderFiles();
}

function resetRun() {
  run = null;
  $("#overallFill").style.width = "0";
  $("#overallLabel").textContent = "Pronto";
  $("#overallMeta").textContent = "";
}

function renderFiles() {
  const has = files.length > 0;
  $("#dropzone").hidden = has;
  $("#tableWrap").hidden = !has;
  $("#queueFoot").hidden = !has;
  $("#clearList").hidden = !has || isRunning();

  const tbody = $("#fileRows");
  tbody.innerHTML = "";
  files.forEach((f, i) => {
    const st = run && run.files[i] ? run.files[i] : null;
    const tr = el("tr");

    tr.append(el("td", "idx", String(i + 1).padStart(2, "0")));

    const tdName = el("td");
    tdName.append(el("div", "f-name", f.name));
    const folder = el("div", "f-folder");
    folder.append(document.createTextNode("‎" + f.folder));
    tdName.append(folder);
    tr.append(tdName);

    tr.append(el("td", "num", fmtDur(f.duration)));

    const tdSt = el("td");
    const box = el("div", "st " + (st ? st.status : "idle"));
    const main = el("span", "st-main");
    const sub = el("span", "st-sub");
    if (!st) {
      main.textContent = "Na fila";
      box.className = "st queued";
    } else if (st.status === "queued") {
      main.textContent = "Aguardando";
    } else if (st.status === "running") {
      main.textContent = `${STAGES[st.stage] || "Preparando"}  ${Math.floor(st.file_pct)}%`;
      const eta = st.file_pct > 3 ? (st.elapsed / (st.file_pct / 100)) - st.elapsed : null;
      sub.textContent = fmtDur(st.elapsed) + (eta != null ? `  ·  faltam ~${fmtLong(eta)}` : "");
    } else if (st.status === "done") {
      main.textContent = "Concluído";
      sub.textContent = `${fmtDur(st.elapsed)}  ·  ${String(st.speed).replace(".", ",")}×` + (st.speakers ? `  ·  ${st.speakers} ${st.speakers === 1 ? "falante" : "falantes"}` : "");
      sub.title = `Levou ${fmtLong(st.elapsed)}, ${String(st.speed).replace(".", ",")} vezes mais rápido que a duração do vídeo.`;
    } else if (st.status === "error") {
      main.textContent = "Erro";
      sub.textContent = st.error || "Veja o registro.";
    } else if (st.status === "cancelled") {
      main.textContent = "Cancelado";
    }
    box.append(main, sub);
    tdSt.append(box);
    if (st && (st.status === "running")) {
      const bar = el("div", "row-bar");
      const fill = el("div");
      fill.style.width = st.file_pct + "%";
      bar.append(fill);
      tdSt.append(bar);
    }
    tr.append(tdSt);

    const tdA = el("td", "acts");
    if (st && st.status === "done") {
      const readable = st.outputs.find((p) => p.endsWith("com horários.txt")) || st.outputs.find((p) => p.endsWith(".txt"));
      if (readable) {
        const b = el("button", "text-btn", "Ler");
        b.onclick = () => openPreview(f.name, readable);
        tdA.append(b);
      }
      const main = st.outputs.find((p) => p.endsWith(".docx")) || readable || st.outputs[0];
      if (main) {
        const b = el("button", "text-btn", "Abrir");
        b.onclick = () => api.open_path(main);
        tdA.append(b);
      }
      const b2 = el("button", "text-btn", "Pasta");
      b2.onclick = () => api.reveal(st.outputs[0] || st.out_dir);
      tdA.append(b2);
    } else if (!isRunning()) {
      const b = el("button", "text-btn remove", "Remover");
      b.onclick = () => { files.splice(i, 1); if (run) resetRun(); renderFiles(); };
      tdA.append(b);
    }
    tr.append(tdA);
    tbody.append(tr);
  });

  renderTotals();
  renderStartButton();
}

function estimateSeconds(totalSec) {
  const sp = SPEED[cfg.device] || SPEED.cuda;
  let s = totalSec / sp[cfg.quality];
  if (cfg.diarize) s += totalSec / sp.diarize;
  return s + 15 + files.length * 5;
}

function renderTotals() {
  if (!files.length) return;
  const known = files.filter((f) => f.duration != null);
  const total = known.reduce((a, f) => a + f.duration, 0);
  const n = files.length;
  $("#totalsText").textContent = `${n} ${n === 1 ? "arquivo" : "arquivos"}  ·  ${fmtLong(total)} de mídia`;
  $("#estimateText").textContent = total ? `estimativa de ${fmtLong(estimateSeconds(total))} com as opções atuais` : "";
}

function renderStartButton() {
  const btn = $("#startBtn");
  const running = isRunning();
  btn.hidden = running;
  $("#cancelBtn").hidden = !running;
  btn.disabled = !files.length || !(cfg.formats || []).length;
  btn.textContent = run && !run.running && run.finished ? "Transcrever novamente" : "Transcrever";
  $("#addFiles").disabled = running;
  $("#addFolder").disabled = running;
  $("#settings").style.pointerEvents = running ? "none" : "";
  $("#settings").style.opacity = running ? ".55" : "";
}

/* ------------------------------------------------------------ execucao */

async function start() {
  if (!files.length) return;
  if (!(cfg.formats || []).length) { toast("Escolha ao menos um tipo de arquivo para gerar."); return; }
  if (cfg.output_mode === "folder" && !cfg.output_folder) { toast("Escolha a pasta onde salvar."); return; }
  if (cfg.diarize && !(env.token && env.token.present)) {
    toast("Para identificar falantes, configure o token do Hugging Face.");
    return;
  }
  const r = await api.start({ files, config: cfg });
  if (!r.ok) { toast(r.message); return; }
  run = { running: true, files: files.map(() => ({ status: "queued", file_pct: 0 })) };
  renderFiles();
  poll();
}

function poll() {
  clearInterval(polling);
  polling = setInterval(async () => {
    const st = await api.get_state();
    run = st;
    updateOverall(st);
    renderFiles();
    renderLog(st.log);
    if (!st.running) {
      clearInterval(polling);
      polling = null;
      finish(st);
    }
  }, 500);
}

function updateOverall(st) {
  const n = st.files.length || 1;
  const pct = st.files.reduce((a, f) => a + (f.status === "done" ? 100 : f.file_pct || 0), 0) / n;
  $("#overallFill").style.width = pct.toFixed(1) + "%";
  const done = st.files.filter((f) => f.status === "done").length;
  const current = st.files.findIndex((f) => f.status === "running");
  let label = st.status || "Processando";
  if (st.running && current >= 0) label = `Arquivo ${current + 1} de ${n}`;
  if (!st.running) {
    const errs = st.files.filter((f) => f.status === "error").length;
    if (st.cancelled) label = "Cancelado";
    else if (st.fatal) label = "Não foi possível concluir";
    else if (errs) label = `${done} concluído${done === 1 ? "" : "s"}, ${errs} com erro`;
    else label = n === 1 ? "Transcrição concluída" : `${n} transcrições concluídas`;
  }
  $("#overallLabel").textContent = label;
  let meta = `${Math.floor(pct)}%  ·  ${fmtDur(st.total_elapsed)}`;
  if (st.running && current < 0) meta = `${st.status}  ·  ${fmtDur(st.total_elapsed)}`;
  $("#overallMeta").textContent = meta;
}

function finish(st) {
  renderStartButton();
  if (st.fatal) {
    toast(st.fatal);
    $("#logDrawer").hidden = false;
  }
}

function renderLog(lines) {
  const pre = $("#logText");
  const atBottom = pre.scrollTop + pre.clientHeight >= pre.scrollHeight - 20;
  pre.textContent = (lines || []).join("\n");
  if (atBottom) pre.scrollTop = pre.scrollHeight;
}

/* ------------------------------------------------------------ visualizacao */

async function openPreview(title, path) {
  previewPath = path;
  $("#previewTitle").textContent = title;
  const body = $("#previewBody");
  body.innerHTML = "";
  const text = await api.read_text(path);
  const lines = text.split(/\r?\n/);
  if (lines.length && !lines[0].startsWith("[")) lines.shift(); // titulo
  const re = /^\[(\d{2}:\d{2}:\d{2})\]\s?(.*)$/;
  let block = null;
  lines.forEach((ln) => {
    const m = ln.match(re);
    if (m) {
      const rest = m[2];
      if (/^Falante \d+$/.test(rest.trim())) {
        block = el("div", "tblock");
        const meta = el("div", "meta");
        meta.append(el("span", "who", rest.trim()), el("span", "ts", m[1]));
        block.append(meta);
        body.append(block);
      } else {
        const row = el("div", "tline");
        row.append(el("span", "ts", m[1]), el("span", null, rest));
        body.append(row);
        block = null;
      }
    } else if (ln.trim() && block) {
      block.append(el("div", null, ln));
    } else if (ln.trim()) {
      body.append(el("p", "tblock", ln));
    }
  });
  $("#preview").hidden = false;
}

/* ------------------------------------------------------------ eventos */

function bind() {
  $$(".seg[data-key] button").forEach((b) => {
    b.addEventListener("click", () => {
      const key = b.parentElement.dataset.key;
      cfg[key] = b.dataset.value;
      applyConfigToUI();
      saveSoon();
    });
  });

  ["language", "batch_size", "vad_method"].forEach((id) =>
    $("#" + id).addEventListener("change", (e) => {
      cfg[id] = id === "batch_size" ? parseInt(e.target.value, 10) : e.target.value;
      saveSoon(); refreshDependent();
    }));

  $("#diarize").addEventListener("change", (e) => { cfg.diarize = e.target.checked; refreshDependent(); saveSoon(); });

  ["num_speakers", "min_speakers", "max_speakers"].forEach((id) =>
    $("#" + id).addEventListener("change", (e) => {
      let v = Math.max(1, Math.min(20, parseInt(e.target.value, 10) || 1));
      e.target.value = v; cfg[id] = v;
      if (cfg.min_speakers > cfg.max_speakers) {
        if (id === "min_speakers") cfg.max_speakers = v; else cfg.min_speakers = v;
        $("#min_speakers").value = cfg.min_speakers; $("#max_speakers").value = cfg.max_speakers;
      }
      saveSoon();
    }));

  $("#vocabulary").addEventListener("input", (e) => { cfg.vocabulary = e.target.value; saveSoon(); });

  $$("#formats input").forEach((c) => c.addEventListener("change", () => {
    cfg.formats = $$("#formats input").filter((x) => x.checked).map((x) => x.value);
    saveSoon(); renderStartButton();
  }));

  $$("input[name=output_mode]").forEach((r) => r.addEventListener("change", () => {
    cfg.output_mode = r.value; refreshDependent(); saveSoon();
  }));

  $("#pickOutput").addEventListener("click", async () => {
    const p = await api.pick_output_folder();
    if (p) { cfg.output_folder = p; cfg.output_mode = "folder"; applyConfigToUI(); saveSoon(); }
  });

  const pick = async () => addFiles(await api.pick_files());
  $("#addFiles").addEventListener("click", pick);
  $("#dzPick").addEventListener("click", pick);
  $("#addFolder").addEventListener("click", async () => addFiles(await api.pick_media_folder()));
  $("#clearList").addEventListener("click", () => { files = []; resetRun(); renderFiles(); });

  $("#startBtn").addEventListener("click", start);
  $("#cancelBtn").addEventListener("click", async () => {
    $("#cancelBtn").disabled = true;
    await api.cancel();
    setTimeout(() => ($("#cancelBtn").disabled = false), 1500);
  });

  $("#logToggle").addEventListener("click", () => ($("#logDrawer").hidden = !$("#logDrawer").hidden));
  $("#logClose").addEventListener("click", () => ($("#logDrawer").hidden = true));

  $("#previewClose").addEventListener("click", () => ($("#preview").hidden = true));
  $("#preview").addEventListener("click", (e) => { if (e.target.id === "preview") $("#preview").hidden = true; });
  $("#previewOpen").addEventListener("click", () => previewPath && api.open_path(previewPath));
  $("#previewCopy").addEventListener("click", async () => {
    const text = $("#previewBody").innerText;
    try { await navigator.clipboard.writeText(text); toast("Texto copiado."); }
    catch { toast("Não foi possível copiar."); }
  });
  document.addEventListener("keydown", (e) => { if (e.key === "Escape") { $("#preview").hidden = true; $("#logDrawer").hidden = true; } });

  // token
  $("#tokenEdit").addEventListener("click", () => { $("#tokenForm").hidden = !$("#tokenForm").hidden; $("#tokenInput").focus(); });
  $("#tokenSave").addEventListener("click", async () => {
    $("#tokenMsg").textContent = "Verificando…"; $("#tokenMsg").className = "hint";
    const r = await api.save_token($("#tokenInput").value);
    $("#tokenInput").value = "";
    const init = await api.get_initial();
    env.token = init.token;
    if (env.token.present) $("#tokenForm").hidden = true;
    renderToken(env.token, r.message, r.ok);
  });
  $("#tokenCheck").addEventListener("click", async () => {
    $("#tokenMsg").textContent = "Verificando…"; $("#tokenMsg").className = "hint";
    const r = await api.check_token();
    renderToken(env.token, r.message, r.ok);
  });

  // arrastar e soltar (o caminho real chega pelo backend)
  let depth = 0;
  document.addEventListener("dragenter", (e) => { e.preventDefault(); depth++; document.body.classList.add("dragging"); });
  document.addEventListener("dragleave", () => { depth = Math.max(0, depth - 1); if (!depth) document.body.classList.remove("dragging"); });
  document.addEventListener("drop", () => { depth = 0; document.body.classList.remove("dragging"); });
}

window.onDroppedFiles = (infos) => addFiles(infos);

/* ------------------------------------------------------------ inicio */

async function init() {
  api = window.pywebview.api;
  env = await api.get_initial();
  cfg = env.config;

  const gpu = env.gpu;
  const line = $("#deviceLine");
  line.innerHTML = "";
  if (gpu.available) {
    line.append(el("span", "on", "GPU disponível"), document.createTextNode(`  ·  ${gpu.name}  ·  ${gpu.memory_gb} GB`));
    $("#gpuName").textContent = `${gpu.name}`;
  } else {
    line.append(document.createTextNode("Placa de vídeo NVIDIA não encontrada  ·  usando o processador"));
    $("#gpuName").textContent = "indisponível";
  }
  $("#cpuName").textContent = `${env.cpu.name.replace(/\s+\d+-Core Processor/i, "")} · ${env.cpu.threads} threads`;
  if (!env.ffmpeg) toast("ffmpeg não encontrado. A leitura de vídeos não vai funcionar.");

  renderToken(env.token);
  bind();
  applyConfigToUI();
  renderFiles();
}

window.addEventListener("pywebviewready", init);
