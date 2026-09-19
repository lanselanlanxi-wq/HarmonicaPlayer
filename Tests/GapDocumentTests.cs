using HarmonicaPlayer;

static class GapDocumentTests
{
    public static async Task Run()
    {
        int passed = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; }
        void Reject(Action action)
        {
            try { action(); } catch (FormatException) { passed++; return; }
            throw new Exception("Invalid gap accepted");
        }
        const string header = "@format=HarmonicaPlayer/1\n@bpm=120\n";
        foreach (string gap in new[] { "10", "20", "35", "5000" })
        {
            var result = ScoreDocumentReader.Parse(header + "@gap=" + gap + "\n\n0", null, 120);
            Check(result.Document.Gap == int.Parse(gap), "gap import");
            Check(result.Warnings.Length == 0 && result.Document.ExtraHeaders!.Length == 0, "gap is known metadata");
        }
        Check(ScoreDocumentReader.Parse(header + "\n1", null, 120).Document.Gap == 20, "new format default");
        Check(ScoreDocumentReader.Parse("1", "old bpm100.txt", 120).Document.Gap == 20, "legacy default");
        Check(ScoreDocumentReader.Parse(header + "@GAP=30\n\n1", null, 120).Document.Gap == 30, "case insensitive");
        foreach (string gap in new[] { "", "0", "9", "5001", "-20", "20.5", "abc" })
            Reject(() => ScoreDocumentReader.Parse(header + "@gap=" + gap + "\n\n1", null, 120));
        Reject(() => ScoreDocumentReader.Parse(header + "@gap=20\n@GAP=30\n\n1", null, 120));
        var doc = new ScoreDocument("测试", 120, "1 2", Gap: 35);
        string saved = ScoreDocumentWriter.Serialize(doc, "35");
        Check(ScoreDocumentReader.Parse(saved, null, 120).Document.Gap == 35, "save/import gap roundtrip");
        Check(ScoreDocumentReader.Parse(ScoreDocumentWriter.Serialize(doc, "25"), null, 120).Document.Gap == 25, "save current edited gap");
        Reject(() => ScoreDocumentWriter.Serialize(doc with { ExtraHeaders = ["@gap=99"] }, "35"));
        Reject(() => ScoreDocumentWriter.Serialize(doc, "5000"));
        string path = Path.Combine(Path.GetTempPath(), "Harmonica-gap-" + Guid.NewGuid() + ".txt");
        try
        {
            await ScoreDocumentWriter.SaveAsync(path, doc, "35");
            Check((await ScoreDocumentReader.ReadAsync(path, 120)).Document.Gap == 35, "disk gap roundtrip");
        }
        finally { File.Delete(path); }
        Console.WriteLine($"PASS gap update: {passed} targeted checks");
    }
}
