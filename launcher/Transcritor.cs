// Transcritor.exe - inicializador e preparador do ambiente.
//
// Sem argumentos: abre o aplicativo; se o ambiente ainda nao existir, mostra a
// janela de preparo (primeiro uso da versao Portable).
// --preparar [--modelos] [--cpu] [--automatico]: usado pelo instalador.
//
// Compilado com o csc.exe do .NET Framework 4.x (C# 5), sem dependencias.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Transcritor")]
[assembly: System.Reflection.AssemblyProduct("Transcritor")]
[assembly: System.Reflection.AssemblyDescription("Transcritor - transcricao local com WhisperX")]
[assembly: System.Reflection.AssemblyCompany("Felipe Kevin")]
[assembly: System.Reflection.AssemblyCopyright("MIT License")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]

namespace Transcritor
{
    // ------------------------------------------------------------------ Tema

    static class Theme
    {
        public static bool Dark;
        public static Color Bg, Panel, Ink, Ink2, Muted, Faint, Line, LineSoft, Accent, Ok, Danger, Track, OnInk;
        public static FontFamily Serif, Sans, Mono;
        public static float Scale = 1f;

        public static void Init(Control probe)
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                Dark = v is int && (int)v == 0;
            }
            catch { Dark = false; }

            if (Dark)
            {
                Bg = C("#161514"); Panel = C("#1C1B19"); Ink = C("#ECE9E2"); Ink2 = C("#C9C5BC");
                Muted = C("#9A958B"); Faint = C("#6F6B63"); Line = C("#363430"); LineSoft = C("#2B2A27");
                Accent = C("#9CC1AB"); Ok = C("#9CC1AB"); Danger = C("#E08B78"); Track = C("#2E2C29"); OnInk = C("#161514");
            }
            else
            {
                Bg = C("#F3F1EC"); Panel = C("#F8F7F3"); Ink = C("#1C1B19"); Ink2 = C("#47443E");
                Muted = C("#7A756C"); Faint = C("#A39E94"); Line = C("#DCD7CC"); LineSoft = C("#E7E3DA");
                Accent = C("#2C4A3E"); Ok = C("#3F6A50"); Danger = C("#973A2A"); Track = C("#E3DFD6"); OnInk = C("#F3F1EC");
            }
            Serif = Family("Sitka Heading", "Sitka Text", "Georgia", "Cambria");
            Sans = Family("Segoe UI Variable Text", "Segoe UI", "Tahoma");
            Mono = Family("Cascadia Mono", "Consolas", "Courier New");
            using (Graphics g = probe.CreateGraphics()) { Scale = g.DpiX / 96f; }
        }

        static Color C(string hex) { return ColorTranslator.FromHtml(hex); }

        static FontFamily Family(params string[] names)
        {
            foreach (string n in names)
                foreach (FontFamily f in FontFamily.Families)
                    if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return f;
            return FontFamily.GenericSansSerif;
        }

        public static int S(int px) { return (int)Math.Round(px * Scale); }
        public static Font F(FontFamily fam, float pt) { return new Font(fam, pt, FontStyle.Regular, GraphicsUnit.Point); }
        public static Font FB(FontFamily fam, float pt) { return new Font(fam, pt, FontStyle.Bold, GraphicsUnit.Point); }
    }

    // ------------------------------------------------------------------ Controles

    class FlatButton : Control
    {
        public bool Primary;
        bool hover, down;

        public FlatButton(string text, bool primary)
        {
            Text = text; Primary = primary;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Font = Theme.F(Theme.Sans, 10f);
            Cursor = Cursors.Hand;
            Height = Theme.S(38);
            Width = Theme.S(primary ? 168 : 120);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fore;
            using (GraphicsPath p = Round(r, Theme.S(3)))
            {
                if (Primary)
                {
                    Color bg = !Enabled ? Theme.Track : (down ? Theme.Ink2 : (hover ? Blend(Theme.Ink, Theme.Bg, 0.18f) : Theme.Ink));
                    using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                    fore = Enabled ? Theme.OnInk : Theme.Faint;
                }
                else
                {
                    using (Pen pen = new Pen(hover && Enabled ? Theme.Ink2 : Theme.Line)) g.DrawPath(pen, p);
                    fore = Enabled ? Theme.Ink : Theme.Faint;
                }
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    class CheckLine : Control
    {
        bool _checked;
        public string Sub;
        public bool Checked { get { return _checked; } set { _checked = value; Invalidate(); } }

        public CheckLine(string text, string sub, bool value)
        {
            Text = text; Sub = sub; _checked = value;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Theme.F(Theme.Sans, 10f);
            Cursor = Cursors.Hand;
            Height = Theme.S(sub == null ? 26 : 44);
        }
        protected override void OnClick(EventArgs e) { if (Enabled) Checked = !Checked; base.OnClick(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int box = Theme.S(15);
            Rectangle br = new Rectangle(0, Theme.S(3), box, box);
            if (_checked && Enabled)
            {
                using (SolidBrush b = new SolidBrush(Theme.Ink)) g.FillRectangle(b, br);
                using (Pen p = new Pen(Theme.OnInk, Theme.S(2) * 0.8f))
                {
                    g.DrawLines(p, new PointF[] {
                        new PointF(br.X + box * 0.24f, br.Y + box * 0.52f),
                        new PointF(br.X + box * 0.43f, br.Y + box * 0.72f),
                        new PointF(br.X + box * 0.78f, br.Y + box * 0.30f) });
                }
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Theme.Dark ? Theme.Panel : Color.White)) g.FillRectangle(b, br);
                using (Pen p = new Pen(Enabled ? Theme.Ink2 : Theme.Line)) g.DrawRectangle(p, br);
            }
            int x = box + Theme.S(11);
            TextRenderer.DrawText(g, Text, Font, new Point(x, 0), Enabled ? Theme.Ink : Theme.Faint, TextFormatFlags.NoPadding);
            if (Sub != null)
                using (Font f = Theme.F(Theme.Sans, 9f))
                    TextRenderer.DrawText(g, Sub, f, new Rectangle(x, Theme.S(21), Width - x, Theme.S(22)), Theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    class ProgressLine : Control
    {
        double _v;
        public double Value { get { return _v; } set { _v = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
        public ProgressLine()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(2);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush t = new SolidBrush(Theme.Track)) e.Graphics.FillRectangle(t, ClientRectangle);
            using (SolidBrush a = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(a, 0, 0, (int)(Width * _v), Height);
        }
    }

    class Step
    {
        public string Name;
        public double Weight;
        public string State = "pendente";   // pendente | ativo | ok | pulado | erro
        public double Frac;
        public string Note = "";
        public Step(string n, double w) { Name = n; Weight = w; }
    }

    class StepList : Control
    {
        public List<Step> Steps = new List<Step>();
        public StepList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        public int RowH { get { return Theme.S(34); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent.BackColor);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (Font fIdx = Theme.F(Theme.Mono, 8.5f))
            using (Font fName = Theme.F(Theme.Sans, 10f))
            using (Font fState = Theme.F(Theme.Mono, 8.5f))
            using (Pen line = new Pen(Theme.LineSoft))
            using (Pen top = new Pen(Theme.Ink))
            {
                g.DrawLine(top, 0, 0, Width, 0);
                for (int i = 0; i < Steps.Count; i++)
                {
                    Step s = Steps[i];
                    int y = i * RowH;
                    Rectangle row = new Rectangle(0, y, Width, RowH);
                    TextRenderer.DrawText(g, (i + 1).ToString("00"), fIdx, new Rectangle(0, y, Theme.S(30), RowH), Theme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    Color nameColor = s.State == "pendente" ? Theme.Muted : Theme.Ink;
                    TextRenderer.DrawText(g, s.Name, fName, new Rectangle(Theme.S(30), y, Width / 2, RowH), nameColor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                    string st; Color sc;
                    switch (s.State)
                    {
                        case "ativo": st = s.Note.Length > 0 ? s.Note : "em andamento"; sc = Theme.Accent; break;
                        case "ok": st = s.Note.Length > 0 ? s.Note : "concluído"; sc = Theme.Ok; break;
                        case "pulado": st = s.Note.Length > 0 ? s.Note : "não necessário"; sc = Theme.Muted; break;
                        case "erro": st = "erro"; sc = Theme.Danger; break;
                        default: st = "aguardando"; sc = Theme.Faint; break;
                    }
                    TextRenderer.DrawText(g, st, fState, new Rectangle(Width / 2, y, Width / 2, RowH), sc,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                    if (s.State == "ativo" && s.Frac > 0)
                        using (SolidBrush a = new SolidBrush(Theme.Accent))
                            g.FillRectangle(a, Theme.S(30), y + RowH - Theme.S(2), (int)((Width - Theme.S(30)) * Math.Min(1, s.Frac)), Theme.S(2));
                    g.DrawLine(line, 0, y + RowH - 1, Width, y + RowH - 1);
                }
            }
        }
    }

    // ------------------------------------------------------------------ Janela

    class PrepForm : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        readonly Preparer prep;
        readonly bool setupMode, autoStart;
        Panel body;
        Label title, detail;
        StepList steps;
        ProgressLine overall;
        CheckLine chkModels, chkGpu;
        FlatButton btnPrimary, btnSecondary;
        TextBox logBox;
        string view = "intro";
        public int ExitCode = 1;

        public PrepForm(Preparer p, bool setup, bool auto)
        {
            prep = p; setupMode = setup; autoStart = auto;
            Theme.Init(this);
            Text = "Transcritor";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Ink;
            ClientSize = new Size(Theme.S(680), Theme.S(600));
            Font = Theme.F(Theme.Sans, 10f);
            DoubleBuffered = true;

            Label word = L("Transcritor", Theme.F(Theme.Serif, 17f), Theme.Ink);
            word.Location = new Point(Theme.S(36), Theme.S(22));
            Controls.Add(word);
            Label ver = L("versão " + Preparer.Version, Theme.F(Theme.Mono, 8.5f), Theme.Muted);
            ver.Location = new Point(ClientSize.Width - Theme.S(36) - ver.PreferredWidth, Theme.S(32));
            Controls.Add(ver);

            body = new Panel();
            body.BackColor = Theme.Bg;
            body.Location = new Point(0, Theme.S(72));
            body.Size = new Size(ClientSize.Width, ClientSize.Height - Theme.S(72) - Theme.S(74));
            Controls.Add(body);

            btnPrimary = new FlatButton("Preparar", true);
            btnSecondary = new FlatButton("Cancelar", false);
            btnPrimary.Location = new Point(ClientSize.Width - Theme.S(36) - btnPrimary.Width, ClientSize.Height - Theme.S(56));
            btnSecondary.Location = new Point(btnPrimary.Left - Theme.S(12) - btnSecondary.Width, btnPrimary.Top);
            btnPrimary.Click += OnPrimary;
            btnSecondary.Click += OnSecondary;
            Controls.Add(btnPrimary);
            Controls.Add(btnSecondary);

            prep.Changed += delegate { if (IsHandleCreated) BeginInvoke(new Action(RefreshProgress)); };
            prep.Finished += delegate (bool ok, string msg) { if (IsHandleCreated) BeginInvoke(new Action(delegate { OnFinished(ok, msg); })); };

            ShowIntro();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = Theme.Dark ? 1 : 0;
            try { DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (autoStart) Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Line))
            {
                e.Graphics.DrawLine(p, 0, Theme.S(71), ClientSize.Width, Theme.S(71));
                e.Graphics.DrawLine(p, 0, ClientSize.Height - Theme.S(74), ClientSize.Width, ClientSize.Height - Theme.S(74));
            }
        }

        static Label L(string text, Font f, Color c)
        {
            Label l = new Label();
            l.Text = text; l.Font = f; l.ForeColor = c; l.AutoSize = true; l.BackColor = Color.Transparent;
            l.UseMnemonic = false;
            return l;
        }

        Label Para(string text, int y, int h)
        {
            Label l = L(text, Theme.F(Theme.Sans, 10f), Theme.Ink2);
            l.AutoSize = false;
            l.Location = new Point(Theme.S(36), y);
            l.Size = new Size(body.Width - Theme.S(72), h);
            body.Controls.Add(l);
            return l;
        }

        void Title(string text)
        {
            title = L(text, Theme.F(Theme.Serif, 16f), Theme.Ink);
            title.Location = new Point(Theme.S(34), Theme.S(24));
            body.Controls.Add(title);
        }

        void InfoRow(string label, string value, int y, Color valueColor)
        {
            Label a = L(label.ToUpperInvariant(), Theme.FB(Theme.Sans, 7.5f), Theme.Muted);
            a.Location = new Point(Theme.S(36), y + Theme.S(2));
            body.Controls.Add(a);
            Label b = L(value, Theme.F(Theme.Mono, 9f), valueColor);
            b.AutoSize = false;
            b.AutoEllipsis = true;
            b.Location = new Point(Theme.S(176), y);
            b.Size = new Size(body.Width - Theme.S(176) - Theme.S(36), Theme.S(20));
            body.Controls.Add(b);
        }

        void Clear() { body.Controls.Clear(); }

        // ------------------------------------------------------------ telas

        void ShowIntro()
        {
            view = "intro";
            Clear();
            Title(setupMode ? "Baixar os componentes" : "Preparar o Transcritor");
            Para(setupMode
                ? "O instalador vai baixar e configurar tudo de que o Transcritor precisa: Python, ffmpeg, PyTorch e WhisperX. Os arquivos ficam na pasta escolhida."
                : "Na primeira vez, o Transcritor baixa os componentes de que precisa (Python, ffmpeg, PyTorch e WhisperX) e os guarda nesta pasta. Nada é instalado no Windows, e a pasta pode ser movida depois.",
                Theme.S(68), Theme.S(64));

            int y = Theme.S(150);
            bool longPath = prep.Root.Length > 80;
            InfoRow("Pasta", longPath ? prep.Root + "  (caminho longo: prefira uma pasta mais curta)" : prep.Root, y, longPath ? Theme.Danger : Theme.Ink); y += Theme.S(30);
            InfoRow("Placa de vídeo", prep.GpuName != null ? prep.GpuName : "não encontrada, o processador será usado", y, prep.GpuName != null ? Theme.Ok : Theme.Muted); y += Theme.S(30);
            InfoRow("Espaço livre", prep.FreeSpaceText(), y, prep.FreeBytes() < 16L * 1024 * 1024 * 1024 ? Theme.Danger : Theme.Ink); y += Theme.S(30);
            InfoRow("Download", "cerca de 4,5 GB, ou 11,5 GB com os modelos", y, Theme.Ink); y += Theme.S(44);

            chkGpu = new CheckLine("Usar a placa de vídeo", prep.GpuName != null ? "Recomendado. Até 20 vezes mais rápido que o processador." : "Nenhuma placa NVIDIA foi encontrada.", prep.GpuName != null && !prep.ForceCpu);
            chkGpu.Enabled = prep.GpuName != null;
            chkGpu.Location = new Point(Theme.S(36), y); chkGpu.Width = body.Width - Theme.S(72);
            body.Controls.Add(chkGpu); y += chkGpu.Height + Theme.S(8);

            chkModels = new CheckLine("Baixar também os modelos de transcrição agora", "Cerca de 7 GB. Assim o primeiro uso já funciona sem internet.", prep.WithModels);
            chkModels.Location = new Point(Theme.S(36), y); chkModels.Width = body.Width - Theme.S(72);
            body.Controls.Add(chkModels);

            btnPrimary.Text = "Preparar"; btnPrimary.Enabled = true; btnPrimary.Visible = true;
            btnSecondary.Text = "Cancelar"; btnSecondary.Visible = true;
        }

        void ShowProgress()
        {
            view = "progress";
            Clear();
            Title("Preparando");
            Para("Leva de 10 a 30 minutos, conforme a conexão. Pode continuar usando o computador.", Theme.S(68), Theme.S(24));

            steps = new StepList();
            steps.Steps = prep.Steps;
            steps.Location = new Point(Theme.S(36), Theme.S(106));
            steps.Size = new Size(body.Width - Theme.S(72), steps.RowH * prep.Steps.Count + 1);
            body.Controls.Add(steps);

            overall = new ProgressLine();
            overall.Location = new Point(Theme.S(36), steps.Bottom + Theme.S(18));
            overall.Width = body.Width - Theme.S(72);
            body.Controls.Add(overall);

            detail = L("", Theme.F(Theme.Mono, 8.5f), Theme.Muted);
            detail.AutoSize = false; detail.AutoEllipsis = true;
            detail.Location = new Point(Theme.S(36), overall.Bottom + Theme.S(10));
            detail.Size = new Size(body.Width - Theme.S(72), Theme.S(36));
            body.Controls.Add(detail);

            btnPrimary.Visible = false;
            btnSecondary.Text = "Cancelar"; btnSecondary.Visible = true;
            btnSecondary.Left = ClientSize.Width - Theme.S(36) - btnSecondary.Width;
        }

        void ShowDone()
        {
            view = "done";
            Clear();
            Title("Tudo pronto");
            Para(setupMode
                ? "Os componentes foram instalados. Conclua o assistente para começar a usar o Transcritor."
                : "O Transcritor está preparado. Das próximas vezes, ele abre direto.",
                Theme.S(68), Theme.S(48));
            int y = Theme.S(140);
            InfoRow("Processamento", prep.ResultDevice, y, Theme.Ok); y += Theme.S(30);
            InfoRow("Tempo", prep.ElapsedText(), y, Theme.Ink); y += Theme.S(30);
            InfoRow("Modelos", prep.WithModels ? "baixados, uso offline" : "serão baixados no primeiro uso", y, Theme.Ink);

            btnPrimary.Text = setupMode ? "Concluir" : "Abrir o Transcritor"; btnPrimary.Visible = true; btnPrimary.Enabled = true;
            btnPrimary.Left = ClientSize.Width - Theme.S(36) - btnPrimary.Width;
            btnSecondary.Visible = !setupMode;
            btnSecondary.Text = "Fechar";
            btnSecondary.Left = btnPrimary.Left - Theme.S(12) - btnSecondary.Width;
        }

        void ShowError(string msg)
        {
            view = "error";
            Clear();
            Title("Não foi possível concluir");
            Label m = Para(msg, Theme.S(68), Theme.S(44));
            m.ForeColor = Theme.Danger;

            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BorderStyle = BorderStyle.None;
            logBox.BackColor = Theme.Panel; logBox.ForeColor = Theme.Ink2;
            logBox.Font = Theme.F(Theme.Mono, 8.5f);
            logBox.Location = new Point(Theme.S(36), Theme.S(124));
            logBox.Size = new Size(body.Width - Theme.S(72), body.Height - Theme.S(140));
            logBox.Text = prep.LogTail(40);
            body.Controls.Add(logBox);
            logBox.SelectionStart = logBox.Text.Length; logBox.ScrollToCaret();

            btnPrimary.Text = "Tentar novamente"; btnPrimary.Visible = true; btnPrimary.Enabled = true;
            btnPrimary.Left = ClientSize.Width - Theme.S(36) - btnPrimary.Width;
            btnSecondary.Text = "Abrir registro"; btnSecondary.Visible = true;
            btnSecondary.Left = btnPrimary.Left - Theme.S(12) - btnSecondary.Width;
        }

        // ------------------------------------------------------------ acoes

        void Start()
        {
            if (chkGpu != null) prep.ForceCpu = !chkGpu.Checked;
            if (chkModels != null) prep.WithModels = chkModels.Checked;
            prep.BuildSteps();
            ShowProgress();
            prep.StartAsync();
        }

        void OnPrimary(object s, EventArgs e)
        {
            if (view == "intro") Start();
            else if (view == "error") { ShowIntro(); if (autoStart) Start(); }
            else if (view == "done")
            {
                ExitCode = 0;
                if (!setupMode) Program.LaunchApp(prep.Root);
                Close();
            }
        }

        void OnSecondary(object s, EventArgs e)
        {
            if (view == "progress")
            {
                if (MessageBox.Show(this, "Cancelar a preparação? O que já foi baixado é aproveitado na próxima vez.", "Transcritor",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    prep.Cancel();
                }
            }
            else if (view == "error") { try { Process.Start(prep.LogPath); } catch { } }
            else if (view == "done") { ExitCode = 0; Close(); }
            else Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (view == "progress" && prep.Running)
            {
                if (MessageBox.Show(this, "A preparação ainda está em andamento. Deseja interromper?", "Transcritor",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { e.Cancel = true; return; }
                prep.Cancel();
            }
            base.OnFormClosing(e);
        }

        void RefreshProgress()
        {
            if (view != "progress") return;
            steps.Invalidate();
            overall.Value = prep.OverallFraction();
            detail.Text = prep.Detail;
        }

        void OnFinished(bool ok, string msg)
        {
            if (ok)
            {
                ExitCode = 0;
                if (setupMode) { Close(); return; }   // o assistente mostra a pagina final
                ShowDone();
            }
            else if (msg == "cancelado")
            {
                if (setupMode) { ExitCode = 2; Close(); }
                else ShowIntro();
            }
            else ShowError(msg);
        }
    }

    // ------------------------------------------------------------------ Preparo

    delegate void FinishedHandler(bool ok, string message);

    class Preparer
    {
        public const string Version = "1.1.0";
        public readonly string Root, RuntimeDir, PyDir, PyExe, FfDir, ModelsDir, LogPath, CacheDir;
        public string GpuName;
        public bool ForceCpu, WithModels;
        public List<Step> Steps = new List<Step>();
        public string Detail = "";
        public string ResultDevice = "";
        public bool Running;
        public event EventHandler Changed;
        public event FinishedHandler Finished;

        Dictionary<string, object> deps;
        volatile bool cancelled;
        Process current;
        DateTime started;
        readonly object logLock = new object();
        readonly List<string> logLines = new List<string>();

        public Preparer(string root)
        {
            Root = root;
            RuntimeDir = Path.Combine(root, "runtime");
            PyDir = Path.Combine(RuntimeDir, "python");
            PyExe = Path.Combine(PyDir, "python.exe");
            FfDir = Path.Combine(RuntimeDir, "ffmpeg");
            ModelsDir = Path.Combine(root, "models");
            CacheDir = Path.Combine(RuntimeDir, "downloads");
            LogPath = Path.Combine(RuntimeDir, "preparo.log");
            GpuName = DetectGpu();
            string json = File.ReadAllText(Path.Combine(root, "app", "dependencias.json"), Encoding.UTF8);
            deps = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
        }

        public static bool IsReady(string root)
        {
            return File.Exists(Path.Combine(root, "runtime", "pronto.json")) &&
                   File.Exists(Path.Combine(root, "runtime", "python", "pythonw.exe"));
        }

        // ------------------------------------------------------------ informacoes

        static string DetectGpu()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=name --format=csv,noheader");
                psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(10000);
                    if (p.ExitCode == 0 && o.Length > 0) return o.Split('\n')[0].Trim();
                }
            }
            catch { }
            return null;
        }

        public long FreeBytes()
        {
            try { return new DriveInfo(Path.GetPathRoot(Root)).AvailableFreeSpace; } catch { return long.MaxValue; }
        }

        public string FreeSpaceText()
        {
            long b = FreeBytes();
            if (b == long.MaxValue) return "desconhecido";
            string t = string.Format("{0:0.#} GB em {1}", b / 1073741824.0, Path.GetPathRoot(Root).TrimEnd('\\'));
            if (b < 16L * 1073741824) t += "  (recomendado: 16 GB ou mais)";
            return t;
        }

        public string ElapsedText()
        {
            TimeSpan t = DateTime.Now - started;
            return t.TotalMinutes >= 1 ? string.Format("{0} min {1:00} s", (int)t.TotalMinutes, t.Seconds) : string.Format("{0} s", (int)t.TotalSeconds);
        }

        public string LogTail(int n)
        {
            lock (logLock)
            {
                int start = Math.Max(0, logLines.Count - n);
                return string.Join("\r\n", logLines.GetRange(start, logLines.Count - start).ToArray());
            }
        }

        // ------------------------------------------------------------ etapas

        public void BuildSteps()
        {
            bool gpu = GpuName != null && !ForceCpu;
            Steps = new List<Step>();
            Steps.Add(new Step("Componentes do Windows", 2));
            Steps.Add(new Step("Python 3.11", 3));
            Steps.Add(new Step("ffmpeg", 5));
            Steps.Add(new Step(gpu ? "PyTorch para a placa de vídeo" : "PyTorch para o processador", gpu ? 45 : 8));
            Steps.Add(new Step("WhisperX e dependências", 20));
            Steps.Add(new Step("Interface", 3));
            if (WithModels) Steps.Add(new Step("Modelos de transcrição", 40));
            Steps.Add(new Step("Verificação final", 2));
        }

        public double OverallFraction()
        {
            double total = 0, done = 0;
            foreach (Step s in Steps)
            {
                total += s.Weight;
                if (s.State == "ok" || s.State == "pulado") done += s.Weight;
                else if (s.State == "ativo") done += s.Weight * Math.Min(1, s.Frac);
            }
            return total > 0 ? done / total : 0;
        }

        void Raise() { EventHandler h = Changed; if (h != null) h(this, EventArgs.Empty); }

        void Log(string line)
        {
            lock (logLock)
            {
                logLines.Add(line);
                if (logLines.Count > 3000) logLines.RemoveRange(0, 1000);
                try { File.AppendAllText(LogPath, line + "\r\n", Encoding.UTF8); } catch { }
            }
        }

        void SetDetail(string d) { Detail = d; Raise(); }

        public void Cancel()
        {
            cancelled = true;
            try
            {
                Process p = current;
                if (p != null && !p.HasExited)
                {
                    Process k = Process.Start(new ProcessStartInfo("taskkill", "/PID " + p.Id + " /T /F") { CreateNoWindow = true, UseShellExecute = false });
                    k.WaitForExit(5000);
                }
            }
            catch { }
        }

        void Check() { if (cancelled) throw new OperationCanceledException(); }

        public void StartAsync()
        {
            cancelled = false; Running = true; started = DateTime.Now;
            Thread t = new Thread(Run);
            t.IsBackground = true;
            t.Start();
        }

        void Run()
        {
            Directory.CreateDirectory(RuntimeDir);
            Directory.CreateDirectory(CacheDir);
            Log("==== Preparo iniciado " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " em " + Root);
            Log("GPU: " + (GpuName ?? "nenhuma") + " | usar GPU: " + (GpuName != null && !ForceCpu) + " | modelos: " + WithModels);
            string error = null;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
                File.Delete(Path.Combine(RuntimeDir, "pronto.json"));
                int i = 0;
                RunStep(Steps[i++], StepWindows);
                RunStep(Steps[i++], StepPython);
                RunStep(Steps[i++], StepFfmpeg);
                RunStep(Steps[i++], StepTorch);
                RunStep(Steps[i++], StepPackages);
                RunStep(Steps[i++], StepInterface);
                if (WithModels) RunStep(Steps[i++], StepModels);
                RunStep(Steps[i++], StepVerify);
                try { Directory.Delete(CacheDir, true); } catch { }
            }
            catch (OperationCanceledException) { error = "cancelado"; Log("Cancelado pelo usuário."); }
            catch (Exception ex) { error = ex.Message; Log("ERRO: " + ex); }
            Running = false;
            foreach (Step s in Steps) if (s.State == "ativo") s.State = error == "cancelado" ? "pendente" : "erro";
            Raise();
            Log("==== Fim: " + (error ?? "sucesso"));
            FinishedHandler f = Finished;
            if (f != null) f(error == null, error);
        }

        void RunStep(Step s, Action body)
        {
            Check();
            s.State = "ativo"; s.Frac = 0; s.Note = ""; Detail = "";
            Log("---- " + s.Name);
            Raise();
            body();
            if (s.State == "ativo") s.State = "ok";
            s.Frac = 1;
            Raise();
        }

        Step Cur { get { foreach (Step s in Steps) if (s.State == "ativo") return s; return null; } }

        void Skip(string note)
        {
            Step s = Cur;
            if (s == null) return;
            s.State = "pulado"; s.Note = note;
            Log("Etapa pulada: " + note);
        }

        // ------------------------------------------------------------ utilitarios

        string Str(string key) { return (string)deps[key]; }
        Dictionary<string, object> Obj(string key) { return (Dictionary<string, object>)deps[key]; }
        List<string> List(string key)
        {
            List<string> r = new List<string>();
            foreach (object o in (IEnumerable)deps[key]) r.Add((string)o);
            return r;
        }
        long Estimate(string key)
        {
            object v = Obj("estimativa_bytes")[key];
            return Convert.ToInt64(v);
        }

        static string Gb(long b) { return b >= 1073741824 ? string.Format("{0:0.0} GB", b / 1073741824.0) : string.Format("{0:0} MB", b / 1048576.0); }

        string Download(string url, string fileName, string sha256)
        {
            string dest = Path.Combine(CacheDir, fileName);
            if (File.Exists(dest) && sha256 != null && Sha256(dest) == sha256) { Log("Já baixado: " + fileName); return dest; }
            Log("Baixando " + url);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Transcritor/" + Version;
            req.Timeout = 60000; req.ReadWriteTimeout = 120000;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream input = resp.GetResponseStream())
            using (FileStream output = File.Create(dest + ".part"))
            {
                long total = resp.ContentLength, got = 0;
                byte[] buf = new byte[1 << 16];
                int n; DateTime last = DateTime.MinValue;
                while ((n = input.Read(buf, 0, buf.Length)) > 0)
                {
                    Check();
                    output.Write(buf, 0, n); got += n;
                    if ((DateTime.Now - last).TotalMilliseconds > 250)
                    {
                        last = DateTime.Now;
                        Step s = Cur;
                        if (s != null && total > 0) { s.Frac = 0.9 * got / total; s.Note = string.Format("{0} de {1}", Gb(got), Gb(total)); }
                        SetDetail("Baixando " + fileName);
                    }
                }
            }
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(dest + ".part", dest);
            if (sha256 != null)
            {
                string h = Sha256(dest);
                if (h != sha256)
                {
                    File.Delete(dest);
                    throw new Exception("O arquivo " + fileName + " foi baixado corrompido ou alterado (impressão digital diferente). Tente novamente.");
                }
                Log("Integridade conferida (SHA-256): " + fileName);
            }
            return dest;
        }

        static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream f = File.OpenRead(path))
            {
                byte[] h = sha.ComputeHash(f);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static void RequireMicrosoftSignature(string path)
        {
            try
            {
                X509Certificate c = X509Certificate.CreateFromSignedFile(path);
                if (c.Subject.IndexOf("Microsoft Corporation", StringComparison.OrdinalIgnoreCase) >= 0) return;
            }
            catch { }
            throw new Exception("O instalador baixado da Microsoft não tem uma assinatura válida: " + Path.GetFileName(path));
        }

        int RunPy(string args, Action<string> onLine, Dictionary<string, string> extraEnv)
        {
            return RunProc(PyExe, args, onLine, extraEnv);
        }

        int RunProc(string exe, string args, Action<string> onLine, Dictionary<string, string> extraEnv)
        {
            Check();
            Log("> " + Path.GetFileName(exe) + " " + Regex.Replace(args, "hf_[A-Za-z0-9]+", "hf_***"));
            ProcessStartInfo psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8; psi.StandardErrorEncoding = Encoding.UTF8;
            psi.WorkingDirectory = RuntimeDir;
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            psi.EnvironmentVariables["PYTHONNOUSERSITE"] = "1";
            psi.EnvironmentVariables["PIP_DISABLE_PIP_VERSION_CHECK"] = "1";
            psi.EnvironmentVariables["PIP_NO_INPUT"] = "1";
            psi.EnvironmentVariables["HF_HOME"] = Path.Combine(ModelsDir, "huggingface");
            psi.EnvironmentVariables["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1";
            psi.EnvironmentVariables["TORCH_HOME"] = Path.Combine(ModelsDir, "torch");
            psi.EnvironmentVariables["NLTK_DATA"] = Path.Combine(ModelsDir, "nltk_data");
            Directory.CreateDirectory(Path.Combine(ModelsDir, "nltk_data"));
            psi.EnvironmentVariables.Remove("PYTHONPATH");
            psi.EnvironmentVariables.Remove("PYTHONHOME");
            if (extraEnv != null) foreach (KeyValuePair<string, string> kv in extraEnv) psi.EnvironmentVariables[kv.Key] = kv.Value;

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                DataReceivedEventHandler h = delegate (object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    string line = e.Data;
                    if (!line.StartsWith("Progress ")) Log(line);
                    if (onLine != null) { try { onLine(line); } catch { } }
                };
                p.OutputDataReceived += h; p.ErrorDataReceived += h;
                p.Start();
                current = p;
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                while (!p.WaitForExit(200)) { if (cancelled) { Cancel(); p.WaitForExit(5000); throw new OperationCanceledException(); } }
                p.WaitForExit();
                current = null;
                Check();
                Log("(código de saída " + p.ExitCode + ")");
                return p.ExitCode;
            }
        }

        static readonly Regex RxDownloading = new Regex(@"^\s*Downloading (\S+?)(?:-\d[^\s]*)?\.whl \(([\d\.]+) ([kMG]?B)\)");
        static readonly Regex RxProgress = new Regex(@"^Progress (\d+) of (\d+)");

        void Pip(string args, long estimate, string what)
        {
            long doneBytes = 0, curTotal = 0, curGot = 0;
            string curName = "";
            Action<string> parse = delegate (string line)
            {
                Match m = RxProgress.Match(line);
                if (m.Success)
                {
                    curGot = long.Parse(m.Groups[1].Value); curTotal = long.Parse(m.Groups[2].Value);
                    if (curGot >= curTotal && curTotal > 0) { doneBytes += curTotal; curGot = 0; curTotal = 0; }
                }
                else
                {
                    Match d = Regex.Match(line, @"^\s*Downloading (\S+)");
                    if (d.Success)
                    {
                        string n = d.Groups[1].Value;
                        int cut = n.IndexOf('-');
                        curName = cut > 0 ? n.Substring(0, cut) : n;
                    }
                    else if (line.StartsWith("Installing collected packages")) { curName = ""; SetDetail("Instalando os pacotes"); }
                    else if (line.StartsWith("Successfully installed")) SetDetail("Pacotes instalados");
                }
                Step s = Cur;
                if (s == null) return;
                long all = doneBytes + curGot;
                s.Frac = Math.Min(0.95, estimate > 0 ? (double)all / estimate : 0);
                if (all > 0) s.Note = string.Format("{0} baixados", Gb(all));
                if (curName.Length > 0 && curTotal > 0)
                    Detail = string.Format("Baixando {0}  ·  {1} de {2}", curName, Gb(curGot), Gb(curTotal));
                Raise();
            };
            int code = RunPy("-m pip install --no-warn-script-location --progress-bar raw " + args, parse, null);
            if (code != 0) throw new Exception("Falha ao instalar " + what + ". Veja o registro para detalhes.");
        }

        string Quote(IEnumerable<string> items)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in items) sb.Append(" \"").Append(s).Append('"');
            return sb.ToString();
        }

        // ------------------------------------------------------------ etapas concretas

        void StepWindows()
        {
            List<string> done = new List<string>();
            // Microsoft Visual C++ (necessario para o PyTorch)
            string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            bool vc = File.Exists(Path.Combine(sys32, "msvcp140.dll")) && File.Exists(Path.Combine(sys32, "vcruntime140_1.dll"));
            if (!vc)
            {
                string f = Download(Str("vcredist"), "vc_redist.x64.exe", null);
                RequireMicrosoftSignature(f);
                SetDetail("Autorize a instalação do Microsoft Visual C++ na janela do Windows");
                ProcessStartInfo psi = new ProcessStartInfo(f, "/install /quiet /norestart");
                psi.UseShellExecute = true; psi.Verb = "runas";
                using (Process p = Process.Start(psi)) p.WaitForExit();
                done.Add("Visual C++");
            }
            // WebView2 (desenha a janela do app)
            if (!HasWebView2())
            {
                string f = Download(Str("webview2"), "MicrosoftEdgeWebview2Setup.exe", null);
                RequireMicrosoftSignature(f);
                SetDetail("Instalando o Microsoft Edge WebView2");
                using (Process p = Process.Start(new ProcessStartInfo(f, "/silent /install") { UseShellExecute = true })) p.WaitForExit();
                done.Add("WebView2");
            }
            Step s = Cur;
            if (done.Count == 0) { s.State = "pulado"; s.Note = "já presentes"; }
            else s.Note = string.Join(", ", done.ToArray()) + " instalado";
        }

        static bool HasWebView2()
        {
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            foreach (string root in new string[] { Path.Combine(pf86, @"Microsoft\EdgeWebView\Application"), Path.Combine(pf86, @"Microsoft\EdgeCore") })
            {
                try
                {
                    foreach (string d in Directory.GetDirectories(root))
                        if (File.Exists(Path.Combine(d, "msedgewebview2.exe"))) return true;
                }
                catch { }
            }
            string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\EdgeWebView\Application");
            try { foreach (string d in Directory.GetDirectories(local)) if (File.Exists(Path.Combine(d, "msedgewebview2.exe"))) return true; } catch { }
            return false;
        }

        void StepPython()
        {
            string pip = Path.Combine(PyDir, @"Lib\site-packages\pip\__init__.py");
            if (File.Exists(PyExe) && File.Exists(pip)) { Skip("já preparado"); return; }
            Dictionary<string, object> py = Obj("python");
            string zip = Download((string)py["url"], "python-embed.zip", (string)py["sha256"]);
            SetDetail("Extraindo o Python");
            if (Directory.Exists(PyDir)) Directory.Delete(PyDir, true);
            ZipFile.ExtractToDirectory(zip, PyDir);
            foreach (string pth in Directory.GetFiles(PyDir, "python*._pth"))
            {
                string zipName = Path.GetFileNameWithoutExtension(pth) + ".zip";
                File.WriteAllText(pth, zipName + "\r\n.\r\nLib\\site-packages\r\n..\\..\\app\r\nimport site\r\n", new UTF8Encoding(false));
            }
            Cur.Frac = 0.92;
            string getpip = Download(Str("getpip"), "get-pip.py", null);
            SetDetail("Instalando o pip");
            if (RunPy("\"" + getpip + "\" --no-warn-script-location", null, null) != 0)
                throw new Exception("Falha ao instalar o pip no Python embutido.");
            Cur.Note = "3.11.9";
        }

        void StepFfmpeg()
        {
            if (File.Exists(Path.Combine(FfDir, "ffmpeg.exe")) && File.Exists(Path.Combine(FfDir, "ffprobe.exe"))) { Skip("já preparado"); return; }
            Dictionary<string, object> ff = Obj("ffmpeg");
            string zip = Download((string)ff["url"], "ffmpeg.zip", (string)ff["sha256"]);
            SetDetail("Extraindo o ffmpeg");
            Directory.CreateDirectory(FfDir);
            using (ZipArchive a = ZipFile.OpenRead(zip))
            {
                foreach (ZipArchiveEntry e in a.Entries)
                {
                    if (e.Name == "ffmpeg.exe" || e.Name == "ffprobe.exe")
                        e.ExtractToFile(Path.Combine(FfDir, e.Name), true);
                }
            }
            Cur.Note = "9.0.2";
        }

        bool TorchMatches(bool gpu)
        {
            string code = gpu
                ? "import torch,sys; sys.exit(0 if torch.version.cuda else 3)"
                : "import torch,sys; sys.exit(0)";
            try { return RunPy("-c \"" + code + "\"", null, null) == 0; } catch (OperationCanceledException) { throw; } catch { return false; }
        }

        void StepTorch()
        {
            bool gpu = GpuName != null && !ForceCpu;
            if (Directory.Exists(Path.Combine(PyDir, @"Lib\site-packages\torch")) && TorchMatches(gpu)) { Skip("já instalado"); return; }
            string index = Str(gpu ? "torch_index_gpu" : "torch_index_cpu");
            Pip("--force-reinstall" + Quote(List("torch")) + " --index-url " + index + " --extra-index-url https://pypi.org/simple",
                Estimate(gpu ? "torch_gpu" : "torch_cpu"), "o PyTorch");
            Cur.Note = gpu ? "com CUDA" : "para CPU";
        }

        void StepPackages()
        {
            Pip(Quote(List("pacotes")), Estimate("pacotes"), "o WhisperX");
            Cur.Note = "instalado";
        }

        void StepInterface()
        {
            Pip("setuptools wheel", 0, "as ferramentas de instalação");
            Dictionary<string, string> env = new Dictionary<string, string>();
            env["SETUPTOOLS_USE_DISTUTILS"] = "stdlib";
            List<string> ui = List("interface");
            foreach (string pkg in ui)
            {
                string extra = pkg.StartsWith("proxy_tools") ? " --no-build-isolation" : "";
                int code = RunPy("-m pip install --no-warn-script-location --progress-bar raw" + extra + " \"" + pkg + "\"", null, env);
                if (code != 0) throw new Exception("Falha ao instalar " + pkg + ".");
            }
            Cur.Note = "instalada";
        }

        void StepModels()
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            string spec = Path.Combine(RuntimeDir, "modelos.json");
            Dictionary<string, object> specObj = new Dictionary<string, object>();
            specObj["modelos"] = deps["modelos"];
            specObj["nltk"] = deps.ContainsKey("nltk") ? deps["nltk"] : new object[0];
            File.WriteAllText(spec, js.Serialize(specObj), new UTF8Encoding(false));
            string script = Path.Combine(RuntimeDir, "baixar_modelos.py");
            File.WriteAllText(script,
                "import json, os, sys\n" +
                "import huggingface_hub.file_download as _fd\n" +
                "if os.name == 'nt':\n" +
                "    # evita a corrida do teste de links simbolicos com downloads paralelos\n" +
                "    _fd.are_symlinks_supported = lambda cache_dir=None: False\n" +
                "from huggingface_hub import snapshot_download\n" +
                "spec = json.load(open(sys.argv[1], encoding='utf-8'))\n" +
                "for m in spec['modelos']:\n" +
                "    print('@@modelo ' + m['repo'], flush=True)\n" +
                "    snapshot_download(m['repo'], revision=m.get('revisao'),\n" +
                "                      allow_patterns=m.get('somente'), ignore_patterns=m.get('ignorar'))\n" +
                "if spec.get('nltk'):\n" +
                "    import nltk\n" +
                "    print('@@modelo dados do NLTK', flush=True)\n" +
                "    for pkg in spec['nltk']:\n" +
                "        if not nltk.download(pkg, download_dir=os.environ['NLTK_DATA'], quiet=True):\n" +
                "            sys.exit('falha ao baixar ' + pkg)\n" +
                "print('@@fim', flush=True)\n", new UTF8Encoding(false));
            long estimate = Estimate("modelos");
            string hf = Path.Combine(ModelsDir, "huggingface");
            Directory.CreateDirectory(hf);
            string currentModel = "";
            System.Threading.Timer poll = new System.Threading.Timer(delegate
            {
                long size = DirSize(hf);
                Step s = Cur;
                if (s == null) return;
                s.Frac = Math.Min(0.97, (double)size / estimate);
                s.Note = string.Format("{0} de ~{1}", Gb(size), Gb(estimate));
                Detail = "Baixando " + currentModel;
                Raise();
            }, null, 500, 1000);
            Dictionary<string, string> env = new Dictionary<string, string>();
            env["HF_HUB_DISABLE_PROGRESS_BARS"] = "1";
            int code;
            try
            {
                code = RunPy("\"" + script + "\" \"" + spec + "\"", delegate (string line)
                {
                    if (line.StartsWith("@@modelo ")) currentModel = line.Substring(9);
                }, env);
            }
            finally { poll.Dispose(); }
            if (code != 0) throw new Exception("Falha ao baixar os modelos. Verifique a conexão e tente novamente.");
            Cur.Note = Gb(DirSize(hf));
        }

        static long DirSize(string dir)
        {
            long t = 0;
            try { foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) { try { t += new FileInfo(f).Length; } catch { } } } catch { }
            return t;
        }

        void StepVerify()
        {
            SetDetail("Carregando as bibliotecas");
            string outText = "";
            int code = RunPy("-c \"import torch, whisperx, webview, docx; print('DEV', 'cuda' if torch.cuda.is_available() else 'cpu', torch.__version__)\"",
                delegate (string l) { if (l.StartsWith("DEV ")) outText = l; }, null);
            if (code != 0 || outText.Length == 0) throw new Exception("As bibliotecas não carregaram corretamente. Veja o registro.");
            bool cuda = outText.Contains(" cuda ");
            ResultDevice = cuda ? "placa de vídeo (" + GpuName + ")" : "processador";
            File.WriteAllText(Path.Combine(RuntimeDir, "pronto.json"),
                "{\n  \"versao\": \"" + Version + "\",\n  \"dispositivo\": \"" + (cuda ? "cuda" : "cpu") + "\",\n  \"data\": \"" + DateTime.Now.ToString("s") + "\"\n}\n",
                new UTF8Encoding(false));
            Cur.Note = cuda ? "placa de vídeo pronta" : "processador pronto";
            Detail = "";
        }
    }

    // ------------------------------------------------------------------ Entrada

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        public static void LaunchApp(string root)
        {
            ProcessStartInfo psi = new ProcessStartInfo(
                Path.Combine(root, @"runtime\python\pythonw.exe"),
                "\"" + Path.Combine(root, @"app\app.py") + "\"");
            psi.WorkingDirectory = Path.Combine(root, "app");
            psi.UseShellExecute = false;
            psi.EnvironmentVariables.Remove("PYTHONPATH");
            psi.EnvironmentVariables.Remove("PYTHONHOME");
            Process.Start(psi);
        }

        [STAThread]
        static int Main(string[] args)
        {
            string root = Path.GetDirectoryName(Application.ExecutablePath);
            List<string> a = new List<string>();
            foreach (string s in args) a.Add(s.ToLowerInvariant());

            bool prepare = a.Contains("--preparar");
            bool repair = a.Contains("--reparar");
            if (!prepare && !repair && Preparer.IsReady(root))
            {
                try { LaunchApp(root); return 0; }
                catch (Exception ex) { MessageBox.Show("Não foi possível abrir o Transcritor:\n" + ex.Message, "Transcritor", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
            }

            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!File.Exists(Path.Combine(root, @"app\dependencias.json")))
            {
                MessageBox.Show("A pasta \"app\" não foi encontrada ao lado do Transcritor.exe.\nExtraia o pacote completo antes de abrir.", "Transcritor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            Preparer prep = new Preparer(root);
            prep.WithModels = a.Contains("--modelos");
            prep.ForceCpu = a.Contains("--cpu");
            PrepForm form = new PrepForm(prep, prepare, a.Contains("--automatico"));
            Application.Run(form);
            return form.ExitCode;
        }
    }
}
