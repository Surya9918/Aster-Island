namespace Aster.Core;

public sealed record NetworkAuditEntry(DateTimeOffset Time, string Host, string Purpose, string Outcome);
public sealed class NetworkAudit(IClock clock)
{
    private readonly object gate = new();
    private readonly List<NetworkAuditEntry> items = [];
    public IReadOnlyList<NetworkAuditEntry> Entries { get { lock (gate) return items.ToArray(); } }
    public event Action? Changed;
    public void Record(string host, string outcome)
    {
        // Fixed host and purpose prevent URLs, coordinates, or credentials entering the audit.
        lock (gate)
        {
            items.Insert(0, new(clock.UtcNow, host == "api.open-meteo.com" ? host : "unapproved-host", "weather", outcome));
            if (items.Count > 100) items.RemoveRange(100, items.Count - 100);
        }
        Changed?.Invoke();
    }
    public void Clear() { lock (gate) items.Clear(); Changed?.Invoke(); }
}

public sealed class NetworkConsent : IDisposable
{
    private readonly object gate = new();
    private CancellationTokenSource session = new();
    private bool enabled;
    public bool Enabled { get { lock (gate) return enabled; } }
    public void Grant() { lock (gate) { if (enabled) return; session.Dispose(); session = new(); enabled = true; } }
    public void Revoke() { lock (gate) { enabled = false; session.Cancel(); } }
    public CancellationToken Authorize(Uri uri)
    {
        lock (gate)
        {
            if (!enabled || uri.Scheme != "https" || uri.Host != "api.open-meteo.com" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/v1/forecast")
                throw new UnauthorizedAccessException("Weather network permission is disabled or destination is disallowed.");
            return session.Token;
        }
    }
    public void Dispose() { Revoke(); session.Dispose(); }
}
