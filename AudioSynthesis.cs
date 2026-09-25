using System.IO;
using System.Reflection;
using System.Text.Json;

namespace HarmonicaPlayer;

// v0.3.3: real Special 20 recordings from VCSL (CC0), embedded in the EXE.
// The class name is retained for callers; no oscillator fallback is used.
public static class AudioSynthesis
{
    public const int SampleRate = 44100;
    public static long Frame(double ms) => (long)Math.Round(ms * SampleRate / 1000.0);
    private sealed record Sample(int Pitch, short[] Data, int Start, int End, int Crossfade, short[] Release);
    private static readonly Lazy<Sample[]> Bank = new(LoadBank);
    private static Stream Resource(string name) => typeof(AudioSynthesis).Assembly.GetManifestResourceStream("HarmonicaSamples." + name)
        ?? throw new InvalidOperationException("内置口琴采样缺失，请重新下载完整版本。");
    private static Sample[] LoadBank()
    {
        using var manifest = Resource("SOURCES.json");
        using var json = JsonDocument.Parse(manifest);
        var result = new List<Sample>();
        foreach (var entry in json.RootElement.GetProperty("samples").EnumerateArray())
        {
            int pitch = entry.GetProperty("midi").GetInt32();
            short[] data = LoadPcm(pitch + ".wav");
            short[] release = LoadPcm(entry.GetProperty("releaseFile").GetString()!);
            int start = entry.GetProperty("loopStart").GetInt32(), end = entry.GetProperty("loopEnd").GetInt32();
            int cross = entry.GetProperty("crossfade").GetInt32();
            if (data == null || start < 0 || cross <= 0 || start + 2 * cross >= end || end >= data.Length)
                throw new InvalidDataException("口琴采样循环设置无效。");
            if (release.Length < 2) throw new InvalidDataException("口琴尾音采样为空。");
            result.Add(new(pitch, data, start, end, cross, release));
        }
        if (result.Count == 0) throw new InvalidDataException("内置口琴采样为空。");
        return result.OrderBy(s => s.Pitch).ToArray();
    }
    private static short[] LoadPcm(string name)
    {
        using var stream = Resource(name);
        using var reader = new BinaryReader(stream);
        string Four() => new(reader.ReadChars(4));
        if (Four() != "RIFF") throw new InvalidDataException("口琴采样不是WAV。");
        reader.ReadUInt32();
        if (Four() != "WAVE") throw new InvalidDataException("口琴采样格式错误。");
        bool validFormat = false;
        short[]? data = null;
        while (stream.Position + 8 <= stream.Length)
        {
            string tag = Four(); uint length = reader.ReadUInt32();
            long next = stream.Position + length + (length & 1);
            if (next > stream.Length) throw new InvalidDataException("口琴采样文件不完整。");
            if (tag == "fmt ")
            {
                if (length < 16) throw new InvalidDataException("口琴采样格式不完整。");
                ushort format = reader.ReadUInt16(), channels = reader.ReadUInt16();
                uint rate = reader.ReadUInt32(); reader.ReadUInt32();
                ushort align = reader.ReadUInt16(), bits = reader.ReadUInt16();
                validFormat = format == 1 && channels == 1 && rate == SampleRate && align == 2 && bits == 16;
            }
            else if (tag == "data")
            {
                if (!validFormat || length % 2 != 0 || length > 20_000_000) throw new InvalidDataException("口琴采样必须为44.1kHz单声道16位PCM。");
                data = new short[length / 2];
                for (int i = 0; i < data.Length; i++) data[i] = reader.ReadInt16();
            }
            stream.Position = next;
        }
        return data ?? throw new InvalidDataException("口琴采样没有音频数据。");
    }
    private static double Read(Sample s, double position)
    {
        if (position >= s.End)
            position = s.Start + s.Crossfade + (position - s.End) % (s.End - s.Start - s.Crossfade);
        double Linear(double at)
        {
            int i = (int)at; double fraction = at - i;
            return s.Data[i] * (1 - fraction) + s.Data[i + 1] * fraction;
        }
        double value = Linear(position);
        if (position >= s.End - s.Crossfade)
        {
            double amount = (position - (s.End - s.Crossfade)) / s.Crossfade;
            double blend = .5 - .5 * Math.Cos(Math.PI * amount);
            double head = Linear(s.Start + position - (s.End - s.Crossfade));
            value = value * (1 - blend) + head * blend;
        }
        return value;
    }
    internal static (double DurationMs, double CrossfadeMs) ReleaseTiming(double soundDurationMs, double step)
    {
        // Keep the natural speed of the release recording; never stretch it to fill a note.
        // Reserve at most 35% of a short note, and at most 180ms for a long one.
        double duration = Math.Min(Math.Min(180, 180 / step), soundDurationMs * .35);
        return (duration, Math.Min(45, duration * .5));
    }
    // Short notes need a stable tone before they end. Blend continuously back to
    // the recorded attack for longer notes; do not jump at a duration threshold.
    internal static double AttackOffsetMs(double duration) =>
        200 * (1 - Smooth(Math.Clamp((duration - 250) / 250, 0, 1)));
    internal static double ReleaseAmount(double duration) =>
        Smooth(Math.Clamp((duration - 300) / 400, 0, 1));
    private static double Smooth(double amount) => .5 - .5 * Math.Cos(Math.PI * amount);
    public static short[] Render(ScoreTimeline timeline, long firstFrame, int count, int volume)
    {
        if (firstFrame < 0 || count < 0) throw new ArgumentOutOfRangeException();
        var samples = new short[count];
        var bank = Bank.Value;
        double gain = Math.Clamp(volume, 0, 100) / 100.0;
        int index = timeline.IndexAtTime(firstFrame * 1000.0 / SampleRate);
        Sample? selected = null; int lastPitch = -1; double step = 1;
        for (int i = 0; i < count; i++)
        {
            double ms = (firstFrame + i) * 1000.0 / SampleRate;
            while (index + 1 < timeline.Notes.Count && ms >= timeline.Notes[index].EndMs) index++;
            var note = timeline.Notes[index];
            if (note.MidiPitch < 0 || ms < note.StartMs || ms >= note.SoundEndMs) continue;
            if (lastPitch != note.MidiPitch)
            {
                selected = bank.OrderBy(s => Math.Abs(s.Pitch - note.MidiPitch)).ThenByDescending(s => s.Pitch).First();
                step = Math.Pow(2, (note.MidiPitch - selected.Pitch) / 12.0); lastPitch = note.MidiPitch;
            }
            double age = ms - note.StartMs;
            double duration = note.SoundEndMs - note.StartMs;
            double value = Read(selected!, (AttackOffsetMs(duration) + age * step) * SampleRate / 1000);
            var ending = ReleaseTiming(duration, step);
            double releaseAge = age - (duration - ending.DurationMs);
            double releaseAmount = ReleaseAmount(duration);
            if (releaseAge >= 0 && releaseAmount > 0)
            {
                double position = releaseAge * SampleRate / 1000 * step;
                int at = (int)position;
                double tail = at + 1 < selected!.Release.Length
                    ? selected.Release[at] * (1 - (position - at)) + selected.Release[at + 1] * (position - at) : 0;
                double blend = releaseAmount * Smooth(Math.Clamp(releaseAge / ending.CrossfadeMs, 0, 1));
                value = value * (1 - blend) + tail * blend;
            }
            // Smooth derivatives at the handover and the final silence boundary.
            double fade = Math.Min(20 + 15 * releaseAmount, duration * .2);
            double attack = 3 + 9 * (1 - Smooth(Math.Clamp((duration - 250) / 250, 0, 1)));
            double envelope = Smooth(Math.Clamp(age / attack, 0, 1)) *
                Smooth(Math.Clamp((note.SoundEndMs - ms) / fade, 0, 1));
            samples[i] = (short)Math.Clamp(Math.Round(value * envelope * gain), short.MinValue, short.MaxValue);
        }
        return samples;
    }
}
