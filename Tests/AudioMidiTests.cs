using HarmonicaPlayer;
using System.Text;

static class AudioMidiTests
{
    public static async Task Run()
    {
        int passed = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception("v0.3.0: " + name); passed++; }
        var t = ScoreTimeline.Create("（1） 1 【1】 【【1】】 #1 【#1】 0", "120", "20");
        Check(t.Notes.Select(n => n.MidiPitch).SequenceEqual(new[] { 48, 60, 72, 84, 61, 73, -1 }), "C3/C4/C5/C6 and sharps");
        Check(t.DurationMs == 3500 && t.Notes[0].SoundEndMs == 480 && t.Notes[1].StartMs == 500, "gap inside beat");
        var rhythms = ScoreTimeline.Create("1_ 2__ 3. 4:1.25 0:0.25 5 —", "100", "20");
        Check(rhythms.Notes.Select(n => n.EndMs - n.StartMs).SequenceEqual(new double[] {300,150,900,750,150,1200}), "all durations");
        string body = "1 | 【#2】:1.25 0";
        var cursor = ScoreTimeline.Create(body, "120", "20");
        Check(cursor.IndexAtCursor(body, 0) == 0, "cursor first");
        Check(cursor.IndexAtCursor(body, 2) == 1 && cursor.IndexAtCursor(body, 4) == 1 && cursor.IndexAtCursor(body, 5) == 1, "cursor bar/bracket/sharp selects next");
        Check(cursor.IndexAtCursor(body, 7) == 1 && cursor.IndexAtCursor(body, 10) == 1, "cursor duration selects note");
        Check(cursor.IndexAtCursor(body, body.Length) == 2, "cursor EOF");
        Check(cursor.IndexAtTime(499) == 0 && cursor.IndexAtTime(500) == 1, "time boundary");
        bool rejected = false;
        try { ScoreTimeline.Create("1__", "300", "20"); } catch (ScoreFormatException) { rejected = true; }
        Check(rejected, "shared minimum duration validation");
        var pcm = AudioSynthesis.Render(t, 0, 44100, 100);
        Check(pcm.Any(x => x != 0) && pcm.All(x => Math.Abs((int)x) < short.MaxValue), "sound and no clipping");
        Check(AudioSynthesis.Render(t, AudioSynthesis.Frame(480), 882, 100).All(x => x == 0), "gap silent");
        Check(AudioSynthesis.Render(t, AudioSynthesis.Frame(3000), 22050, 100).All(x => x == 0), "rest silent");
        Check(AudioSynthesis.Render(t, 0, 10000, 0).All(x => x == 0), "volume zero");
        var whole = AudioSynthesis.Render(t, 0, 6000, 60);
        var split = AudioSynthesis.Render(t, 0, 1764, 60).Concat(AudioSynthesis.Render(t, 1764, 4236, 60));
        Check(whole.SequenceEqual(split), "streaming has no boundary discontinuity");
        var a4 = ScoreTimeline.Create("6", "120", "20");
        var wave = AudioSynthesis.Render(a4, 441, 17640, 100);
        double Power(double hz)
        {
            double re = 0, im = 0;
            for (int i = 0; i < wave.Length; i++)
            { double phase = 2 * Math.PI * hz * i / 44100; re += wave[i] * Math.Cos(phase); im += wave[i] * Math.Sin(phase); }
            return re * re + im * im;
        }
        double strongest = Enumerable.Range(430, 21).MaxBy(hz => Power(hz));
        Check(Math.Abs(strongest - 440) <= 2, "A4 sample pitch calibrated");

