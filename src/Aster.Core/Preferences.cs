using System.Text.RegularExpressions;

namespace Aster.Core;

public sealed record Preferences
{
    public string Theme { get; init; } = "Dark";
    public string Accent { get; init; } = "#76D8CB";
    public string BackgroundColor { get; init; } = "";
    public string CardColor { get; init; } = "";
    public string TextColor { get; init; } = "";
    public string Font { get; init; } = "Segoe UI";
    public double TextScale { get; init; } = 1;
    public double OverlayWidth { get; init; } = 560;
    public double Radius { get; init; } = 20;
    public double Opacity { get; init; } = 1;
    public double TopOffset { get; init; } = 12;
    public int Monitor { get; init; }
    public bool ReducedMotion { get; init; }
    public bool ShowOverlay { get; init; } = true;
    public bool ShowMedia { get; init; } = true;
    public bool ShowBattery { get; init; } = true;
    public string Layout { get; init; } = "Balanced";
    public double SpringStiffness { get; init; } = 420;
    public double SpringDamping { get; init; } = 38;
    public double SpringMass { get; init; } = 1;
    public double GlassOpacity { get; init; } = .98;
    public double IslandScale { get; init; } = 1;
    public bool AutoStowMaximized { get; init; } = true;
    public bool ReplaceVolumeKeys { get; init; } = true;
    public bool ReplaceBrightnessKeys { get; init; }
    public int BrightnessDownKey { get; init; }
    public int BrightnessUpKey { get; init; }
    public bool CustomDock {get;init;}
    public bool DockBrowserShortcut {get;init;}=true;
    public double DockScale {get;init;}=1;
    public List<string> DockPinnedPaths {get;init;}=[];
    public bool SpotifyWallpaper {get;init;}
    public bool TaskbarGlass { get; init; }
    public int TaskbarTintOpacity { get; init; } = 110;
    public double GlassShine { get; init; } = 1.4;

    public Preferences Validated() => this with
    {
        Theme = Theme is "Dark" or "Light" ? Theme : "Dark",
        Accent = Regex.IsMatch(Accent ?? "", "^#[0-9a-fA-F]{6}$") ? Accent! : "#76D8CB",
        BackgroundColor = ValidColor(BackgroundColor), CardColor = ValidColor(CardColor), TextColor = ValidColor(TextColor),
        Font = string.IsNullOrWhiteSpace(Font) || Font.Length > 100 ? "Segoe UI" : Font,
        TextScale = Clamp(TextScale, .85, 1.35, 1),
        OverlayWidth = Clamp(OverlayWidth, 540, 680, 560),
        Radius = Clamp(Radius, 0, 32, 20),
        Opacity = Clamp(Opacity, .85, 1, 1),
        TopOffset = Clamp(TopOffset, 0, 160, 12),
        Monitor = Math.Clamp(Monitor, 0, 15),
        Layout = Layout is "Balanced" or "Compact" ? Layout : "Balanced"
        ,SpringStiffness = new SpringConfiguration(SpringStiffness, SpringDamping, SpringMass).Validated().Stiffness
        ,SpringDamping = new SpringConfiguration(SpringStiffness, SpringDamping, SpringMass).Validated().Damping
        ,SpringMass = new SpringConfiguration(SpringStiffness, SpringDamping, SpringMass).Validated().Mass
        ,GlassOpacity = Clamp(GlassOpacity, .8, 1, .98)
        ,IslandScale = Clamp(IslandScale, .85, 1.2, 1)
        ,BrightnessDownKey=HardwareKeyPolicy.IsAssignableBrightnessKey(BrightnessDownKey)?BrightnessDownKey:0
        ,BrightnessUpKey=HardwareKeyPolicy.IsAssignableBrightnessKey(BrightnessUpKey)?BrightnessUpKey:0
        ,TaskbarTintOpacity=Math.Clamp(TaskbarTintOpacity,40,220)
        ,GlassShine=Clamp(GlassShine,.4,2,1.4)
        ,DockScale=Clamp(DockScale,.75,1.25,1)
        ,DockPinnedPaths=(DockPinnedPaths??[]).Where(DockPolicy.IsLocalLaunchPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList()
    };
    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    private static string ValidColor(string? color) => Regex.IsMatch(color ?? "", "^#[0-9a-fA-F]{6}$") ? color! : "";
}

public sealed record TaskItem(Guid Id, string Text, bool Done);
public sealed record LauncherItem(string Name, string Path);
public sealed record WorkspaceData
{
    public string Note { get; init; } = "";
    public List<TaskItem> Tasks { get; init; } = [];
    public List<string> Shelf { get; init; } = [];
    public List<LauncherItem> Launchers { get; init; } = [];
    public WorkspaceData Validated() => this with
    {
        Note = (Note ?? "")[..Math.Min((Note ?? "").Length, 100_000)],
        Tasks = (Tasks ?? []).Where(t => t is not null && !string.IsNullOrWhiteSpace(t.Text))
            .Take(100).Select(t => t with { Text = t.Text[..Math.Min(t.Text.Length, 500)] }).ToList(),
        Shelf = (Shelf ?? []).Where(p => !string.IsNullOrWhiteSpace(p) && p.Length < 32767).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList(),
        Launchers = (Launchers ?? []).Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Path) && !string.IsNullOrWhiteSpace(a.Name)).Take(30).ToList()
    };
}
