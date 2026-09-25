using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace HarmonicaPlayer;

// Windows-only regression test using actual WPF windows. Only dry/log playback;
// no note input. Uses a fresh temporary settings file for each run.
public static class WindowsSmokeTests
{
    [STAThread]
    public static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int failures = 0;
        app.Startup += async (_, _) =>
        {
            try
            {
                foreach (string test in new[] { "idle", "pending-save", "countdown", "playback", "error-recovery" })
                {
                    string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HarmonicaPlayerSmoke-" + Guid.NewGuid().ToString("N") + ".json");
                    PlayerWindow? window = null;
                    try
                    {
                        window = new PlayerWindow(path, new TestScoreDialogs());
                        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        window.Closed += (_, _) => closed.TrySetResult();
                        window.Show();
                        await WaitUntil(() => window.IsLoaded, "window loaded");
                        if (test == "error-recovery")
                        {
                            var editor = Descendants(window).OfType<TextBox>().Single(t => t.AcceptsReturn && !t.IsReadOnly);
                            var startButton = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString()?.StartsWith("开始 ") == true);
                            var locate = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString() == "定位曲谱错误");
                            editor.Text = "1\n8";
                            await WaitUntil(() => !startButton.IsEnabled && locate.IsEnabled, "invalid score displayed");
                            if (startButton.IsEnabled || !locate.IsEnabled) throw new Exception("Invalid score did not disable start/enable location.");
                            locate.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            if (editor.SelectionStart != 2 || editor.SelectionLength != 1) throw new Exception("Error location is incorrect.");
                            var bindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                            await (Task)typeof(PlayerWindow).GetMethod("Begin", bindingFlags)!.Invoke(window, new object[] { (uint)0 })!;
                            editor.Text = "1 2";
                            await WaitUntil(() => startButton.IsEnabled && !locate.IsEnabled, "score recovered");
                            if (!startButton.IsEnabled || locate.IsEnabled) throw new Exception("Corrected score did not restore controls.");
                            if (typeof(PlayerWindow).GetField("scoreIssue", bindingFlags)!.GetValue(window) != null ||
                                typeof(PlayerWindow).GetField("operationIssue", bindingFlags)!.GetValue(window) != null)
                                throw new Exception("Corrected score left a stale error.");
                            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                            var controller = (HotkeyController)typeof(PlayerWindow).GetField("hotkeys", flags)!.GetValue(window)!;
                            controller.Suspend();
                            var dryBox = Descendants(window).OfType<CheckBox>().Single();
                            dryBox.IsChecked = false;
                            if (startButton.IsEnabled) throw new Exception("Real playback allowed without stop hotkey.");
                            dryBox.IsChecked = true;
                            if (!startButton.IsEnabled) throw new Exception("Dry run unnecessarily blocked by stop hotkey.");
                        }
                        if (test == "pending-save")
                        {
                            // A valid numeric edit queues the real debounced settings saver.
                            var number = Descendants(window).OfType<TextBox>().First(t => t.Text == "120" && !t.IsReadOnly);
                            number.Text = "110";
                        }
                        if (test is "countdown" or "playback")
                        {
                            var dry = Descendants(window).OfType<CheckBox>().Single(c => c.Content?.ToString()?.StartsWith("仅日志测试") == true);
                            if (dry.IsChecked != true) throw new Exception("Dry-run default is off.");
                            var start = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString()?.StartsWith("开始 ") == true);
                            start.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            await WaitUntil(() => test == "countdown"
                                ? Field<TextBlock>(window, "status").Text.Contains("秒后开始")
                                : Field<TextBox>(window, "log").Text.Length > 0, test + " actually started");
                        }
                        var clock = Stopwatch.StartNew();
                        window.Close();
                        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        Console.WriteLine($"PASS {test}: close {clock.ElapsedMilliseconds}ms");
                    }
                    finally
                    {
                        if (window?.IsVisible == true) window.Close();
                        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    }
                }
                await SettingsGenerationTests();
                await DocumentWindowTests();
                await AudioWindowTests();
            }
            catch (Exception e) { failures++; Console.Error.WriteLine(e); }
            finally { app.Shutdown(); }
        };
        app.Run();
        return failures == 0 ? 0 : 1;
    }
    private static T Field<T>(PlayerWindow window, string name) =>
        (T)typeof(PlayerWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
    private static Task Invoke(PlayerWindow window, string name, params object[] args) =>
        (Task)typeof(PlayerWindow).GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, args)!;
    private static async Task WaitUntil(Func<bool> predicate, string name)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(8)) throw new Exception("Timed out: " + name);
            await Task.Delay(20);
        }
    }
    private static async Task CloseWindow(PlayerWindow window)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        window.Close(); await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    private static async Task SettingsGenerationTests()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int writes = 0;
        var window = new PlayerWindow(path, new TestScoreDialogs(), value =>
        {
            if (Interlocked.Increment(ref writes) == 1)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new IOException("Test write timeout");
            }
            SettingsStore.Save(path, value);
        });
        try
        {
            window.Show();
            var bpm = Field<TextBox>(window, "bpm");
            bpm.Text = "90";
            Task slow = Invoke(window, "SaveCurrentSettingsAsync", false);
            await WaitUntil(() => entered.IsSet, "slow settings write entered");
            bpm.Text = "";
            await Invoke(window, "SaveCurrentSettingsAsync", false);
            string error = Field<TextBlock>(window, "settingsStatus").Text;
            if (!error.Contains("尚未填写完整")) throw new Exception("Invalid edit not reported");
            release.Set(); await slow;
            if (Field<TextBlock>(window, "settingsStatus").Text != error) throw new Exception("Old save replaced newer invalid status");
            bpm.Text = "100"; Task a = Invoke(window, "SaveCurrentSettingsAsync", false);
            bpm.Text = "110"; Task b = Invoke(window, "SaveCurrentSettingsAsync", false);
            bpm.Text = "100"; Task final = Invoke(window, "SaveCurrentSettingsAsync", false);
            await Task.WhenAll(a, b, final);
            if (SettingsStore.Load(path, out _).Bpm != 100) throw new Exception("A-B-A final save incorrect");
            bpm.Text = "95";
            await CloseWindow(window);
            if (SettingsStore.Load(path, out _).Bpm != 95) throw new Exception("Close lost final valid edit");
            Console.WriteLine("PASS settings-generation: slow invalid edit, A-B-A, close final value");
        }
        finally
        {
            release.Set();
            if (window.IsVisible) await CloseWindow(window);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }
    private static async Task DocumentWindowTests()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HarmonicaUI-" + Guid.NewGuid());
        System.IO.Directory.CreateDirectory(directory);
        var dialogs = new TestScoreDialogs();
        var window = new PlayerWindow(System.IO.Path.Combine(directory, "settings.json"), dialogs);
        try
        {
            window.Show();
            var editor = Field<TextBox>(window, "score");
            editor.Text = "1 2 3";
            if (!window.Title.EndsWith(" *")) throw new Exception("Dirty title missing");
            dialogs.Answer = MessageBoxResult.Cancel;
            window.Close();
            await WaitUntil(() => !window.IsClosing, "cancel close");
            if (!window.IsVisible || editor.Text != "1 2 3") throw new Exception("Cancel lost document");
            dialogs.SavePath = System.IO.Path.Combine(directory, "saved.txt");
            await Invoke(window, "DocumentOperation", (Func<Task<bool>>)(async () =>
                await (Task<bool>)typeof(PlayerWindow).GetMethod("SaveScoreAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, new object[] { false })!));
            if (window.Title.EndsWith(" *") || !System.IO.File.Exists(dialogs.SavePath)) throw new Exception("Save did not mark clean");
            Field<TextBox>(window, "gap").Text = "21";
            if (!window.Title.EndsWith(" *")) throw new Exception("Gap edit must mark score unsaved");
            Field<TextBox>(window, "gap").Text = "20";
            if (window.Title.EndsWith(" *")) throw new Exception("Restoring gap must restore clean state");
            editor.Text = "1 2";
            dialogs.OpenPath = dialogs.SavePath;
            dialogs.Answer = MessageBoxResult.Yes;
            await Invoke(window, "Import");
            if (editor.Text != "1 2") throw new Exception("Reimport read stale pre-save file");
            string legacyPath = System.IO.Path.Combine(directory, "old bpm100.txt");
            await System.IO.File.WriteAllTextAsync(legacyPath, "5 6 7");
            dialogs.OpenPath = legacyPath;
            await Invoke(window, "Import");
            if (Field<TextBox>(window, "bpm").Text != "100" || editor.Text != "5 6 7") throw new Exception("Legacy import failed");
            editor.Text = "5 6";
            dialogs.Answer = MessageBoxResult.Cancel;
            await Invoke(window, "Import");
            if (editor.Text != "5 6") throw new Exception("Cancelled import lost edits");
            dialogs.SavePath = legacyPath;
            dialogs.Answer = MessageBoxResult.Yes;
            window.Close();
            await WaitUntil(() => !window.IsClosing, "failed save cancels close");
            if (!window.IsVisible || await System.IO.File.ReadAllTextAsync(legacyPath) != "5 6 7") throw new Exception("Legacy overwrite protection failed");
            string badPath = System.IO.Path.Combine(directory, "bad.txt");
            await System.IO.File.WriteAllTextAsync(badPath, "@format=unknown\n@bpm=100\n\n1");
            dialogs.OpenPath = badPath;
            await Invoke(window, "Import");
            if (editor.Text != "5 6") throw new Exception("Malformed import lost editor");
            dialogs.SavePath = System.IO.Path.Combine(directory, "converted.txt");
            await CloseWindow(window);
            var saved = await ScoreDocumentReader.ReadAsync(dialogs.SavePath, 120);
            if (saved.Document.Bpm != 100 || saved.Document.ScoreText != "5 6") throw new Exception("Close-save roundtrip failed");
            Console.WriteLine("PASS document UI: dirty, cancel, save, legacy protection, malformed import, close-save");
        }
        finally
        {
            dialogs.Answer = MessageBoxResult.No;
            if (window.IsVisible) await CloseWindow(window);
            foreach (string file in System.IO.Directory.GetFiles(directory)) System.IO.File.Delete(file);
            System.IO.Directory.Delete(directory);
        }
    }
    private static async Task AudioWindowTests()
    {
        string path = Path.Combine(Path.GetTempPath(), "HarmonicaAudioUI-" + Guid.NewGuid() + ".json");
        var fake = new FakeLocalAudio();
        var dialogs = new TestScoreDialogs { MidiPath = path + ".mid" };
        var window = new PlayerWindow(path, dialogs, audioPlayer: fake);
        try
        {
            window.Show(); await WaitUntil(() => window.IsLoaded, "audio window loaded");
            var editor = Field<TextBox>(window, "score");
            editor.Text = "1 【2】 0 3"; editor.Select(3, 0);
            await WaitUntil(() => Field<Button>(window, "listen").IsEnabled, "listen available");
            await Invoke(window, "ExportMidiAsync");
            if (!File.Exists(dialogs.MidiPath) || !window.Title.EndsWith(" *") || editor.IsReadOnly)
                throw new Exception("MIDI export lost dirty state or did not create file");
            dialogs.MidiPath = null;
            await Invoke(window, "ExportMidiAsync");
            if (!window.Title.EndsWith(" *") || editor.IsReadOnly)
                throw new Exception("Cancelled export changed editor");
            Field<HotkeyController>(window, "hotkeys").Suspend();
            Field<CheckBox>(window, "dry").IsChecked = false;
            if (Field<Button>(window, "start").IsEnabled || !Field<Button>(window, "listen").IsEnabled)
                throw new Exception("Local audio depends on game hotkey readiness");
            var run = Invoke(window, "ListenAsync", true);
            await WaitUntil(() => fake.Active, "audio started");
            if (fake.StartIndex != 1 || !editor.IsReadOnly || Field<Button>(window,"exportMidi").IsEnabled ||
                Field<Button>(window,"start").IsEnabled || Field<TextBox>(window,"bpm").IsEnabled)
                throw new Exception("Audio cursor or mutual exclusion failed");
            fake.Report!(1000);
            await WaitUntil(() => editor.SelectionStart == 6, "progress highlights rest");
            Field<Slider>(window, "volumeSlider").Value = 35;
            if (fake.Volume != 35) throw new Exception("Volume not forwarded");
            Field<Button>(window,"stop").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await run.WaitAsync(TimeSpan.FromSeconds(3));
            if(editor.IsReadOnly || editor.SelectionStart != 3 || !Field<Button>(window,"listen").IsEnabled)
                throw new Exception("Stop did not restore editor/cursor");
            fake.Fail = true;
            await Invoke(window,"ListenAsync",false);
            if(editor.IsReadOnly || !Field<TextBlock>(window,"alert").Text.Contains("fake audio device"))
                throw new Exception("Audio error not visible or UI stuck");
            fake.Fail = false;
            run = Invoke(window,"ListenAsync",false);
            await WaitUntil(() => fake.Active,"audio retry");
            fake.Complete(); await run.WaitAsync(TimeSpan.FromSeconds(3));
            if(Field<ProgressBar>(window,"audioProgress").Value != 100 || editor.IsReadOnly)
                throw new Exception("Audio completion did not restore UI");
            // Replay then close while a stream is active.
            run = Invoke(window,"ListenAsync",false);
            await WaitUntil(() => fake.Active,"audio before close");
            await CloseWindow(window); await run;
            if(fake.Active) throw new Exception("Audio continued after close");
            var saved = SettingsStore.Load(path, out var warning);
            if(warning != null || saved.PreviewVolume != 35) throw new Exception("Preview volume not saved");
            Console.WriteLine("PASS v0.3.0 audio UI: cursor, progress, lock, volume, stop, failure/retry, complete, close");
        }
        finally
        {
            if(window.IsVisible) await CloseWindow(window);
            if(File.Exists(path)) File.Delete(path);
            if(File.Exists(path + ".mid")) File.Delete(path + ".mid");
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject dependency)
            {
                yield return dependency;
                foreach (var nested in Descendants(dependency)) yield return nested;
            }
    }
}

sealed class TestScoreDialogs : IScoreDialogs
{
    public string? OpenPath, SavePath, MidiPath;
    public MessageBoxResult Answer = MessageBoxResult.No;
    public string? Open(Window owner) => OpenPath;
    public string? Save(Window owner, string suggestedName) => SavePath;
    public MessageBoxResult Unsaved(Window owner) => Answer;
    public string? SaveMidi(Window owner, string suggestedName) => MidiPath;
}

sealed class FakeLocalAudio : ILocalAudioPlayer
{
    public int Volume { get; set; }
    public bool Active, Fail;
    public int StartIndex;
    public Action<double>? Report;
    private TaskCompletionSource? completion;
    public void Complete() => completion!.TrySetResult();
    public async Task PlayAsync(ScoreTimeline timeline, int startIndex, Action<double> progress, CancellationToken token)
    {
        if(Fail) throw new InvalidOperationException("fake audio device unavailable");
        Active=true; StartIndex=startIndex; Report=progress;
        completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
        try { await completion.Task.WaitAsync(token); }
        finally { Active=false; }
    }
}
