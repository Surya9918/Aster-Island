namespace Aster.Core;

public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

public sealed class FocusTimer(IClock clock)
{
    private DateTimeOffset? deadline;
    private TimeSpan remaining = TimeSpan.FromMinutes(25);
    public bool Running => deadline.HasValue;
    public TimeSpan Remaining => deadline is { } end ? Max(end - clock.UtcNow) : remaining;
    public void Start() { if (!Running && remaining > TimeSpan.Zero) deadline = clock.UtcNow + remaining; }
    public void Pause() { remaining = Remaining; deadline = null; }
    public void Reset(int minutes) { deadline = null; remaining = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 180)); }
    public bool CompleteIfDue()
    {
        if (!Running || Remaining > TimeSpan.Zero) return false;
        deadline = null; remaining = TimeSpan.Zero; return true;
    }
    private static TimeSpan Max(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
