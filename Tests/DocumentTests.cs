using HarmonicaPlayer;
using System.Text;

static class DocumentTests
{
    public static async Task Run()
    {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
        void Reject(Action action, string name)
        {
            try { action(); } catch (FormatException) { count++; return; }
            throw new Exception("Expected rejection: " + name);
        }
        string head = "@format=HarmonicaPlayer/1\n@title=测试\n@bpm=100\n";
        var original = new ScoreDocument("测试", 100, "1 2_ 3_ |\r\n0 【【1】】:1.25");
        string serialized = ScoreDocumentWriter.Serialize(original, "20");
        var read = ScoreDocumentReader.Parse(serialized, "测试 bpm120.txt", 90);
        Check(read.Document.Bpm == 100 && read.Document.Title == "测试", "header priority");
        Check(read.Document.ScoreText == original.ScoreText, "body whitespace preserved");
        Check(read.Warnings.Length == 1 && !read.Document.IsLegacy, "filename conflict warning");
        foreach (string name in new[] { "歌 bpm100.txt", "歌_BPM100.txt", "歌-bpm100.txt", "歌 bpm 100.txt" })
            Check(ScoreDocumentReader.Parse("1 2", name, 120).Document.Bpm == 100, "legacy filename " + name);
        var legacy = ScoreDocumentReader.Parse("1\n2", "旧谱.txt", 95);
        Check(legacy.Document.IsLegacy && legacy.Document.Bpm == 95 && legacy.Warnings.Length == 1, "legacy fallback");
        Check(ScoreDocumentReader.Parse("\uFEFF" + serialized, null, 120).Document.Bpm == 100, "BOM");
        Check(ScoreDocumentReader.Parse(serialized.Replace("\n", "\r\n"), null, 120).Document.Bpm == 100, "CRLF header");
        Check(ScoreDocumentReader.Parse("@format=HarmonicaPlayer/1\n@bpm=110\n\n1", "fallback.txt", 120).Document.Title == "fallback", "title fallback");
        var unknown = ScoreDocumentReader.Parse(head + "@author=someone\n@time=4/4\n\n1", null, 120);
        Check(unknown.Warnings.Length == 2 && ScoreDocumentWriter.Serialize(unknown.Document, "20").Contains("@time=4/4"), "unknown fields retained");
        foreach (string bad in new[] {
            head + "@BPM=110\n\n1", head.Replace("/1", "/2") + "\n1",
            head.Replace("@bpm=100\n", "") + "\n1", head + "1",
            head.Replace("100", "0") + "\n1", head.Replace("100", "100.5") + "\n1",
            head.Replace("@format=HarmonicaPlayer/1\n", "") + "\n1",
            head + "@author=a\n@AUTHOR=b\n\n1" })
            Reject(() => ScoreDocumentReader.Parse(bad, null, 120), "bad metadata");
        foreach (string name in new[] { "歌 bpm0.txt", "歌 bpm301.txt", "歌 bpm100.5.txt", "歌 bpm100 bpm120.txt" })
            Reject(() => ScoreDocumentReader.Parse("1", name, 120), "bad filename BPM");
        Check(ScoreDocumentReader.Parse(serialized, "歌 bpm0.txt", 120).Document.Bpm == 100, "header overrides invalid filename");
        Check(ScoreDocumentReader.Parse(serialized, null, 0).Document.Bpm == 100, "valid header does not depend on fallback");
        Reject(() => ScoreDocumentReader.Parse("1", null, 0), "invalid fallback");
        Reject(() => ScoreDocumentWriter.Serialize(original with { Bpm = 301 }, "20"), "invalid save BPM");
        Reject(() => ScoreDocumentWriter.Serialize(original, "5000"), "save shares short-note validation");
        Reject(() => ScoreDocumentWriter.Serialize(original with { Title = "" }, "20"), "empty title");
        Reject(() => ScoreDocumentWriter.Serialize(original with { ScoreText = "8" }, "20"), "invalid score save");
        Reject(() => ScoreDocumentWriter.Serialize(original with { Title = "a\nb" }, "20"), "title injection");
        Reject(() => ScoreDocumentWriter.Serialize(original with { ExtraHeaders = ["@bpm=20"] }, "20"), "duplicate extra header");
        Reject(() => ScoreDocumentReader.Parse(new string('1', ScoreDocumentReader.MaxBytes + 1), null, 120), "size limit");
        var notes = ScoreParser.Parse("1");
        foreach (string gap in new[] { "10", "20", "448" }) Check(PlaybackValidation.ValidateGap(gap, notes, 500) == int.Parse(gap), "valid gap");
        foreach (string gap in new[] { "9", "5001", "", "20.5", "449" }) Reject(() => PlaybackValidation.ValidateGap(gap, notes, 500), "gap rejected");
        Check(PlaybackValidation.ValidateGap("5000", ScoreParser.Parse("0:0.01"), 500) == 5000, "rests exempt");
        Check(PlaybackValidation.ValidateGap("20", notes, 72) == 20, "exact 40ms hold");
        try { PlaybackValidation.ValidateGap("20", ScoreParser.Parse("0 1"), 71); throw new Exception("12ms missing"); }
        catch (ScoreFormatException e) { Check(e.Position == 2, "12ms retained and location preserved"); }
        string directory = Path.Combine(Path.GetTempPath(), "HarmonicaDocument-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string target = Path.Combine(directory, "score.txt");
            await ScoreDocumentWriter.SaveAsync(target, original, "20");
            Check((await ScoreDocumentReader.ReadAsync(target, 120)).Document.ScoreText == original.ScoreText, "disk roundtrip");
            Check(!(await File.ReadAllBytesAsync(target)).Take(3).SequenceEqual(new byte[] {239,187,191}), "UTF8 without BOM");
            string before = await File.ReadAllTextAsync(target);
            try { await ScoreDocumentWriter.SaveAsync(target, original with { ScoreText = "8" }, "20"); throw new Exception("save should fail"); }
            catch (FormatException) { Check(await File.ReadAllTextAsync(target) == before, "invalid save preserves original"); }
            try { await ScoreDocumentWriter.SaveAsync(target, original with { IsLegacy = true, SourcePath = target }, "20"); throw new Exception("overwrite should fail"); }
            catch (IOException) { Check(await File.ReadAllTextAsync(target) == before, "legacy original protected"); }
            await ScoreDocumentWriter.SaveAsync(target, original with { Bpm = 90 }, "20");
            Check((await ScoreDocumentReader.ReadAsync(target, 120)).Document.Bpm == 90, "replace latest version");
            Check(!Directory.GetFiles(directory, "*.tmp").Any(), "no temp left");
            string blocked = Path.Combine(directory, "directory-not-file");
            Directory.CreateDirectory(blocked);
            try { await ScoreDocumentWriter.SaveAsync(blocked, original, "20"); throw new Exception("write should fail"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { Check(Directory.Exists(blocked), "failed replacement preserves target"); }
            finally { Directory.Delete(blocked); }
            Check(!Directory.GetFiles(directory, "*.tmp").Any(), "failure cleans temp");
            string invalid = Path.Combine(directory, "invalid.txt");
            await File.WriteAllBytesAsync(invalid, [0xFF, 0xFE, 0xFF]);
            try { await ScoreDocumentReader.ReadAsync(invalid, 120); throw new Exception("invalid UTF8 accepted"); }
            catch (DecoderFallbackException) { count++; }
        }
        finally { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
        Console.WriteLine($"PASS v0.2.1: {count} document and validation tests");
    }
}
