using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HarmonicaPlayer;

public interface IToneOutput
{
    void Start(ScoreNote note);
    void Stop();
}

public sealed class ToneOutput : IToneOutput
{
    private const uint Async = 0x0001;
    private const uint NoDefault = 0x0002;
    private const uint Memory = 0x0004;
    private const uint Loop = 0x0008;
    private readonly object gate = new();
    private GCHandle pinnedWave;
    private bool wavePinned;

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(IntPtr sound, IntPtr module, uint flags);

    public void Start(ScoreNote note)
    {
        if (note.Degree is < 1 or > 7) return;
        byte[] wave = CreateWave(Frequency(note));
        lock (gate)
        {
            StopCore();
            pinnedWave = GCHandle.Alloc(wave, GCHandleType.Pinned);
            wavePinned = true;
            if (!PlaySound(pinnedWave.AddrOfPinnedObject(), IntPtr.Zero, Async | NoDefault | Memory | Loop))
            {
                StopCore();
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法播放测试音效。");
            }
        }
    }

    public void Stop()
    {
        lock (gate) StopCore();
    }

    private void StopCore()
    {
        PlaySound(IntPtr.Zero, IntPtr.Zero, 0);
        if (!wavePinned) return;
        pinnedWave.Free();
        wavePinned = false;
    }

    internal static double Frequency(ScoreNote note)
    {
        int[] semitones = { 0, 2, 4, 5, 7, 9, 11 };
        int midi = 60 + note.Octave * 12 + semitones[note.Degree - 1] + (note.Sharp ? 1 : 0);
        return 440 * Math.Pow(2, (midi - 69) / 12.0);
    }

    private static byte[] CreateWave(double frequency)
    {
        const int sampleRate = 44100;
        int samplesPerCycle = Math.Max(1, (int)Math.Round(sampleRate / frequency));
        int cycles = Math.Max(1, (int)Math.Ceiling(sampleRate * 0.25 / samplesPerCycle));
        int sampleCount = samplesPerCycle * cycles;
        int dataLength = sampleCount * sizeof(short);
        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short)); writer.Write((short)sizeof(short)); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataLength);
        for (int i = 0; i < sampleCount; i++)
            writer.Write((short)(Math.Sin(2 * Math.PI * i / samplesPerCycle) * short.MaxValue * 0.18));
        writer.Flush();
        return stream.ToArray();
    }
}
