using System.Security.Principal;
using System.Windows;

namespace HarmonicaPlayer;

public static class SingleInstance
{
    public static void Run()
    {
        string scope = @"Local\HarmonicaPlayer." + WindowsIdentity.GetCurrent().User!.Value;
        using var mutex = new Mutex(false, scope + ".Mutex");
        bool owned;
        try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
        if (!owned)
        {
            // Covers the small gap between mutex acquisition and event creation.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try { using var signal = EventWaitHandle.OpenExisting(scope + ".Activate"); signal.Set(); return; }
                catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(50); }
            }
            MessageBox.Show("播放器已经运行，请在任务栏中打开已有窗口。", "HarmonicaPlayer");
            return;
        }
        try
        {
            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, scope + ".Activate");
            var app = new Application();
            var window = new PlayerWindow();
            var registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) =>
            {
                try
                {
                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (app.Dispatcher.HasShutdownStarted) return;
                        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                        window.Show(); window.Activate();
                    }));
                }
                catch (InvalidOperationException) { /* The application is shutting down. */ }
            }, null, Timeout.Infinite, false);
            try { app.Run(window); }
            finally { registration.Unregister(null); }
        }
        finally { mutex.ReleaseMutex(); }
    }
}
