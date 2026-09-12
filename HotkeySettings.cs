namespace HarmonicaPlayer;

// Values deliberately match Win32 MOD_ALT / MOD_CONTROL / MOD_SHIFT.
public record HotkeyBinding(uint Key, uint Modifiers = 0)
{
    public static HotkeyBinding DefaultStart => new(0x75);
    public static HotkeyBinding DefaultStop => new(0x77);
    public string Label => ((Modifiers & 2) != 0 ? "Ctrl + " : "") +
        ((Modifiers & 1) != 0 ? "Alt + " : "") +
        ((Modifiers & 4) != 0 ? "Shift + " : "") + KeyName(Key);
    public static string KeyName(uint key) => key switch
    {
        >= 0x70 and <= 0x7A => $"F{key - 0x6F}",
        >= 0x30 and <= 0x39 => ((char)key).ToString(),
        >= 0x41 and <= 0x5A => ((char)key).ToString(),
        0x1B => "Esc", 0x20 => "Space", 0x24 => "Home", 0x23 => "End",
        0x21 => "PageUp", 0x22 => "PageDown", 0x2D => "Insert", 0x2E => "Delete",
        _ => $"VK {key:X2}"
    };
    public string? Error()
    {
        if ((Modifiers & ~7u) != 0) return "仅支持 Ctrl、Alt、Shift，不支持 Windows 键。";
        if (Key == 0x7B) return "F12为系统调试保留键，请选择其他键。";
        if (Key is 0x5A or 0x58 or 0x43 or 0x56 or 0x42 or 0x4E or 0x4D or 0xBC)
            return "Z/X/C/V/B/N/M/逗号用于演奏，请选择其他快捷键（包括组合键）。";
        bool function = Key is >= 0x70 and <= 0x7A;
        bool text = Key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A;
        bool navigation = Key is 0x1B or 0x20 or 0x24 or 0x23 or 0x21 or 0x22 or 0x2D or 0x2E;
        if (!function && !text && !navigation) return "请选择 F1～F11、字母、数字或导航键。";
        if ((text || Key == 0x20) && Modifiers == 0)
            return "字母、数字和空格须搭配 Ctrl、Alt 或 Shift，避免影响日常输入。";
        if (Key == 0x2E && (Modifiers & 3) == 3) return "Ctrl+Alt+Delete为系统保留组合。";
        if (Key == 0x73 && (Modifiers & 1) != 0) return "Alt+F4用于关闭窗口，请选择其他组合。";
        if (Key == 0x1B && (Modifiers & 3) != 0) return "带 Ctrl 或 Alt 的 Esc 为系统快捷键，请选择其他组合。";
        if (Key == 0x20 && (Modifiers & 1) != 0) return "Alt+Space用于系统菜单，请选择其他组合。";
        return null;
    }
    public static void ValidatePair(HotkeyBinding start, HotkeyBinding stop)
    {
        if (start.Error() is string a) throw new FormatException("开始键：" + a);
        if (stop.Error() is string b) throw new FormatException("停止键：" + b);
        if (start == stop) throw new FormatException("开始键和停止键不能相同。");
    }
}

public interface IHotkeyBackend
{
    int Register(int id, HotkeyBinding binding);
    void Unregister(int id);
}

// Called on the window thread; track each registration independently.
public sealed class HotkeyController(IHotkeyBackend backend)
{
    public bool StartReady { get; private set; }
    public bool StopReady { get; private set; }
    public int StartError { get; private set; }
    public int StopError { get; private set; }
    public void Apply(HotkeyBinding start, HotkeyBinding stop)
    {
        HotkeyBinding.ValidatePair(start, stop); // Never discard valid registrations for invalid input.
        Suspend();
        StopError = backend.Register(2, stop); StopReady = StopError == 0;
        StartError = backend.Register(1, start); StartReady = StartError == 0;
    }
    public void Suspend()
    {
        if (StartReady) backend.Unregister(1);
        if (StopReady) backend.Unregister(2);
        StartReady = StopReady = false;
    }
    public string Describe(HotkeyBinding start, HotkeyBinding stop) =>
        $"开始 {start.Label}：{Result(StartReady, StartError)}；停止 {stop.Label}：{Result(StopReady, StopError)}。" +
        (!StopReady ? "停止键不可用，禁止真实演奏；请修改快捷键或解除占用后点“重试注册”。" :
         !StartReady ? "可点击“开始”按钮演奏；也可修改开始键或重试注册。" : "快捷键已就绪。");
    private static string Result(bool ok, int error) => ok ? "可用" :
        error == 1409 ? "被占用" : $"注册失败（错误码{error}，可能被其他程序占用）";
}
