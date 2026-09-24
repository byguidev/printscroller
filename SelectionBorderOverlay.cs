using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrintScroller
{
    /// <summary>
    /// Moldura fina e "click-through" que fica sobre a área selecionada durante toda
    /// a captura, para que o usuário sempre veja o que está sendo gravado. Não recebe
    /// nenhum evento de mouse/teclado (WS_EX_TRANSPARENT) nem rouba o foco da janela
    /// que está sendo rolada (WS_EX_NOACTIVATE), então não interfere na rolagem.
    /// </summary>
    internal sealed class SelectionBorderOverlay : Form
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        // Distância (em px) entre a moldura e a borda real da área capturada. Sem essa
        // folga, a moldura fica exatamente em cima dos pixels fotografados e acaba
        // sendo capturada junto (aparecendo como faixas azuis coladas no resultado).
        private const int Gap = 6;

        private static readonly Color KeyColor = Color.FromArgb(1, 1, 1);

        public SelectionBorderOverlay(Rectangle region)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(
                region.X - Gap, region.Y - Gap,
                region.Width + Gap * 2, region.Height + Gap * 2);
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = KeyColor;
            TransparencyKey = KeyColor;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Color.DeepSkyBlue, 2);
            var rect = new Rectangle(1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
            e.Graphics.DrawRectangle(pen, rect);
        }
    }
}
