using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PrintScroller
{
    [Flags]
    internal enum HotkeyModifiers : uint
    {
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
    }

    /// <summary>
    /// Atalhos globais registrados via RegisterHotKey (funcionam mesmo sem o app
    /// ter foco). Diferente do GlobalKeyboardHook (usado só para o ENTER "solto"),
    /// aqui as combinações já incluem modificadores, então não precisamos rastrear
    /// o estado de Ctrl/Shift manualmente.
    /// </summary>
    internal sealed class GlobalHotkey : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        private sealed class HotkeyWindow : NativeWindow
        {
            public event Action<int>? HotkeyPressed;

            public HotkeyWindow() => CreateHandle(new CreateParams());

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    HotkeyPressed?.Invoke((int)m.WParam);
                }
                base.WndProc(ref m);
            }
        }

        private readonly HotkeyWindow _window = new();
        private readonly Dictionary<int, string> _idToName = new();
        private int _nextId = 1;

        public event Action<string>? Triggered;

        public GlobalHotkey()
        {
            _window.HotkeyPressed += id =>
            {
                if (_idToName.TryGetValue(id, out var name))
                {
                    Triggered?.Invoke(name);
                }
            };
        }

        public void Register(string name, HotkeyModifiers modifiers, Keys key)
        {
            int id = _nextId++;
            _idToName[id] = name;

            if (!RegisterHotKey(_window.Handle, id, (uint)modifiers, (uint)key))
            {
                MessageBox.Show(
                    $"Não foi possível registrar o atalho global \"{name}\" ({modifiers}+{key}). " +
                    "Outro programa pode já estar usando essa combinação.",
                    "PrintScroller", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void Dispose()
        {
            foreach (var id in _idToName.Keys)
            {
                UnregisterHotKey(_window.Handle, id);
            }
            _idToName.Clear();
            _window.DestroyHandle();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
