using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrintScroller
{
    internal sealed class SelectionOverlay : Form
    {
        private Point _start;
        private Rectangle _selection;
        private bool _selecting;

        public Rectangle? Result { get; private set; }

        public SelectionOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Opacity = 0.35;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            KeyPreview = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _start = e.Location;
            _selecting = true;
            _selection = new Rectangle(e.Location, Size.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_selecting) return;

            int x = Math.Min(_start.X, e.X);
            int y = Math.Min(_start.Y, e.Y);
            int w = Math.Abs(e.X - _start.X);
            int h = Math.Abs(e.Y - _start.Y);
            _selection = new Rectangle(x, y, w, h);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _selecting = false;
            if (_selection.Width > 5 && _selection.Height > 5)
            {
                Result = new Rectangle(
                    Bounds.X + _selection.X,
                    Bounds.Y + _selection.Y,
                    _selection.Width,
                    _selection.Height);
            }
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_selection.Width <= 0 || _selection.Height <= 0) return;

            using var brush = new SolidBrush(Color.FromArgb(60, 30, 144, 255));
            using var pen = new Pen(Color.DeepSkyBlue, 2);
            e.Graphics.FillRectangle(brush, _selection);
            e.Graphics.DrawRectangle(pen, _selection);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Result = null;
                Close();
            }
        }

        public static Rectangle? PromptForRegion()
        {
            using var overlay = new SelectionOverlay();
            overlay.ShowDialog();
            return overlay.Result;
        }
    }
}
