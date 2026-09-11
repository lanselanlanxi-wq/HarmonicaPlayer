namespace HarmonicaPlayer;

public enum ScoreSeparator { None, Space, LineBreak }

public record ScoreNote(int Degree, int Octave, bool Sharp, int Position,
    ScoreSeparator SeparatorBefore = ScoreSeparator.None, double Beats = 1)
{
    public int PauseBefore(int spaceMs, int lineMs) => SeparatorBefore switch
    {
        ScoreSeparator.Space => spaceMs,
        ScoreSeparator.LineBreak => lineMs,
        _ => 0
    };
    public string Label => Degree == 0 ? "休止" :
        (Octave == 1 ? "高" : Octave == -1 ? "低" : "") + (Sharp ? "#" : "") + Degree;
}

public static class ScoreParser
{
    public static List<ScoreNote> Parse(string text, bool rhythm = false)
    {
        var notes = new List<ScoreNote>();
        int octave = 0, opening = -1, sharpAt = -1;
        char closing = '\0';
        var separator = ScoreSeparator.None;
        bool modifierAllowed = false, dotted = false, extended = false;
        int reductions = 0;
        void Error(int at, string message) => throw new FormatException($"第{at + 1}个字符：{message}");
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                if (c is '\r' or '\n' or '\u2028' or '\u2029') separator = ScoreSeparator.LineBreak;
                else if (separator == ScoreSeparator.None) separator = ScoreSeparator.Space;
                continue;
            }
            if (c == '\uFEFF') continue;
            if (c is '|' or '｜') { modifierAllowed = false; continue; }
            if (rhythm && (c is '-' or '—' or '_' or '.'))
            {
                if (!modifierAllowed || notes.Count == 0 || sharpAt >= 0)
                    Error(i, "时值符号必须跟在音符或休止符后，不能跨小节。");
                var previous = notes[^1];
                if (c == '_')
                {
                    if (dotted || extended || reductions >= 2)
                        Error(i, "减时符最多两条，且须写在附点和延长符之前。");
                    reductions++;
                    notes[^1] = previous with { Beats = previous.Beats / 2 };
                }
                else if (c == '.')
                {
                    if (dotted || extended) Error(i, "仅支持一个附点，须写在延长符之前。");
                    dotted = true;
                    notes[^1] = previous with { Beats = previous.Beats * 1.5 };
                }
                else
                {
                    extended = true;
                    if (previous.Beats + 1 > 64) Error(i, "单个音或休止最多64拍。");
                    notes[^1] = previous with { Beats = previous.Beats + 1 };
                }
                continue;
            }
            if (c is '【' or '（' or '(')
            {
                if (closing != '\0') Error(i, "0.1版不支持嵌套音区括号。");
                if (sharpAt >= 0) Error(sharpAt, "请把#写在括号内音符之前，如【#6】。");
                octave = c == '【' ? 1 : -1;
                closing = c == '【' ? '】' : c == '（' ? '）' : ')';
                opening = i;
            }
            else if (c is '】' or '）' or ')')
            {
                if (c != closing) Error(i, "结束括号不匹配。");
                if (sharpAt >= 0) Error(sharpAt, "#后面缺少音符。");
                octave = 0;
                closing = '\0';
            }
            else if (c == '#')
            {
                if (sharpAt >= 0) Error(i, "不支持连续#。");
                sharpAt = i;
            }
            else if (c is >= '0' and <= '7')
            {
                if (c == '0' && sharpAt >= 0) Error(i, "休止符不能升半音。");
                notes.Add(new(c - '0', octave, sharpAt >= 0, i,
                    notes.Count == 0 ? ScoreSeparator.None : separator));
                separator = ScoreSeparator.None;
                modifierAllowed = true; dotted = extended = false; reductions = 0;
                sharpAt = -1;
            }
            else Error(i, $"无法识别“{c}”，请删除标题、歌词或修改符号。");
        }
        if (sharpAt >= 0) Error(sharpAt, "#后面缺少音符。");
        if (closing != '\0') Error(opening, "缺少结束括号。");
        if (notes.Count == 0) throw new FormatException("没有可演奏的音符。");
        if (notes.Count > 20000) throw new FormatException("0.1版最多支持20000个音符。");
        return notes;
    }
}