        // Independently decode bytes as a Standard MIDI File, not exporter helpers.
        var bytes = MidiExporter.Encode(t, "测试曲");
        int Int32(int at) => (bytes[at] << 24) | (bytes[at+1] << 16) | (bytes[at+2] << 8) | bytes[at+3];
        Check(Encoding.ASCII.GetString(bytes, 0, 4) == "MThd" && Int32(4) == 6 && bytes[9] == 0 && bytes[11] == 1, "SMF type 0 one track");
        int ppq = bytes[12] * 256 + bytes[13];
        Check(ppq == 9600 && Encoding.ASCII.GetString(bytes,14,4) == "MTrk" && Int32(18) == bytes.Length - 22, "chunk lengths/division");
        var events = new List<(long Tick, int Status, int A, int B)>();
        int pos = 22; long time = 0, endTick = -1; int tempo = 0; string name = "";
        int Var() { int n=0,b; do { b=bytes[pos++]; n=(n<<7)|(b&127); } while((b&128)!=0); return n; }
        while(pos < bytes.Length)
        {
            time += Var(); int status = bytes[pos++];
            if(status == 255)
            {
                int type = bytes[pos++], len = Var();
                if(type == 3) name = Encoding.UTF8.GetString(bytes,pos,len);
                if(type == 0x51) tempo = (bytes[pos]<<16)|(bytes[pos+1]<<8)|bytes[pos+2];
                if(type == 0x2F) endTick = time;
                pos += len;
            }
            else { int a=bytes[pos++], b=status == 0xC0 ? 0 : bytes[pos++]; events.Add((time,status,a,b)); }
        }
        Check(name == "测试曲" && tempo == 500000, "title and tempo metadata");
        Check(events[0].Status == 0xC0 && events[0].A == 21, "GM harmonica");
        Check(events.Count(e => e.Status == 0x90) == 6 && events.Count(e => e.Status == 0x80) == 6, "balanced on/off excluding rest");
        Check(events.Where(e => e.Status == 0x90).Select(e => e.A).SequenceEqual(new[] {48,60,72,84,61,73}), "MIDI pitch mapping");
        Check(events.First(e => e.Status == 0x80).Tick == 9216 && events.Where(e => e.Status == 0x90).Skip(1).First().Tick == 9600, "MIDI gap exact at 120 BPM");
        Check(endTick == 7 * 9600, "trailing rest retained");
        var rests = MidiExporter.Encode(ScoreTimeline.Create("0 —", "120", "20"), "休止");
        Check(rests.Length > 30, "all rest file encodable");
        // Fractional beats and non-integer tempo: rounded absolute positions avoid drift.
        var longScore = ScoreTimeline.Create(string.Join(' ', Enumerable.Repeat("1:0.333", 2000)), "137", "20");
        Check(Math.Abs(longScore.DurationMs - 666 * 60000.0 / 137) < .00001, "long score cumulative precision");
        Check(MidiExporter.Encode(longScore,"长谱").Length > 10000, "long score encoding");
        var defaults = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"Bpm\":120,\"Gap\":20}")!;
        Check(defaults.PreviewVolume == 60, "old settings default volume");
        try { (defaults with { PreviewVolume = 101 }).Validate(); Check(false,"volume validation"); }
        catch (FormatException) { Check(true,"volume validation"); }
        string root = Path.Combine(Path.GetTempPath(), "HarmonicaMidi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root,"score.mid");
            await File.WriteAllTextAsync(path,"old");
            await MidiExporter.SaveAsync(path,t,"测试曲");
            Check(File.ReadAllBytes(path).SequenceEqual(bytes), "atomic MIDI replacement");
            string directory = Path.Combine(root,"target.mid"); Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory,"keep"),"keep");
            bool failed=false;
            try { await MidiExporter.SaveAsync(directory,t,"测试曲"); }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException) { failed=true; }
            Check(failed && File.ReadAllText(Path.Combine(directory,"keep")) == "keep" && !Directory.GetFiles(root,"*.tmp").Any(), "failed export preserves target and cleans temporary");
        }
        finally { Directory.Delete(root,true); }
        Console.WriteLine($"PASS v0.3.0: {passed} audio/timeline/MIDI tests");
    }
}
