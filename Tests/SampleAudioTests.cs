using HarmonicaPlayer;
using System.Text.Json;

static class SampleAudioTests
{
    public static void Run()
    {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception("v0.3.1: " + name); count++; }
        string Note(int midi)
        {
            int[] offsets = { 0, 2, 4, 5, 7, 9, 11 };
            if (midi == 84) return "【【1】】";
            int octave = (midi - 48) / 12 - 1, semitone = (midi - 48) % 12;
            int degree = Array.FindLastIndex(offsets, x => x <= semitone);
            string body = (semitone == offsets[degree] ? "" : "#") + (degree + 1);
            return octave == -1 ? "（" + body + "）" : octave == 1 ? "【" + body + "】" : body;
        }
        for (int midi = 48; midi <= 84; midi++)
        {
            var timeline = ScoreTimeline.Create(Note(midi) + ":8", "120", "20");
            var wave = AudioSynthesis.Render(timeline, 88200, 44100, 100);
            double expected = 440 * Math.Pow(2, (midi - 69) / 12.0);
            double Power(double hz)
            {
                double re = 0, im = 0;
                for (int i = 0; i < wave.Length; i++)
                {
                    double envelope = .5 - .5 * Math.Cos(2 * Math.PI * i / (wave.Length - 1));
                    double phase = 2 * Math.PI * hz * i / 44100;
                    re += wave[i] * envelope * Math.Cos(phase); im += wave[i] * envelope * Math.Sin(phase);
                }
                return re * re + im * im;
            }
            int cents = Enumerable.Range(-20, 41).MaxBy(c => Power(expected * Math.Pow(2,c/1200.0)));
            Check(Math.Abs(cents) <= 10 && wave.Any(x => x != 0), "calibrated pitch " + midi + " cents=" + cents);
        }
        var longNote = ScoreTimeline.Create("1:64", "20", "20");
        var full = AudioSynthesis.Render(longNote, 0, 44100 * 8, 100);
        var parts = new List<short>();
        for (int i = 0; i < full.Length; i += 1379)
            parts.AddRange(AudioSynthesis.Render(longNote,i,Math.Min(1379,full.Length-i),100));
        Check(full.SequenceEqual(parts), "chunk continuity through several loops");
        Check(full.Skip(44100*7).Any(x => x != 0) && AudioSynthesis.Render(longNote,44100*180,4410,100).Any(x => x != 0), "long sustain remains audible");
        Check(AudioSynthesis.Render(longNote, AudioSynthesis.Frame(191980), 882, 100).All(x => x == 0), "long note gap preserved");
        var repeat=ScoreTimeline.Create("1 1", "120", "20");
        Check(AudioSynthesis.Render(repeat,0,20000,100).SequenceEqual(AudioSynthesis.Render(repeat,22050,20000,100)), "repeat notes restart recording");
        Check(AudioSynthesis.Render(longNote,0,2000,0).All(x => x == 0), "sample mute");
        using var manifest = typeof(AudioSynthesis).Assembly.GetManifestResourceStream("HarmonicaSamples.SOURCES.json")!;
        using var doc=JsonDocument.Parse(manifest);
        foreach(var s in doc.RootElement.GetProperty("samples").EnumerateArray())
        {
            int midi=s.GetProperty("midi").GetInt32(), end=s.GetProperty("loopEnd").GetInt32();
            var timeline=ScoreTimeline.Create(Note(midi)+":16","120","20");
            var pcm=AudioSynthesis.Render(timeline,end-100,200,100);
            int boundary=Math.Abs(pcm[100]-pcm[99]);
            int adjacent=Enumerable.Range(1,199).Where(i=>i!=100).Max(i=>Math.Abs(pcm[i]-pcm[i-1]));
            Check(boundary<=adjacent+100,"loop wrap without impulse " + midi);
        }
        Console.WriteLine($"PASS v0.3.1: {count} recorded-sample checks");
    }
}
