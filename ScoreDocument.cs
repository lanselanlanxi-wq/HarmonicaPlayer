using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace HarmonicaPlayer;

public sealed record ScoreDocument(string Title, int Bpm, string ScoreText,
    string? SourcePath = null, bool IsLegacy = false, string[]? ExtraHeaders = null, int Gap = 20)
{
    public const string FormatVersion = "HarmonicaPlayer/1";
    public List<ScoreNote> Validate(string gap)
    {
        ValidateTitle(Title);
        PlaybackValidation.ParseBpm(Bpm.ToString(CultureInfo.InvariantCulture));
        var notes = ScoreParser.Parse(ScoreText);
        PlaybackValidation.ValidateGap(gap, notes, 60000.0 / Bpm);
        return notes;
    }
    public static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || title.Any(char.IsControl) || title.Contains('\u2028') || title.Contains('\u2029'))
            throw new FormatException("曲名不能为空，最多200个字符，不能包含换行或控制字符。");
    }
}

public sealed record ScoreReadResult(ScoreDocument Document, string[] Warnings);

public static class ScoreDocumentReader
{
    public const int MaxBytes = 1024 * 1024;
    private static readonly Regex FileBpm = new(@"(?<![A-Za-z])bpm\s*([+-]?\d+(?:\.\d+)?)(?![\d.])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static readonly Regex Header = new(@"^@([A-Za-z][A-Za-z0-9_-]*)=(.*)$", RegexOptions.CultureInvariant);

    public static async Task<ScoreReadResult> ReadAsync(string path, int fallbackBpm)
    {
        // Bound the bytes actually read, not only FileInfo.Length (file may grow).
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        using var bytes = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer)) != 0)
        {
            if (bytes.Length + count > MaxBytes) throw new IOException("文件不得超过1MB。");
            bytes.Write(buffer, 0, count);
        }
        return Parse(new UTF8Encoding(false, true).GetString(bytes.ToArray()), path, fallbackBpm);
    }

    public static ScoreReadResult Parse(string text, string? path, int fallbackBpm)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes) throw new FormatException("文件不得超过1MB。");
        text = text.TrimStart('\uFEFF');
        string filename = path == null ? "未命名曲谱" : Path.GetFileNameWithoutExtension(path);
        var warnings = new List<string>();
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var extras = new List<string>();
        int offset = 0;
        bool hasHeader = text.TrimStart().StartsWith('@');
        if (hasHeader)
        {
            bool started = false, separated = false;
            while (offset < text.Length)
            {
                int end = offset;
                while (end < text.Length && text[end] is not '\r' and not '\n') end++;
                string raw = text[offset..end];
                int next = end;
                if (next < text.Length && text[next] == '\r') next++;
                if (next < text.Length && text[next] == '\n') next++;
                offset = next;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    if (started) { separated = true; break; }
                    continue;
                }
                var match = Header.Match(raw);
                if (!match.Success) throw new FormatException("文件头须为 @字段=值，并以一个空行与谱面分隔。");
                started = true;
                string key = match.Groups[1].Value, value = match.Groups[2].Value;
                if (!fields.TryAdd(key, value)) throw new FormatException($"文件头字段重复：@{key}。");
                if (!new[] { "format", "title", "bpm", "gap" }.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    extras.Add(raw);
                    warnings.Add($"未知字段 @{key} 已保留，但本版不解释其作用。");
                }
            }
            if (!separated) throw new FormatException("文件头后须留一个空行，再填写谱面。");
            if (!fields.TryGetValue("format", out var format) || format != ScoreDocument.FormatVersion)
                throw new FormatException("缺少或不支持的 @format，本版支持 HarmonicaPlayer/1。");
            if (!fields.ContainsKey("bpm")) throw new FormatException("新格式缺少 @bpm。");
        }

        int? nameBpm = null;
        var matches = FileBpm.Matches(filename);
        if (fields.ContainsKey("bpm"))
        {
            // A valid explicit header remains authoritative even if the name is stale/invalid.
            if (matches.Count == 1 && int.TryParse(matches[0].Groups[1].Value, out int value) && value is >= 20 and <= 300)
                nameBpm = value;
            else if (matches.Count > 0) warnings.Add("文件名中的BPM无效或不唯一，已使用文件头BPM。");
        }
        else if (matches.Count > 0)
        {
            if (matches.Count != 1) throw new FormatException("文件名含多个BPM，请保留一个明确的速度。");
            nameBpm = PlaybackValidation.ParseBpm(matches[0].Groups[1].Value);
        }
        int bpm;
        if (fields.TryGetValue("bpm", out var tempo))
        {
            bpm = PlaybackValidation.ParseBpm(tempo);
            if (nameBpm != null && nameBpm != bpm) warnings.Add($"文件名BPM为{nameBpm}，文件头为{bpm}；以文件头为准，文件名不会自动更改。");
        }
        else if (nameBpm is int fromName)
        {
            bpm = fromName;
            warnings.Add($"已从文件名读取BPM：{bpm}。另存为新格式后会写入文件头。");
        }
        else
        {
            bpm = PlaybackValidation.ParseBpm(fallbackBpm.ToString(CultureInfo.InvariantCulture));
            warnings.Add($"曲谱未标注BPM，沿用当前有效速度 {bpm}，请确认后保存。");
        }
        string title = fields.TryGetValue("title", out var given) && !string.IsNullOrWhiteSpace(given) ? given : filename;
        ScoreDocument.ValidateTitle(title);
        int gap = fields.TryGetValue("gap", out var gapText) ? PlaybackValidation.ParseGap(gapText) : 20;
        return new(new(title, bpm, hasHeader ? text[offset..] : text, path, !hasHeader, extras.ToArray(), gap), warnings.ToArray());
    }
}

public static class ScoreDocumentWriter
{
    public static string Serialize(ScoreDocument document, string gap)
    {
        document.Validate(gap);
        var result = new StringBuilder();
        result.Append("@format=").Append(ScoreDocument.FormatVersion).Append('\n');
        result.Append("@title=").Append(document.Title).Append('\n');
        result.Append("@bpm=").Append(document.Bpm.ToString(CultureInfo.InvariantCulture)).Append('\n');
        result.Append("@gap=").Append(PlaybackValidation.ParseGap(gap).ToString(CultureInfo.InvariantCulture)).Append('\n');
        var keys = new HashSet<string>(new[] { "format", "title", "bpm", "gap" }, StringComparer.OrdinalIgnoreCase);
        foreach (string line in document.ExtraHeaders ?? [])
        {
            var match = ScoreDocumentReader.Header.Match(line);
            if (!match.Success || line.Any(char.IsControl) || !keys.Add(match.Groups[1].Value))
                throw new FormatException("保留的文件头字段无效或重复，无法安全保存。");
            result.Append(line).Append('\n');
        }
        result.Append('\n').Append(document.ScoreText);
        string text = result.ToString();
        if (Encoding.UTF8.GetByteCount(text) > ScoreDocumentReader.MaxBytes) throw new FormatException("保存后的文件不得超过1MB。");
        return text;
    }

    public static async Task SaveAsync(string path, ScoreDocument document, string gap)
    {
        string target = Path.GetFullPath(path);
        if (document.IsLegacy && document.SourcePath != null &&
            string.Equals(target, Path.GetFullPath(document.SourcePath), StringComparison.OrdinalIgnoreCase))
            throw new IOException("旧谱首次保存必须选择不同文件名，以保留原文件。");
        string text = Serialize(document, gap);
        string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, text, new UTF8Encoding(false));
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
