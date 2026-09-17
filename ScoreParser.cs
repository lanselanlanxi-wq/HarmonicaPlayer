using System.Globalization;

namespace HarmonicaPlayer;

public sealed class ScoreFormatException(int position, string message) : FormatException(message)
{
    public int Position { get; } = position;
}

public record ScoreNote(int Degree, int Octave, bool Sharp, int Position, double Beats = 1)
{
    public string Label => Degree == 0 ? "休止" :
        (Octave == 2 ? "超高" : Octave == 1 ? "高" : Octave == -1 ? "低" : "") + (Sharp ? "#" : "") + Degree;
}

public static class ScoreParser
{
    public static List<ScoreNote> Parse(string text)
    {
        var notes = new List<ScoreNote>();
        int octave = 0, opening = -1, sharpAt = -1;
        bool nestedHigh = false, explicitDuration = false;
        char closing = '\0';
        bool modifierAllowed = false, dotted = false, extended = false;
        int reductions = 0;
        void Error(int at, string message)
        {
            int line = 1, column = 1;
            for (int j = 0; j < at; j++)
            {
                if (text[j] == '\n' || (text[j] == '\r' && (j + 1 >= text.Length || text[j + 1] != '\n')))
                { line++; column = 1; }
                else if (text[j] != '\r') column++;
            }
            throw new ScoreFormatException(at, $"第{line}行、第{column}列（第{at + 1}个字符）：{message}");
        }
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) continue;
            if (c == '\uFEFF') continue;
            if (c is '|' or '｜') { modifierAllowed = false; continue; }
            if (c == ':')
            {
                if (!modifierAllowed || notes.Count == 0 || sharpAt >= 0 ||
                    dotted || extended || reductions > 0 || explicitDuration)
                    Error(i, "指定拍数须跟在音符后，不能与附点、减时符、延长符混用。例：5:1.25。");
                int first = i + 1, end = first;
                while (end < text.Length && (text[end] is >= '0' and <= '9' || text[end] == '.')) end++;
                string value = text[first..end];
                if (value.Length == 0 || value[0] == '.' || value[^1] == '.' ||
                    !double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double beats) ||
                    !double.IsFinite(beats) || beats <= 0 || beats > 64)
                    Error(i, "拍数必须是大于0、不超过64的整数或小数，例如5:1.25、0:0.25。");
                // Parse again after validation so definite assignment is explicit.
                notes[^1] = notes[^1] with { Beats = double.Parse(value, CultureInfo.InvariantCulture) };
                explicitDuration = true;
                i = end - 1;
                continue;
            }
            if (c is '-' or '—' or '_' or '.')
            {
                if (!modifierAllowed || notes.Count == 0 || sharpAt >= 0)
                    Error(i, "时值符号必须跟在音符或休止符后，不能跨小节。");
                if (explicitDuration) Error(i, "指定拍数不能再加时值符号，请直接修改冒号后的总拍数。");
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
                if (closing != '\0')
                {
                    if (c == '【' && closing == '】' && octave == 1 && i == opening + 1)
                    { octave = 2; nestedHigh = true; modifierAllowed = false; continue; }
                    Error(i, "嵌套音区仅支持【【1】】（高两个八度的do）。");
                }
                if (sharpAt >= 0) Error(sharpAt, "请把#写在括号内音符之前，如【#6】。");
                octave = c == '【' ? 1 : -1;
                closing = c == '【' ? '】' : c == '（' ? '）' : ')';
                opening = i;
                modifierAllowed = false;
            }
            else if (c is '】' or '）' or ')')
            {
                if (c != closing) Error(i, "结束括号不匹配。");
                if (sharpAt >= 0) Error(sharpAt, "#后面缺少音符。");
                if (nestedHigh)
                {
                    if (i != opening + 3 || text[opening + 2] != '1' || i + 1 >= text.Length || text[i + 1] != '】')
                        Error(i, "最高音请完整写成【【1】】，时值放在括号外。");
                    i++; nestedHigh = false;
                }
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
                if (octave == 2 && (c != '1' || sharpAt >= 0 || i != opening + 2))
                    Error(i, "最高音区仅支持【【1】】，暂不支持其他音或升半音。");
                if (c == '0' && sharpAt >= 0) Error(i, "休止符不能升半音。");
                notes.Add(new(c - '0', octave, sharpAt >= 0, i));
                modifierAllowed = true; dotted = extended = explicitDuration = false; reductions = 0;
                sharpAt = -1;
            }
            else Error(i, $"无法识别“{c}”，请删除标题、歌词或修改符号。");
        }
        if (sharpAt >= 0) Error(sharpAt, "#后面缺少音符。");
        if (closing != '\0') Error(opening, "缺少结束括号。");
        if (notes.Count == 0) throw new FormatException("没有可演奏的音符。");
        if (notes.Count > 20000) throw new FormatException("最多支持20000个音符。");
        return notes;
    }
}
