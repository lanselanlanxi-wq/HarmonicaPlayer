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
                foreach (string test in new[] { "idle", "pending-save", "countdown", "playback", "test-playback" })
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
                        if (test == "pending-save")
                        {
                            // A valid numeric edit queues the real debounced settings saver.
                            var number = Descendants(window).OfType<TextBox>().First(t => t.Text == "300" && !t.IsReadOnly);
                            number.Text = "350";
                        }
                        if (test is "countdown" or "playback" or "test-playback")
                        {
                            var dry = Descendants(window).OfType<CheckBox>().Single(c => c.Content?.ToString()?.StartsWith("仅日志测试") == true);
                            if (dry.IsChecked != true) throw new Exception("Dry-run default is off.");
                            string buttonLabel = test == "test-playback" ? "播放测试" : "开始 ";
                            if (test == "test-playback") dry.IsChecked = false;
                            var play = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString()?.StartsWith(buttonLabel) == true);
                            play.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            await Task.Delay(test == "countdown" ? 100 : 3400);
                            if (test == "test-playback")
                            {
                                var log = Descendants(window).OfType<TextBox>().Single(t => t.IsReadOnly && t.Height == 110);
                                if (string.IsNullOrWhiteSpace(log.Text)) throw new Exception("Test playback did not produce a log entry.");
                            }
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
