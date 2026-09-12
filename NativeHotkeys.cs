using System.Runtime.InteropServices;

namespace HarmonicaPlayer;

public sealed class NativeHotkeys(IntPtr window) : IHotkeyBackend
{
    public int Register(int id, HotkeyBinding binding)
    {
        bool ok = NativeInput.RegisterHotKey(window, id, binding.Modifiers | 0x4000u, binding.Key);
        if (ok) return 0;
        int error = Marshal.GetLastWin32Error();
        return error == 0 ? -1 : error; // A failed call must never look like success.
    }
    public void Unregister(int id) => NativeInput.UnregisterHotKey(window, id);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    public static bool Held(uint triggerKey) =>
        new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0) ||
        (triggerKey != 0 && (GetAsyncKeyState((int)triggerKey) & 0x8000) != 0);
}
