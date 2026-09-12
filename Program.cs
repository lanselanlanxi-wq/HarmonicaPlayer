using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HarmonicaPlayer;

public static class Program
{
    [STAThread]
    public static void Main() => SingleInstance.Run();
}

public sealed class PlayerWindow : Window
{
    private readonly TextBox score = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 230, Text = "1234567 【1234567】 （1234567） #1 #2 #4 #5 #6 111 000" };
    private readonly TextBox duration = new() { Text = "300", Width = 75 };
    private readonly TextBox gap = new() { Text = "20", Width = 75 };
    private readonly TextBox spaceGap = new() { Text = "100", Width = 75 };
    private readonly TextBox lineGap = new() { Text = "300", Width = 75 };
    private readonly CheckBox dry = new() { Content = "仅日志测试（不发送键鼠、不发声）", IsChecked = true, Margin = new Thickness(0, 10, 0, 10) };
    private readonly TextBlock status = new() { Text = "就绪：先导入谱面，快捷键状态见上方。", TextWrapping = TextWrapping.Wrap };
    private readonly TextBox log = new() { IsReadOnly = true, Height = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button start = new() { Content = "开始 F6", Margin = new Thickness(5), Padding = new Thickness(15, 6, 15, 6) };
    private readonly Button import = new() { Content = "导入 TXT", Margin = new Thickness(5), Padding = new Thickness(15, 6, 15, 6) };
    private readonly CheckBox rhythm = new() { Content = "节奏模式（按拍数播放；忽略空格/换行额外停顿）", IsChecked = false };
    private readonly TextBox bpm = new() { Text = "120", Width = 75 };
    private readonly TextBox preview = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button refresh = new() { Content = "预览 / 检查", Margin = new Thickness(5), Padding = new Thickness(12, 6, 12, 6) };
    private CancellationTokenSource? cancellation;
    private Task? running;
    public bool IsClosing => closing;
    private SettingsWriter settingsWriter = null!;
    private readonly string settingsPath;
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly NativeInput output = new();
    private HwndSource? source;
    private IntPtr hwnd;
    private bool allowClose, closing, editingHotkeys, beginning;
    private HotkeyController? hotkeys;
    private AppSettings settings = new();
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly TextBlock settingsStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock hotkeyStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
    private readonly Button configure = new() { Content = "自定义快捷键", Margin = new Thickness(5), Padding = new Thickness(10, 6, 10, 6) };
    private readonly Button retry = new() { Content = "重试注册", Margin = new Thickness(5), Padding = new Thickness(10, 6, 10, 6) };
    private readonly Button stop = new() { Content = "停止 F8", Margin = new Thickness(5), Padding = new Thickness(15, 6, 15, 6) };

    public PlayerWindow(string? settingsFile = null)
    {
        settingsPath = settingsFile ?? SettingsStore.DefaultPath;
        Title = "口琴简谱播放器 0.1.5"; Width = 740; Height = 900; MinWidth = 600; MinHeight = 600;
        var panel = new StackPanel { Margin = new Thickness(18) };
        Content = new ScrollViewer { Content = panel };
        panel.Children.Add(new TextBlock { Text = "TXT → 单音口琴演奏", FontSize = 23 });
        panel.Children.Add(new TextBlock { Text = "【高音】 （低音） #升半音；0休止，-或—延长一拍，_半拍，__四分之一拍，.附点。例：1 2_ 3_ 5 — | 0 6. 5_ 1 |", Margin = new Thickness(0, 10, 0, 10), TextWrapping = TextWrapping.Wrap });
        var controls = new WrapPanel { Orientation = Orientation.Horizontal };
        controls.Children.Add(import); controls.Children.Add(refresh); controls.Children.Add(start);
        controls.Children.Add(stop); panel.Children.Add(controls);
        var shortcuts = new StackPanel { Orientation = Orientation.Horizontal };
        shortcuts.Children.Add(configure); shortcuts.Children.Add(retry); panel.Children.Add(shortcuts);
        panel.Children.Add(hotkeyStatus); panel.Children.Add(settingsStatus); panel.Children.Add(score);
        panel.Children.Add(rhythm);
        var tempo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        tempo.Children.Add(new TextBlock { Text = "速度 ♩ / 分钟：", VerticalAlignment = VerticalAlignment.Center });
        tempo.Children.Add(bpm); panel.Children.Add(tempo);
        panel.Children.Add(new TextBlock { Text = "解析预览（每个音的拍数和时间；不是图片识谱）", Margin = new Thickness(0, 8, 0, 4) });
        panel.Children.Add(preview);
        var timing = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        timing.Children.Add(new TextBlock { Text = "旧模式每音(ms)：", VerticalAlignment = VerticalAlignment.Center }); timing.Children.Add(duration);
        timing.Children.Add(new TextBlock { Text = "  其中留白(ms)：", VerticalAlignment = VerticalAlignment.Center }); timing.Children.Add(gap);
        panel.Children.Add(timing);
        var pauses = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        pauses.Children.Add(new TextBlock { Text = "空格额外停顿(ms)：", VerticalAlignment = VerticalAlignment.Center }); pauses.Children.Add(spaceGap);
        pauses.Children.Add(new TextBlock { Text = "  换行额外停顿(ms)：", VerticalAlignment = VerticalAlignment.Center }); pauses.Children.Add(lineGap);
        panel.Children.Add(pauses); panel.Children.Add(dry);
        panel.Children.Add(new TextBlock
        {
            Text = "实际演奏请取消“仅日志测试”。开始后直接倒计时3秒，不再弹窗；请保持游戏口琴界面在前台，除停止键外不要操作鼠标键盘。",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(status); panel.Children.Add(log);
        settings = SettingsStore.Load(settingsPath, out var loadWarning);
        settingsWriter = new SettingsWriter(value => SettingsStore.Save(settingsPath, value),
            loadWarning == null && File.Exists(settingsPath) ? settings : null);
        rhythm.IsChecked = settings.Rhythm; bpm.Text = settings.Bpm.ToString();
        duration.Text = settings.Duration.ToString(); gap.Text = settings.Gap.ToString();
        spaceGap.Text = settings.SpaceGap.ToString(); lineGap.Text = settings.LineGap.ToString();
        settingsStatus.Text = loadWarning ?? "设置会自动保存；每次启动默认开启“仅日志测试”。";
        saveTimer.Tick += async (_, _) => { saveTimer.Stop(); await SaveCurrentSettingsAsync(); };
        previewTimer.Tick += (_, _) => { previewTimer.Stop(); Preview(); };
        configure.Click += (_, _) => ConfigureHotkeys();
        retry.Click += (_, _) => ApplyHotkeys();
        import.Click += (_, _) => Import(); start.Click += async (_, _) => await Begin();
        stop.Click += (_, _) => Stop();
        refresh.Click += (_, _) => Preview();
        rhythm.Checked += (_, _) => { SetBusy(false); Preview(); QueueSave(); };
        rhythm.Unchecked += (_, _) => { SetBusy(false); Preview(); QueueSave(); };
        score.TextChanged += (_, _) => QueuePreview();
        bpm.TextChanged += (_, _) => QueuePreview();
        duration.TextChanged += (_, _) => QueuePreview();
        spaceGap.TextChanged += (_, _) => QueuePreview();
        lineGap.TextChanged += (_, _) => QueuePreview();
        foreach (var box in new[] { bpm, duration, gap, spaceGap, lineGap })
            box.TextChanged += (_, _) => QueueSave();
        SetBusy(false); Preview();
        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(hwnd); source.AddHook(Hook);
            hotkeys = new HotkeyController(new NativeHotkeys(hwnd));
            ApplyHotkeys();
        };
        Closing += (_, e) =>
        {
            if (allowClose) return;
            e.Cancel = true;
            if (closing) return;
            closing = true;
            saveTimer.Stop(); previewTimer.Stop();
            cancellation?.Cancel(); // Cancel first; do not put disk I/O before it.
            SetBusy(true); stop.IsEnabled = false;
            status.Text = "正在停止演奏并关闭…";
            // Always leave the initial Closing event before calling Close again,
            // even when there is no playback task or every await completes inline.
            Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                new Action(() => _ = CloseAfterStopAsync()));
        };
    }
    private async Task CloseAfterStopAsync()
    {
        try
        {
            Task? playback = running;
            if (playback != null)
            {
                if (await Task.WhenAny(playback, Task.Delay(1500)) != playback)
                    status.Text = "正在等待演奏任务停止，窗口仍可响应…";
                try { await playback; }
                catch (Exception e) { status.Text = "演奏结束异常：" + e.Message; }
            }
            // No background note sender remains before final cleanup begins.
            string? releaseError = await Task.Run(output.Release);
            if (releaseError != null)
            {
                // Do not silently exit with keys still held. A second close retries.
                status.Text = "按键释放失败，请手动按下并松开相关键后再关闭：" + releaseError;
                return;
            }
            Task save = SaveCurrentSettingsAsync(true);
            if (await Task.WhenAny(save, Task.Delay(1500)) != save)
                status.Text = "正在保存最后的设置，请稍候（窗口仍可响应）…";
            await save;
            hotkeys?.Suspend(); source?.RemoveHook(Hook);
            allowClose = true; Close();
        }
        catch (Exception e) { status.Text = "关闭未完成，请重试：" + e.Message; }
        finally
        {
            if (!allowClose)
            {
                closing = false; stop.IsEnabled = true; SetBusy(false);
            }
        }
    }
    private void QueuePreview()
    {
        if (closing) return;
        previewTimer.Stop(); previewTimer.Start();
    }

    private IntPtr Hook(IntPtr h, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (message == 0x0312)
        {
            // Discard queued messages from old registrations after editing.
            uint packed = unchecked((uint)l.ToInt64());
            uint key = packed >> 16, modifiers = packed & 0xFFFF;
            if (!editingHotkeys && w.ToInt32() == 1 && hotkeys?.StartReady == true &&
                key == settings.Start.Key && modifiers == settings.Start.Modifiers) _ = Begin(key);
            if (!editingHotkeys && w.ToInt32() == 2 && hotkeys?.StopReady == true &&
                key == settings.Stop.Key && modifiers == settings.Stop.Modifiers) Stop();
            handled = true;
        }
        return IntPtr.Zero;
    }
    private void QueueSave()
    {
        if (closing) return;
        saveTimer.Stop(); saveTimer.Start();
    }
    private async Task SaveCurrentSettingsAsync(bool preserveValidTiming = false)
    {
        AppSettings next = settings;
        string? invalid = null;
        if (!int.TryParse(bpm.Text, out int tempo) || !int.TryParse(duration.Text, out int ms) ||
            !int.TryParse(gap.Text, out int silence) || !int.TryParse(spaceGap.Text, out int space) ||
            !int.TryParse(lineGap.Text, out int line))
            invalid = "部分数值尚未填写完整，保留上次有效设置。";
        else
        {
            var candidate = settings with { Rhythm = rhythm.IsChecked == true, Bpm = tempo, Duration = ms,
                Gap = silence, SpaceGap = space, LineGap = line };
            try { candidate.Validate(); next = candidate; }
            catch (FormatException e) { invalid = e.Message; }
        }
        if (invalid != null && !preserveValidTiming)
        { settingsStatus.Text = invalid; return; }
        settings = next; // Update the in-memory snapshot before asynchronous disk work.
        string? error = await settingsWriter.SaveAsync(next);
        if (settings != next) return; // Do not overwrite a newer save's status.
        settingsStatus.Text = error != null ? "设置未保存：" + error :
            invalid ?? "设置已保存（快捷键、节奏模式、速度和停顿）。";
    }
    private void ApplyHotkeys()
    {
        if (hotkeys == null || cancellation != null || closing || beginning || editingHotkeys) return;
        hotkeys.Apply(settings.Start, settings.Stop);
        start.Content = "开始 " + settings.Start.Label; stop.Content = "停止 " + settings.Stop.Label;
        hotkeyStatus.Text = hotkeys.Describe(settings.Start, settings.Stop);
    }
    private void ConfigureHotkeys()
    {
        if (hotkeys == null || cancellation != null || closing || beginning || editingHotkeys) return;
        editingHotkeys = true; hotkeys.Suspend();
        try
        {
            var dialog = new HotkeyDialog(settings.Start, settings.Stop) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                settings = settings with { Start = dialog.Start, Stop = dialog.Stop };
                _ = SaveCurrentSettingsAsync(true);
            }
        }
        finally { editingHotkeys = false; ApplyHotkeys(); }
    }
    private async Task WaitForRelease(uint triggerKey, CancellationToken token)
    {
        var timeout = Stopwatch.StartNew();
        while (NativeHotkeys.Held(triggerKey))
        {
            token.ThrowIfCancellationRequested();
            if (timeout.Elapsed.TotalSeconds > 15)
                throw new InvalidOperationException("等待松开快捷键超时，已取消演奏。");
            status.Text = $"请松开启动键及 Ctrl / Alt / Shift / Win；{settings.Stop.Label} 可停止。";
            await Task.Delay(20, token);
        }
        token.ThrowIfCancellationRequested();
    }
    private void Import()
    {
        if (cancellation != null || beginning || editingHotkeys || closing) return;
        beginning = true; SetBusy(true);
        try
        {
            var dialog = new OpenFileDialog { Filter = "TXT 简谱|*.txt" };
            if (dialog.ShowDialog(this) != true || closing) return;
            if (new FileInfo(dialog.FileName).Length > 1024 * 1024) throw new IOException("文件不得超过1MB。");
            score.Text = File.ReadAllText(dialog.FileName, new UTF8Encoding(false, true));
            Title = "口琴简谱播放器 0.1.5 — " + Path.GetFileName(dialog.FileName);
            Preview(); status.Text = "已导入；请查看解析预览。TXT只放谱面，速度在界面设置。";
        }
        catch (Exception e) { status.Text = "导入失败，请使用UTF-8 TXT：" + e.Message; }
        finally { beginning = false; SetBusy(false); }
    }
    private void Stop()
    {
        if (closing) return; // Shutdown owns final input cleanup; do not race it.
        cancellation?.Cancel();
        if (cancellation != null) status.Text = "正在停止并释放输入…";
        else { var error = output.Release(); status.Text = error ?? "已停止。"; }
    }
    private async Task Begin(uint triggerKey = 0)
    {
        if (cancellation != null || closing || beginning || editingHotkeys) return;
        beginning = true; SetBusy(true);
        try
        {
            var notes = ScoreParser.Parse(score.Text, rhythm.IsChecked == true);
            var (ms, spaceMs, lineMs) = Timing();
            if (!int.TryParse(gap.Text, out int silence) || silence < 10 || silence > 5000 ||
                notes.Any(n => n.Degree != 0 && n.Beats * ms - silence < 40))
                throw new FormatException("留白范围10～5000毫秒；最短音减去留白后须至少40毫秒。请降低速度或减少留白。");
            bool simulation = dry.IsChecked == true;
            if (!simulation && hotkeys?.StopReady != true)
                throw new InvalidOperationException($"停止键 {settings.Stop.Label} 不可用，禁止真实演奏。请点击“自定义快捷键”或“重试注册”。");
            cancellation = new CancellationTokenSource();
            if (closing || cancellation.IsCancellationRequested) return;
            _ = SaveCurrentSettingsAsync();
            SetBusy(true); log.Clear();
            running = Run(notes, ms, silence, spaceMs, lineMs, simulation, triggerKey, cancellation.Token);
            await running;
        }
        catch (Exception e) { status.Text = e.Message; }
        finally
        {
            cancellation?.Dispose(); cancellation = null; running = null; beginning = false; SetBusy(false);
        }
    }
    private (double Ms, int Space, int Line) Timing()
    {
        if (rhythm.IsChecked == true)
        {
            if (!int.TryParse(bpm.Text, out int tempo) || tempo < 20 || tempo > 300)
                throw new FormatException("速度范围20～300（四分音符/分钟）。");
            return (60000.0 / tempo, 0, 0);
        }
        if (!int.TryParse(duration.Text, out int ms) || ms < 80 || ms > 5000)
            throw new FormatException("旧模式每音时长范围80～5000毫秒。");
        if (!int.TryParse(spaceGap.Text, out int space) || space < 0 || space > 10000 ||
            !int.TryParse(lineGap.Text, out int line) || line < 0 || line > 10000)
            throw new FormatException("空格和换行停顿范围0～10000毫秒。");
        return (ms, space, line);
    }
    private void Preview()
    {
        if (cancellation != null || closing) return;
        try
        {
            var notes = ScoreParser.Parse(score.Text, rhythm.IsChecked == true);
            var (ms, space, line) = Timing();
            double total = 0;
            var lines = new StringBuilder();
            foreach (var note in notes)
            {
                total += note.PauseBefore(space, line);
                if (lines.Length < 30000)
                    lines.AppendLine($"{total / 1000:0.###}s  {note.Label}  {note.Beats:0.###}拍  ({note.Beats * ms:0.##}ms)");
                total += note.Beats * ms;
            }
            preview.Text = $"共{notes.Count}个音/休止，{notes.Sum(n => n.Beats):0.###}拍，预计{total / 1000:0.###}秒（不含倒计时）\n" + lines;
        }
        catch (Exception e) { preview.Text = "谱面/设置错误：" + e.Message; }
    }
    private void SetBusy(bool busy)
    {
        busy |= closing;
        start.IsEnabled = import.IsEnabled = refresh.IsEnabled = rhythm.IsEnabled = gap.IsEnabled = dry.IsEnabled = !busy;
        bool useRhythm = rhythm.IsChecked == true;
        bpm.IsEnabled = !busy && useRhythm;
        duration.IsEnabled = spaceGap.IsEnabled = lineGap.IsEnabled = !busy && !useRhythm;
        configure.IsEnabled = retry.IsEnabled = !busy;
        score.IsReadOnly = busy;
    }
    private async Task Run(List<ScoreNote> notes, double ms, int silence, int spaceMs, int lineMs, bool simulation, uint triggerKey, CancellationToken token)
    {
        string result = "演奏完成。";
        try
        {
            await WaitForRelease(triggerKey, token);
            for (int n = 3; n > 0; n--)
            { status.Text = $"{n}秒后开始，请切到目标窗口…"; await Task.Delay(1000, token); }
            // Check again after countdown: the user may have used Alt+Tab to switch windows.
            await WaitForRelease(triggerKey, token);
            var target = NativeInput.GetForegroundWindow();
            NativeInput.GetWindowThreadProcessId(target, out uint pid);
            if (!simulation && (target == IntPtr.Zero || pid == (uint)Environment.ProcessId))
                throw new InvalidOperationException("没有切到外部目标窗口，已取消。");
            await Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                void Check()
                {
                    token.ThrowIfCancellationRequested();
                    if (!simulation && NativeInput.GetForegroundWindow() != target)
                        throw new InvalidOperationException("目标窗口失去焦点，已停止。");
                }
                void Wait(double until)
                {
                    while (true)
                    {
                        Check(); double remaining = until - clock.Elapsed.TotalMilliseconds;
                        if (remaining <= 0) return;
                        if (remaining > 2) token.WaitHandle.WaitOne((int)Math.Min(5, Math.Max(1, remaining - 1)));
                        else Thread.SpinWait(100);
                    }
                }
                var initialError = simulation ? null : output.Release();
                if (initialError != null) throw new InvalidOperationException(initialError);
                double nextStart = 0;
                for (int i = 0; i < notes.Count; i++)
                {
                    var note = notes[i];
                    double noteMs = note.Beats * ms;
                    int pause = note.PauseBefore(spaceMs, lineMs);
                    double begin = nextStart + pause;
                    if (pause > 0)
                    {
                        string pauseLabel = note.SeparatorBefore == ScoreSeparator.LineBreak ? "换行" : "空格";
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            if (!closing && !token.IsCancellationRequested)
                                status.Text = $"{pauseLabel}停顿：{pause}ms";
                        }));
                    }
                    Wait(begin); Check();
                    // 严重超时直接停止，避免恢复后瞬间补发积压音符。
                    if (clock.Elapsed.TotalMilliseconds - begin > Math.Min(50, noteMs / 4.0))
                        throw new InvalidOperationException("调度延迟过大，已停止；请降低后台负载后重试。");
                    int index = i;
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        if (closing || token.IsCancellationRequested) return;
                        status.Text = $"{index + 1}/{notes.Count}：{note.Label} / {note.Beats:0.###}拍";
                        if (log.LineCount > 150) log.Clear();
                        log.AppendText($"{begin:0}ms  {note.Label}  {note.Beats:0.###}拍 ({noteMs:0.##}ms)\n"); log.ScrollToEnd();
                    }));
                    if (note.Degree != 0)
                    {
                        if (!simulation) output.Modifiers(note);
                        Wait(begin + 12); Check();
                        if (!simulation) output.NoteOn(note);
                        Wait(begin + noteMs - silence);
                        var error = simulation ? null : output.Release();
                        if (error != null) throw new InvalidOperationException(error);
                    }
                    Wait(begin + noteMs);
                    nextStart = begin + noteMs;
                }
            }, token);
        }
        catch (OperationCanceledException) { result = "已停止。"; }
        catch (Exception e) { result = "已停止：" + e.Message; }
        finally
        {
            // 工作任务已退出，再做最终释放，避免释放之后仍有旧任务按键。
            var error = simulation ? null : await Task.Run(output.Release);
            if (!closing) status.Text = error == null ? result : result + " 释放失败，请手动按下并松开相关键：" + error;
        }
    }
}
