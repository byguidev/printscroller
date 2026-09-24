using System;
using System.Drawing;
using System.Threading;
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
                "1. Arraste com o mouse para delimitar a área da tela a ser capturada.\n" +
                "2. Um print inicial dessa área é feito automaticamente.\n" +
                "3. Role o conteúdo normalmente (mouse, teclado, barra de rolagem).\n" +
                "4. Sempre que uma página cheia for revelada, um novo print é costurado ao anterior.\n" +
                "5. Pressione ENTER a qualquer momento para finalizar e salvar a imagem.",
                "PrintScroller",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            Rectangle? selection = SelectionOverlay.PromptForRegion();
            if (selection == null || selection.Value.Width < 10 || selection.Value.Height < 10)
            {
                MessageBox.Show("Nenhuma área válida foi selecionada. Encerrando.", "PrintScroller",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Dá tempo para o overlay de seleção desaparecer completamente da tela
            // antes do primeiro print, evitando capturar resquícios dele.
            Application.DoEvents();
            Thread.Sleep(200);

            using var context = new CaptureApplicationContext(selection.Value);
            Application.Run(context);
        }
    }
}
