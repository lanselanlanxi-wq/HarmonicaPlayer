using System.IO;
using System.Text;

namespace HarmonicaPlayer;

public static class MidiExporter
{
    public const int TicksPerQuarter = 9600;
    public static byte[] Encode(ScoreTimeline timeline, string title)
    {
        using var track = new MemoryStream();
        void Variable(long value)
        {
            if (value is < 0 or > 0x0FFFFFFF) throw new FormatException("MIDI事件间隔超出格式范围。");
            Span<byte> bytes = stackalloc byte[4]; int i = 3;
            bytes[i] = (byte)(value & 127);
            while ((value >>= 7) > 0) bytes[--i] = (byte)((value & 127) | 128);
            track.Write(bytes[i..]);
        }
        long previous = 0;
        long Tick(double ms) => (long)Math.Round(ms * timeline.Bpm / 60000.0 * TicksPerQuarter);
        void At(long tick) { Variable(tick - previous); previous = tick; }
        At(0); track.Write(new byte[] { 0xFF, 3 });
        var name = Encoding.UTF8.GetBytes(title); Variable(name.Length); track.Write(name);
        int tempo = (int)Math.Round(60000000.0 / timeline.Bpm);
        At(0); track.Write(new byte[] { 0xFF, 0x51, 3, (byte)(tempo >> 16), (byte)(tempo >> 8), (byte)tempo });
        At(0); track.Write(new byte[] { 0xC0, 21 }); // GM harmonica (one-based 22).
        foreach (var note in timeline.Notes)
        {
            if (note.MidiPitch < 0) continue;
            long on = Tick(note.StartMs), off = Math.Max(on + 1, Tick(note.SoundEndMs));
            At(on); track.Write(new byte[] { 0x90, (byte)note.MidiPitch, 96 });
            At(off); track.Write(new byte[] { 0x80, (byte)note.MidiPitch, 0 });
        }
        At(Tick(timeline.DurationMs)); track.Write(new byte[] { 0xFF, 0x2F, 0 });
        using var file = new MemoryStream();
        file.Write(Encoding.ASCII.GetBytes("MThd"));
        Write32(file, 6); file.Write(new byte[] { 0, 0, 0, 1, (byte)(TicksPerQuarter >> 8), (byte)(TicksPerQuarter & 255) });
        file.Write(Encoding.ASCII.GetBytes("MTrk")); Write32(file, checked((int)track.Length));
        track.Position = 0; track.CopyTo(file); return file.ToArray();
    }
    private static void Write32(Stream stream, int value) => stream.Write(new byte[]
        { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
    public static async Task SaveAsync(string path, ScoreTimeline timeline, string title)
    {
        byte[] bytes = Encode(timeline, title);
        string fullPath = Path.GetFullPath(path);
        string temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temp, bytes); File.Move(temp, fullPath, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
