using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HarmonicaPlayer;

public sealed partial class PlayerWindow
{
    private readonly Button listen = new() { Content = "从头试听", Margin = new Thickness(5), Padding = new Thickness(10, 6, 10, 6) };
    private readonly Button listenFromCursor = new() { Content = "从光标试听", Margin = new Thickness(5), Padding = new Thickness(10, 6, 10, 6) };
    private readonly Button exportMidi = new() { Content = "导出 MIDI", Margin = new Thickness(5), Padding = new Thickness(10, 6, 10, 6) };
    private readonly Slider volumeSlider = new() { Minimum = 0, Maximum = 100, Value = 60, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 130 };
    private readonly TextBlock volumeLabel = new() { Text = "60%", Width = 45 };
    private readonly ProgressBar audioProgress = new() { Minimum = 0, Maximum = 100, Height = 8, Margin = new Thickness(5) };
    private readonly TextBlock audioPosition = new() { Text = "本地试听：Special 20 真实口琴采样，不发送游戏按键。", TextWrapping = TextWrapping.Wrap };
    private readonly ILocalAudioPlayer localAudio;
    private bool listening;
    private void AddListeningControls(Panel panel)
    {
        var row = new WrapPanel();
        row.Children.Add(listen); row.Children.Add(listenFromCursor); row.Children.Add(exportMidi);
        row.Children.Add(new TextBlock { Text = "试听音量：", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(volumeSlider); row.Children.Add(volumeLabel);
        panel.Children.Add(row); panel.Children.Add(audioProgress); panel.Children.Add(audioPosition);
        score.IsInactiveSelectionHighlightEnabled = true;
        score.SelectionBrush = Brushes.Gold; score.SelectionOpacity = .6;
        listen.Click += async (_, _) => await ListenAsync(false);
        listenFromCursor.Click += async (_, _) => await ListenAsync(true);
        exportMidi.Click += async (_, _) => await ExportMidiAsync();
        volumeSlider.ValueChanged += (_, _) =>
        {
            localAudio.Volume = (int)volumeSlider.Value;
            volumeLabel.Text = $"{localAudio.Volume}%"; QueueSave();
        };
    }
    private async Task ListenAsync(bool fromCursor)
    {
        if (cancellation != null || closing || beginning || editingHotkeys || documentBusy) return;
        int selectionStart = score.SelectionStart, selectionLength = score.SelectionLength;
        beginning = true; listening = true; SetBusy(true);
        operationIssue = null; UpdateAlert();
        try
        {
            string body = score.Text;
            var timeline = ScoreTimeline.Create(body, bpm.Text, gap.Text);
            int index = fromCursor ? timeline.IndexAtCursor(body, selectionStart) : 0;
            double offset = timeline.Notes[index].StartMs;
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            scoreIssue = null; errorPosition = null; UpdateAlert();
            previewTimer.Stop();
            status.Text = "本地试听中；点击停止或按停止快捷键结束。";
            void Progress(double ms)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (closing || token.IsCancellationRequested || !listening) return;
                    int current = timeline.IndexAtTime(ms);
                    var note = timeline.Notes[current];
                    score.Select(note.Note.Position, 1);
                    score.ScrollToLine(score.GetLineIndexFromCharacterIndex(note.Note.Position));
                    audioProgress.Value = Math.Clamp((ms - offset) / (timeline.DurationMs - offset) * 100, 0, 100);
                    audioPosition.Text = $"{current + 1}/{timeline.Notes.Count}：{note.Note.Label} · 已试听 {Time(ms - offset)} / {Time(timeline.DurationMs - offset)} · 剩余 {Time(Math.Max(0, timeline.DurationMs - ms))}";
                }));
            }
            Progress(offset);
            running = localAudio.PlayAsync(timeline, index, Progress, token);
            await running;
            if (!closing)
            {
                audioProgress.Value = 100;
                audioPosition.Text = $"试听完成 · 本段 {Time(timeline.DurationMs - offset)}";
                status.Text = "试听完成，可继续编辑或导出 MIDI。";
            }
        }
        catch (OperationCanceledException) { if (!closing) status.Text = "试听已停止，可继续编辑。"; }
        catch (FormatException) { cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null; Preview(); status.Text = "无法试听，请查看顶部提示。"; }
        catch (Exception e) { if (!closing) { status.Text = "试听失败，可修正后重试。"; ReportIssue(e.Message); } }
        finally
        {
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            running = null; beginning = false; listening = false;
            score.Select(Math.Min(selectionStart, score.Text.Length), Math.Min(selectionLength, score.Text.Length - Math.Min(selectionStart, score.Text.Length)));
            SetBusy(false);
        }
    }
    private static string Time(double ms) => TimeSpan.FromMilliseconds(ms).TotalHours >= 1
        ? $"{(int)TimeSpan.FromMilliseconds(ms).TotalHours}:{TimeSpan.FromMilliseconds(ms):mm\\:ss}"
        : TimeSpan.FromMilliseconds(ms).ToString(@"mm\:ss");
    private Task ExportMidiAsync() => DocumentOperation(async () =>
    {
        try
        {
            var timeline = ScoreTimeline.Create(score.Text, bpm.Text, gap.Text);
            string title = songTitle.Text;
            string name = string.Concat(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string? path = scoreDialogs.SaveMidi(this, name + ".mid");
            if (path == null) return false;
            await MidiExporter.SaveAsync(path, timeline, title);
            operationIssue = null; UpdateAlert();
            status.Text = "已导出整首曲谱为单音轨 MIDI（含BPM、休止和间隔）；TXT保存状态不变。";
            return true;
        }
        catch (FormatException) { Preview(); status.Text = "无法导出，请查看顶部提示。"; return false; }
    });
}
