using HarmonicaPlayer;

static class ReleaseAudioTests
{
    public static void Run()
    {
        int count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception("v0.3.2: " + message); count++; }
        foreach(string note in new[]{"（1）", "1", "3", "【1】", "【3】", "【5】", "【【1】】"})
        foreach(string duration in new[]{"0.15", "1", "8"})
        {
            var timeline=ScoreTimeline.Create(note+":"+duration+" 0_", "120", "20");
            int end=(int)AudioSynthesis.Frame(timeline.Notes[0].SoundEndMs);
            var pcm=AudioSynthesis.Render(timeline,0,(int)AudioSynthesis.Frame(timeline.DurationMs),100);
            Check(pcm.Skip(end).All(x=>x==0), "release preserves gap and rest " + note + ":"+duration);
            Check(Math.Abs((int)pcm[end-1])<=2, "tail reaches silence smoothly " + note + ":"+duration);
            int first=Math.Max(0,end-5000), length=end-first;
            var partition=AudioSynthesis.Render(timeline,first,111,100).Concat(AudioSynthesis.Render(timeline,first+111,length-111,100));
            Check(pcm.Skip(first).Take(length).SequenceEqual(partition),"crossfade independent of buffer split");
            Check(pcm.Max(x=>Math.Abs((int)x))<32767,"no clipping");
        }
        var longNote=ScoreTimeline.Create("1:8", "120", "20");
        var shortened=ScoreTimeline.Create("1", "120", "20");
        Check(AudioSynthesis.Render(longNote,0,4410,100).SequenceEqual(AudioSynthesis.Render(ScoreTimeline.Create("1:2", "120", "20"),0,4410,100)),"unchanged attack/body");
        Check(!AudioSynthesis.Render(longNote,18000,1000,100).SequenceEqual(AudioSynthesis.Render(shortened,18000,1000,100)),"release audible before note end");
        var firstTail=AudioSynthesis.Render(shortened,AudioSynthesis.Frame(380),1764,100);
        var lastTail=AudioSynthesis.Render(shortened,AudioSynthesis.Frame(460),882,100);
        double Rms(short[] p)=>Math.Sqrt(p.Select(x=>(double)x*x).Average());
        Check(Rms(lastTail)<Rms(firstTail),"tail energy decays");
        Check(AudioSynthesis.Render(shortened,0,22050,0).All(x=>x==0),"release obeys volume zero");
        Console.WriteLine($"PASS v0.3.2: {count} release/gap/buffer checks");
    }
}
