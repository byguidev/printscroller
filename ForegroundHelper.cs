using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PrintScroller
{
    /// <summary>
    /// Diálogos disparados pelo hook global de teclado (ENTER) não ganham o direito
    /// automático de ir para o primeiro plano que o Windows concede a atalhos
    /// registrados via RegisterHotKey (Ctrl+Shift+S / Ctrl+Shift+Q) — por isso, sem
    /// isso aqui, eles abrem atrás da janela ativa e o usuário nem percebe que
    /// "algo aconteceu" ao pressionar ENTER. Usa o truque clássico de anexar
    /// temporariamente a fila de input da janela em primeiro plano para conseguir
    /// chamar SetForegroundWindow com sucesso.
    /// </summary>
    internal static class ForegroundHelper
    {
        private const int SW_SHOW = 5;

        /// <summary>
        /// Cria uma janela "dona" invisível já trazida para o primeiro plano, para
        /// ser usada como owner de um ShowDialog/MessageBox — assim o diálogo real
        /// nasce em primeiro plano em vez de atrás da janela ativa do usuário.
        /// </summary>
        public static Form CreateForegroundOwner()
        {
            var owner = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(Point.Empty, Size.Empty),
                TopMost = true,
            };
            owner.Show();
            ForceForeground(owner.Handle);
            return owner;
        }

        private static void ForceForeground(IntPtr hWnd)
        {
            IntPtr foreground = GetForegroundWindow();
            uint foregroundThreadId = GetWindowThreadProcessId(foreground, out _);
            uint currentThreadId = GetCurrentThreadId();

            bool attached = foregroundThreadId != 0
                && foregroundThreadId != currentThreadId
                && AttachThreadInput(currentThreadId, foregroundThreadId, true);

            ShowWindow(hWnd, SW_SHOW);
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);

            if (attached)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
