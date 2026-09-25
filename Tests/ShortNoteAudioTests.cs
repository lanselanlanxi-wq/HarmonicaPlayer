using HarmonicaPlayer;

static class ShortNoteAudioTests
{
    internal const string Example = "【3】 【2_】 【1】 【2_】 |\n【3_.】 【4__】 【3_】 【2.】 |";
    public static void Run()
    {
        int count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("v0.3.3: " + name); count++; }
        foreach (string bpm in new[] { "90", "120", "180" })
        {
            var timeline = ScoreTimeline.Create(Example, bpm, "20");
            var canonical = ScoreTimeline.Create("【3】 【2】_ 【1】 【2】_ | 【3】_. 【4】__ 【3】_ 【2】. |", bpm, "20");
            Check(timeline.Notes.Select(n => (n.MidiPitch, n.StartMs, n.SoundEndMs)).SequenceEqual(
                canonical.Notes.Select(n => (n.MidiPitch, n.StartMs, n.SoundEndMs))), "inside/outside bracket durations agree");
            int frames = (int)AudioSynthesis.Frame(timeline.DurationMs);
            var pcm = AudioSynthesis.Render(timeline, 0, frames, 100);
            var parts = new List<short>();
            for (int i = 0; i < frames; i += 1764)
                parts.AddRange(AudioSynthesis.Render(timeline, i, Math.Min(1764, frames-i), 100));
            Check(pcm.SequenceEqual(parts), "40ms output buffers match continuous render");
            foreach (var n in timeline.Notes)
            {
                int start = (int)Math.Ceiling(n.StartMs * 44.1);
                int end = (int)Math.Ceiling(n.SoundEndMs * 44.1);
                int gapEnd = Math.Min(frames, (int)Math.Ceiling(n.EndMs * 44.1));
                Check(pcm.Skip(end).Take(gapEnd-end).All(x => x == 0), "gap stays silent");
                Check(Math.Abs((int)pcm[start]) <= 2 && Math.Abs((int)pcm[end-1]) <= 2, "smooth note boundaries");
                if (n.SoundEndMs - n.StartMs <= 250)
                {
                    int from = start + (end-start)/3, length = (end-start)/3;
                    double rms = Math.Sqrt(pcm.Skip(from).Take(length).Average(x => (double)x*x)) / 32768;
                    Check(rms > .06 && rms < .35, "short note has a stable audible body");
                }
            }
        }
        // Repeated notes remain separate and deterministic; no legato merges.
        var repeat = ScoreTimeline.Create("【3】_ 【3】_", "120", "20");
        Check(AudioSynthesis.Render(repeat,0,11025,100).Zip(
            AudioSynthesis.Render(repeat,11025,11025,100), (a,b) => Math.Abs(a-b)).Max() <= 1, "repeated short notes retain identical articulation");
        Console.WriteLine($"PASS v0.3.3: {count} short-note phrase checks");
    }
    public static void WriteExample(string path)
    {
        var timeline = ScoreTimeline.Create(Example, "120", "20");
        short[] pcm = AudioSynthesis.Render(timeline, 0, (int)AudioSynthesis.Frame(timeline.DurationMs), 60);
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+pcm.Length*2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length*2);
        foreach(short sample in pcm) writer.Write(sample);
    }
}
