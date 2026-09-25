using System.Runtime.InteropServices;

namespace HarmonicaPlayer;

public interface ILocalAudioPlayer
{
    int Volume { get; set; }
    Task PlayAsync(ScoreTimeline timeline, int startIndex, Action<double> progress, CancellationToken token);
}

// WinMM PCM output, three reusable 40ms buffers. No key/mouse APIs, disk cache,
// external synthesizer, installed codec, or administrator rights required.
public sealed class LocalAudioPlayer : ILocalAudioPlayer
{
    private int volume = 60;
    public int Volume { get => Volatile.Read(ref volume); set => Volatile.Write(ref volume, Math.Clamp(value, 0, 100)); }
    public Task PlayAsync(ScoreTimeline timeline, int startIndex, Action<double> progress, CancellationToken token)
        => Task.Run(() => Play(timeline, startIndex, progress, token), token);
    private void Play(ScoreTimeline timeline, int startIndex, Action<double> progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var format = new WaveFormat { FormatTag = 1, Channels = 1, SamplesPerSec = AudioSynthesis.SampleRate,
            AvgBytesPerSec = AudioSynthesis.SampleRate * 2, BlockAlign = 2, BitsPerSample = 16 };
        Check(waveOutOpen(out var device, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0), "打开音频设备");
        var blocks = new List<Block>();
        try
        {
            Check(waveOutPause(device), "初始化试听");
            for (int i = 0; i < 3; i++) blocks.Add(new Block());
            long next = AudioSynthesis.Frame(timeline.Notes[startIndex].StartMs);
            long end = AudioSynthesis.Frame(timeline.DurationMs);
            void Queue(Block block)
            {
                if (next >= end) return;
                int count = (int)Math.Min(Block.Capacity, end - next);
                short[] pcm = AudioSynthesis.Render(timeline, next, count, Volume);
                Marshal.Copy(pcm, 0, block.Data, count);
                var header = new WaveHeader { Data = block.Data, BufferLength = (uint)(count * 2) };
                Marshal.StructureToPtr(header, block.Header, false);
                Check(waveOutPrepareHeader(device, block.Header, HeaderSize), "准备音频缓冲");
                block.Prepared = true;
                Check(waveOutWrite(device, block.Header, HeaderSize), "输出音频");
                block.Active = true; next += count; block.EndFrame = next;
            }
            foreach (var block in blocks) Queue(block);
            token.ThrowIfCancellationRequested();
            progress(timeline.Notes[startIndex].StartMs);
            Check(waveOutRestart(device), "开始试听");
            // Read completed buffers in submission order, so progress never jumps
            // backwards even when Windows completes several at once.
            int current = 0;
            var stalled = System.Diagnostics.Stopwatch.StartNew();
            while (blocks.Any(b => b.Active))
            {
                token.ThrowIfCancellationRequested();
                var block = blocks[current];
                if (!block.Active) { current = (current + 1) % blocks.Count; continue; }
                var header = Marshal.PtrToStructure<WaveHeader>(block.Header);
                if ((header.Flags & 1) == 0)
                {
                    if (stalled.Elapsed.TotalSeconds > 5) throw new InvalidOperationException("音频设备无响应，请检查输出设备后重试。");
                    token.WaitHandle.WaitOne(5); continue;
                }
                stalled.Restart();
                progress(Math.Min(timeline.DurationMs, block.EndFrame * 1000.0 / AudioSynthesis.SampleRate));
                Check(waveOutUnprepareHeader(device, block.Header, HeaderSize), "释放音频缓冲");
                block.Prepared = false; block.Active = false;
                Queue(block); current = (current + 1) % blocks.Count;
            }
        }
        finally
        {
            // Reset synchronously returns queued buffers before freeing unmanaged memory.
            uint cleanupError = waveOutReset(device);
            foreach (var block in blocks)
            {
                if (!block.Prepared) continue;
                uint result = waveOutUnprepareHeader(device, block.Header, HeaderSize);
                if (result == 0) block.Prepared = false;
                else if (cleanupError == 0) cleanupError = result;
            }
            uint closeError = waveOutClose(device);
            foreach (var block in blocks)
            {
                // A broken driver must never retain pointers to freed buffers.
                // If both unprepare and close fail, retain that block until process exit.
                if (!block.Prepared || closeError == 0) block.Dispose();
            }
            Check(cleanupError != 0 ? cleanupError : closeError, "关闭音频设备");
        }
    }
    private static void Check(uint result, string action)
    {
        if (result != 0) throw new InvalidOperationException($"{action}失败（音频错误 {result}）。请检查扬声器、耳机或默认输出设备后重试。");
    }
    private sealed class Block : IDisposable
    {
        public const int Capacity = AudioSynthesis.SampleRate / 25;
        public IntPtr Data { get; } = Marshal.AllocHGlobal(Capacity * 2);
        public IntPtr Header { get; } = Marshal.AllocHGlobal((int)HeaderSize);
        public bool Prepared, Active;
        public long EndFrame;
        public void Dispose() { Marshal.FreeHGlobal(Header); Marshal.FreeHGlobal(Data); }
    }
    private static readonly uint HeaderSize = (uint)Marshal.SizeOf<WaveHeader>();
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSec, AvgBytesPerSec;
        public ushort BlockAlign, BitsPerSample, ExtraSize;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength, BytesRecorded;
        public UIntPtr User;
        public uint Flags, Loops;
        public IntPtr Next;
        public UIntPtr Reserved;
    }
    [DllImport("winmm.dll")] private static extern uint waveOutOpen(out IntPtr handle, uint device, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutPause(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint waveOutRestart(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr handle);
}
