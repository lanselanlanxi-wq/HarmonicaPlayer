namespace HarmonicaPlayer;

// All writes are serialized off the UI thread. Duplicate pending/successful
// snapshots share a task; failed saves can be retried without changing a value.
public sealed class SettingsWriter(Action<AppSettings> write, AppSettings? initialSaved = null)
{
    private readonly object gate = new();
    private Task<string?> tail = Task.FromResult<string?>(null);
    private AppSettings? requested;
    private AppSettings? saved = initialSaved;
    public Task<string?> SaveAsync(AppSettings snapshot)
    {
        snapshot.Validate();
        lock (gate)
        {
            if (!tail.IsCompleted && snapshot == requested) return tail;
            if (tail.IsCompletedSuccessfully && tail.Result == null && snapshot == saved) return tail;
            requested = snapshot;
            var previous = tail;
            tail = Task.Run(async () =>
            {
                await previous.ConfigureAwait(false);
                try
                {
                    write(snapshot);
                    lock (gate) saved = snapshot;
                    return (string?)null;
                }
                catch (Exception e) { return e.Message; }
            });
            return tail;
        }
    }
    public Task<string?> FlushAsync() { lock (gate) return tail; }
}
