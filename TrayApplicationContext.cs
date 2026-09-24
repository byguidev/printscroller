using System;
using System.Windows.Forms;

namespace PrintScroller
{
    /// <summary>
    /// Contexto de aplicação de longa duração: fica residente (ícone na bandeja)
    /// esperando Ctrl+Shift+S para iniciar uma captura. O programa só é encerrado
    /// de fato com Ctrl+Shift+Q (ou "Sair" no menu da bandeja) — sair de uma
    /// captura com ENTER apenas volta ao estado ocioso, pronto para a próxima.
    /// </summary>
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private const string StartHotkeyName = "start";
        private const string QuitHotkeyName = "quit";
        private const string IdleTrayText = "PrintScroller - Ctrl+Shift+S para capturar";

        private readonly NotifyIcon _trayIcon;
        private readonly GlobalHotkey _hotkeys;
        private CaptureSession? _session;

        public TrayApplicationContext()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Selecionar área (Ctrl+Shift+S)", null, (_, _) => StartCapture());
            menu.Items.Add("Sair (Ctrl+Shift+Q)", null, (_, _) => Quit());

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = IdleTrayText,
                ContextMenuStrip = menu
            };

            _hotkeys = new GlobalHotkey();
            _hotkeys.Register(StartHotkeyName, HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.S);
            _hotkeys.Register(QuitHotkeyName, HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.Q);
            _hotkeys.Triggered += OnHotkeyTriggered;
        }

        private void OnHotkeyTriggered(string name)
        {
            switch (name)
            {
                case StartHotkeyName:
                    StartCapture();
                    break;
                case QuitHotkeyName:
                    Quit();
                    break;
            }
        }

        private void StartCapture()
        {
            if (_session != null) return; // já há uma captura em andamento

            var selection = SelectionOverlay.PromptForRegion();
            if (selection == null || selection.Value.Width < 10 || selection.Value.Height < 10)
            {
                return;
            }

            // Dá tempo para o overlay de seleção desaparecer completamente da tela
            // antes do primeiro print, evitando capturar resquícios dele.
            Application.DoEvents();
            System.Threading.Thread.Sleep(200);

            _session = new CaptureSession(selection.Value, _trayIcon, OnCaptureFinished);
        }

        private void OnCaptureFinished()
        {
            _session?.Dispose();
            _session = null;
            _trayIcon.Text = IdleTrayText;
        }

        private void Quit()
        {
            if (_session != null)
            {
                _session.Abort();
                _session.Dispose();
                _session = null;
            }

            _hotkeys.Triggered -= OnHotkeyTriggered;
            _hotkeys.Dispose();

            _trayIcon.Visible = false;
            _trayIcon.Dispose();

            ExitThread();
        }
    }
}
