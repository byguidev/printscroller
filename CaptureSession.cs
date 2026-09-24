using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace PrintScroller
{
    /// <summary>
    /// Uma captura em andamento, da seleção da área até o ENTER que finaliza e salva.
    /// É dona apenas dos recursos daquela captura específica (timer, moldura visual,
    /// hook do ENTER); o NotifyIcon é compartilhado e pertence ao TrayApplicationContext,
    /// que sobrevive entre uma captura e outra.
    /// </summary>
    internal sealed class CaptureSession : IDisposable
    {
        private const int PollIntervalMs = 120;
        private const int ColumnStep = 3;
        private const int BandHeight = 32;
        private const int MatchErrorThreshold = 14;
        private const int MinCommitPx = 6;

        private readonly Rectangle _region;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly GlobalKeyboardHook _hook;
        private readonly NotifyIcon _trayIcon;
        private readonly SelectionBorderOverlay _borderOverlay;
        private readonly List<Bitmap> _segments = new();
        private readonly Action _onFinished;

        private byte[]? _referenceGray;
        private bool _finished;
        private bool _disposed;
        private int _tickCount;

        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "printscroller_debug.log");

        private static void Log(string message)
        {
            try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n"); }
            catch { /* diagnostics only */ }
        }

        public CaptureSession(Rectangle region, NotifyIcon trayIcon, Action onFinished)
        {
            _region = region;
            _trayIcon = trayIcon;
            _onFinished = onFinished;
            Log($"=== nova captura, região={region.Width}x{region.Height} @ ({region.X},{region.Y}) ===");

            var first = CaptureRegion();
            _segments.Add(first);
            SetReference(first);

            _borderOverlay = new SelectionBorderOverlay(region);
            _borderOverlay.Show();

            _trayIcon.Text = "PrintScroller - capturando: role a tela; ENTER finaliza";

            _hook = new GlobalKeyboardHook();
            _hook.EnterPressed += OnEnterPressed;

            _timer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
            _timer.Tick += (_, _) => Poll();
            _timer.Start();
        }

        private void SetReference(Bitmap bmp) => _referenceGray = ImageMath.ToGrayBuffer(bmp);

        private Bitmap CaptureRegion()
        {
            var bmp = new Bitmap(_region.Width, _region.Height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(_region.Location, Point.Empty, _region.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        private void Poll()
        {
            if (_finished) return;
            _tickCount++;
            TryCommitNewContent($"tick #{_tickCount}");
        }

        private void OnEnterPressed()
        {
            if (_finished) return;
            _finished = true;

            _timer.Stop();
            _hook.EnterPressed -= OnEnterPressed;
            _hook.Dispose();
            _borderOverlay.Close();

            // Mesma lógica do polling normal: pega o que houver de novo desde o
            // último segmento costurado, mesmo que seja uma rolagem incompleta.
            TryCommitNewContent("ENTER");
            Log($"FIM: {_segments.Count} segmento(s) no total");

            SaveResult();

            _onFinished();
        }

        /// <summary>
        /// Compara a área atual com o último quadro de referência e, se pixels novos
        /// e confiáveis surgiram na parte de baixo, costura só essa fatia nova. Usada
        /// tanto no polling contínuo quanto na finalização por ENTER — não há mais
        /// distinção entre "página cheia" e "sobra parcial": cada chamada simplesmente
        /// costura o que rolou desde a última vez, do tamanho que for.
        /// </summary>
        private void TryCommitNewContent(string logPrefix)
        {
            using var current = CaptureRegion();
            var currentGray = ImageMath.ToGrayBuffer(current);
            int offset = ImageMath.EstimateOffset(_referenceGray!, currentGray, _region.Width, _region.Height,
                ColumnStep, BandHeight, MatchErrorThreshold);

            Log($"{logPrefix} offset={offset}");
            if (offset < MinCommitPx) return;

            _segments.Add(ImageMath.CropBottom(current, offset));
            SetReference(current);
            Log($"  -> commit segmento #{_segments.Count} (altura={offset})");
        }

        private void SaveResult()
        {
            using var final = ImageMath.StitchVertically(_segments, _region.Width);
            foreach (var seg in _segments) seg.Dispose();
            _segments.Clear();

            // O ENTER é capturado por um hook de teclado, não por RegisterHotKey, então
            // não ganha o direito automático de ir para o primeiro plano: sem isso, o
            // diálogo abriria atrás da janela ativa do usuário, sem ele perceber.
            using var owner = ForegroundHelper.CreateForegroundOwner();

            using var dlg = new SaveFileDialog
            {
                Title = "Salvar captura rolável",
                Filter = "PNG (*.png)|*.png",
                FileName = $"ScrollCapture_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (dlg.ShowDialog(owner) == DialogResult.OK)
            {
                final.Save(dlg.FileName, ImageFormat.Png);
                MessageBox.Show(owner, $"Captura salva em:\n{dlg.FileName}", "PrintScroller",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Interrompe a captura sem salvar. Usado quando o atalho de matar o programa
        /// é pressionado no meio de uma captura em andamento.
        /// </summary>
        public void Abort()
        {
            if (_finished) return;
            _finished = true;

            _timer.Stop();
            _hook.EnterPressed -= OnEnterPressed;
            _hook.Dispose();
            _borderOverlay.Close();

            foreach (var seg in _segments) seg.Dispose();
            _segments.Clear();

            Log("ABORTADO (atalho de sair pressionado durante a captura)");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _timer.Dispose();
            if (!_finished)
            {
                _hook.EnterPressed -= OnEnterPressed;
                _hook.Dispose();
            }
            _borderOverlay.Dispose();

            foreach (var seg in _segments) seg.Dispose();
            _segments.Clear();
        }
    }
}
