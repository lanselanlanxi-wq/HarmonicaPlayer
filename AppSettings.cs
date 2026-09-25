using System.IO;
using System.Text.Json;

namespace HarmonicaPlayer;

public sealed record AppSettings
{
    public HotkeyBinding Start { get; init; } = HotkeyBinding.DefaultStart;
    public HotkeyBinding Stop { get; init; } = HotkeyBinding.DefaultStop;
    public int Bpm { get; init; } = 120;
    public int Gap { get; init; } = 20;
    public int PreviewVolume { get; init; } = 60;

    public void Validate()
    {
        if (Start is null || Stop is null) throw new FormatException("缺少快捷键设置。");
        HotkeyBinding.ValidatePair(Start, Stop);
        if (Bpm is < 20 or > 300 || Gap is < 10 or > 5000 || PreviewVolume is < 0 or > 100)
            throw new FormatException("设置数值超出允许范围。");
    }
}

public static class SettingsStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HarmonicaPlayer", "settings.json");
    public static AppSettings Load(string path, out string? warning)
    {
        warning = null;
        if (!File.Exists(path)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))
                ?? throw new FormatException("设置文件为空。");
            settings.Validate();
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            warning = "无法读取原设置，已使用默认值：" + e.Message;
            return new();
        }
    }
    public static void Save(string path, AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
