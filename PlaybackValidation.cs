namespace HarmonicaPlayer;

// Extracted following PR #1's shared-validation/test approach. Keep v0.2.0's
// preparation allowance and positional errors; never silently lengthen notes.
public static class PlaybackValidation
{
    public const int PreparationMs = 12;
    public static int ParseBpm(string text)
    {
        if (!int.TryParse(text, out int bpm) || bpm is < 20 or > 300)
            throw new FormatException("速度（BPM）范围20～300，请输入整数。");
        return bpm;
    }

    public static int ParseGap(string text)
    {
        if (!int.TryParse(text, out int silence) || silence is < 10 or > 5000)
            throw new FormatException("音符间隔／留白须为10～5000毫秒的整数。");
        return silence;
    }

    public static int ValidateGap(string text, IReadOnlyCollection<ScoreNote> notes, double beatMs)
    {
        if (!double.IsFinite(beatMs) || beatMs <= 0) throw new FormatException("每拍时长无效。");
        int silence = ParseGap(text);
        var shortNote = notes.FirstOrDefault(n => n.Degree != 0 && n.Beats * beatMs - silence - PreparationMs < 40);
        if (shortNote != null)
            throw new ScoreFormatException(shortNote.Position,
                $"第{shortNote.Position + 1}个字符的音过短：扣除{PreparationMs}ms准备和{silence}ms留白后不足40ms。请降低BPM或减小音符间隔。");
        return silence;
    }
}
