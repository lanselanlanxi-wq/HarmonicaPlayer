using System.Diagnostics;
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
                        window = new PlayerWindow(path);
                        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        window.Closed += (_, _) => closed.TrySetResult();
                        window.Show();
                        await Task.Delay(100);
                        if (test == "error-recovery")
                        {
                            var editor = Descendants(window).OfType<TextBox>().Single(t => t.AcceptsReturn && !t.IsReadOnly);
                            var startButton = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString()?.StartsWith("开始 ") == true);
                            var locate = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString() == "定位曲谱错误");
                            editor.Text = "1\n8";
                            await Task.Delay(300);
                            if (startButton.IsEnabled || !locate.IsEnabled) throw new Exception("Invalid score did not disable start/enable location.");
                            locate.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            if (editor.SelectionStart != 2 || editor.SelectionLength != 1) throw new Exception("Error location is incorrect.");
                            var bindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                            await (Task)typeof(PlayerWindow).GetMethod("Begin", bindingFlags)!.Invoke(window, new object[] { (uint)0 })!;
                            editor.Text = "1 2";
                            await Task.Delay(300);
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
                            await Task.Delay(test == "countdown" ? 100 : 3400);
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
            }
            catch (Exception e) { failures++; Console.Error.WriteLine(e); }
            finally { app.Shutdown(); }
        };
        app.Run();
        return failures == 0 ? 0 : 1;
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
