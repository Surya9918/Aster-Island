namespace Aster.Core;

public sealed record SpringConfiguration(double Stiffness = 420, double Damping = 38, double Mass = 1)
{
    public SpringConfiguration Validated() => new(
        double.IsFinite(Stiffness) ? Math.Clamp(Stiffness, 80, 900) : 420,
        double.IsFinite(Damping) ? Math.Clamp(Damping, 10, 100) : 38,
        double.IsFinite(Mass) ? Math.Clamp(Mass, .5, 3) : 1);
}

/// <summary>Analytic damped harmonic oscillator; retargeting preserves velocity.</summary>
public sealed class SpringValue(double initial)
{
    public double Position { get; private set; } = initial;
    public double Velocity { get; private set; }
    public double Target { get; set; } = initial;
    public bool Settled => Math.Abs(Position - Target) < .02 && Math.Abs(Velocity) < .12;
    public void Snap(double value) { Position = Target = value; Velocity = 0; }
    public void Step(double seconds, SpringConfiguration configuration)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (seconds == 0) return;
        var c = configuration.Validated();
        var a = c.Damping / (2 * c.Mass);
        var w2 = c.Stiffness / c.Mass;
        var y = Position - Target;
        var v = Velocity;
        var discriminant = a * a - w2;
        if (Math.Abs(discriminant) < .000001)
        {
            var b = v + a * y; var decay = Math.Exp(-a * seconds);
            Position = Target + decay * (y + b * seconds);
            Velocity = decay * (v - a * b * seconds);
        }
        else if (discriminant < 0)
        {
            var w = Math.Sqrt(-discriminant); var decay = Math.Exp(-a * seconds);
            var cos = Math.Cos(w * seconds); var sin = Math.Sin(w * seconds); var b = (v + a * y) / w;
            Position = Target + decay * (y * cos + b * sin);
            Velocity = decay * (v * cos - (a * v + w2 * y) / w * sin);
        }
        else
        {
            var root = Math.Sqrt(discriminant); var r1 = -a + root; var r2 = -a - root;
            var b1 = (v - r2 * y) / (r1 - r2); var b2 = y - b1;
            var e1 = Math.Exp(r1 * seconds); var e2 = Math.Exp(r2 * seconds);
            Position = Target + b1 * e1 + b2 * e2; Velocity = r1 * b1 * e1 + r2 * b2 * e2;
        }
        if (Settled) Snap(Target);
    }
}

public enum IslandState { Idle, Hover, Expanded, Notification, PersistentActivity, Transition, Hidden, Stowed, SystemHud }
public enum IslandPage { Media, Focus, Shelf, Notes, Controls, Today, Notifications }
public enum SystemHudKind { Volume, Brightness }
public enum ActivityKind { Media, Focus }
public sealed record IslandActivity(ActivityKind Kind, int Priority, string Label);

/// <summary>Retained activity arbitration and user intent; no rendering or OS dependencies.</summary>
public sealed class IslandStateMachine(IClock clock)
{
    private readonly Dictionary<ActivityKind, IslandActivity> activities = [];
    private IslandState resume = IslandState.Idle;
    private DateTimeOffset? noticeUntil;
    private bool stowed;
    public bool StowContext=>stowed;
    public SystemHudKind HudKind {get;private set;}
    public double HudValue {get;private set;}
    public bool HudMuted {get;private set;}
    public IslandState Target { get; private set; } = IslandState.Idle;
    public IslandState Current { get; private set; } = IslandState.Idle;
    public IslandPage Page { get; private set; } = IslandPage.Today;
    public string NoticeTitle { get; private set; } = "";
    public string NoticeDetail { get; private set; } = "";
    public IslandActivity? PrimaryActivity => activities.Values.OrderByDescending(a => a.Priority).FirstOrDefault();
    public int ActivityCount => activities.Count;
    public event Action? Changed;
    private IslandState Resting => stowed?IslandState.Stowed:activities.Count > 0 ? IslandState.PersistentActivity : IslandState.Idle;
    public void SetStowed(bool value)
    {
        if(stowed==value)return;stowed=value;
        if(Target is IslandState.Idle or IslandState.Hover or IslandState.PersistentActivity or IslandState.Stowed)Retarget(Resting);
    }
    public void SetActivity(IslandActivity? activity, ActivityKind kind)
    {
        if (activity is null) activities.Remove(kind); else activities[kind] = activity;
        if (Target is IslandState.Idle or IslandState.PersistentActivity or IslandState.Stowed) Retarget(Resting);
        else Changed?.Invoke();
    }
    public void Hover(bool inside)
    {
        if (Target is IslandState.Hidden or IslandState.Expanded or IslandState.Notification or IslandState.SystemHud) return;
        Retarget(inside ? IslandState.Hover : Resting);
    }
    public void Expand(IslandPage? page = null)
    {
        if (page is { } selected && selected != Page) { Page = selected; Current = IslandState.Transition; }
        noticeUntil = null; Retarget(IslandState.Expanded);
    }
    public void Select(IslandPage page) => Expand(page);
    public void ClearNotice(){NoticeTitle=NoticeDetail="";noticeUntil=null;}
    public void Collapse() { noticeUntil = null; Retarget(Resting); }
    public void SetHidden(bool hidden) { noticeUntil = null; Retarget(hidden ? IslandState.Hidden : Resting); }
    public void ShowSystemHud(SystemHudKind kind,double value,bool muted=false)
    {
        if(Target==IslandState.Hidden)return;
        if(Target is not(IslandState.Notification or IslandState.SystemHud))resume=Target;
        HudKind=kind;HudValue=double.IsFinite(value)?Math.Clamp(value,0,100):0;HudMuted=muted;
        noticeUntil=clock.UtcNow+TimeSpan.FromSeconds(1.6);Retarget(IslandState.SystemHud);
    }
    public void Notify(string title, string detail, TimeSpan? duration = null)
    {
        if (Target == IslandState.Hidden) return;
        if (Target != IslandState.Notification) resume = Target;
        NoticeTitle = title; NoticeDetail = detail;
        noticeUntil = clock.UtcNow + (duration ?? TimeSpan.FromSeconds(3));
        Retarget(IslandState.Notification);
    }
    public void Tick()
    {
        if (noticeUntil is not { } end || clock.UtcNow < end) return;
        noticeUntil = null; Retarget(resume == IslandState.Expanded ? IslandState.Expanded : Resting);
    }
    public void MarkSettled() { if (Current == Target) return; Current = Target; Changed?.Invoke(); }
    private void Retarget(IslandState state)
    {
        var changed = Target != state;
        Target = state;
        if (changed) Current = IslandState.Transition;
        Changed?.Invoke();
    }
}
