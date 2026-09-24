using System.Windows.Forms;

namespace PrintScroller
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MessageBox.Show(
                "PrintScroller\n\n" +
                "O programa roda em segundo plano (ícone na bandeja) até ser encerrado.\n\n" +
                "• Ctrl+Shift+S: inicia uma captura. Arraste com o mouse para delimitar a área\n" +
                "  (ESC cancela). Uma moldura azul marca a área selecionada e some só quando a\n" +
                "  captura termina.\n" +
                "• Role o conteúdo normalmente (mouse, teclado, barra de rolagem) dentro da\n" +
                "  área marcada; cada trecho novo revelado é costurado automaticamente.\n" +
                "• ENTER a qualquer momento finaliza e salva a captura atual.\n" +
                "• Ctrl+Shift+Q encerra o PrintScroller por completo.",
                "PrintScroller",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            Application.Run(new TrayApplicationContext());
        }
    }
}
