using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace PrintScroller
{
    internal sealed class CaptureApplicationContext : ApplicationContext
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
        private readonly List<Bitmap> _segments = new();

        private byte[]? _referenceGray;
        private bool _finished;
        private int _tickCount;

        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "printscroller_debug.log");

        private static void Log(string message)
        {
            try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n"); }
            catch { /* diagnostics only */ }
        }

        public CaptureApplicationContext(Rectangle region)
        {
            _region = region;
            Log($"=== nova captura, região={region.Width}x{region.Height} @ ({region.X},{region.Y}) ===");

            var first = CaptureRegion();
            _segments.Add(first);
            SetReference(first);

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "PrintScroller - role a tela; ENTER finaliza"
            };

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

            // Mesma lógica do polling normal: pega o que houver de novo desde o
            // último segmento costurado, mesmo que seja uma rolagem incompleta.
            TryCommitNewContent("ENTER");
            Log($"FIM: {_segments.Count} segmento(s) no total");

            _trayIcon.Visible = false;
            _trayIcon.Dispose();

            SaveResult();
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

            using var dlg = new SaveFileDialog
            {
                Title = "Salvar captura rolável",
                Filter = "PNG (*.png)|*.png",
                FileName = $"ScrollCapture_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                final.Save(dlg.FileName, ImageFormat.Png);
                MessageBox.Show($"Captura salva em:\n{dlg.FileName}", "PrintScroller",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            ExitThread();
        }
    }
}
