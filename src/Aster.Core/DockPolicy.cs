namespace Aster.Core;
public static class DockPolicy
{
    public static double AppsViewport(double available,double scale)=>Math.Clamp(available/Math.Clamp(scale,.75,1.25)-390,90,660);
    public static bool Eligible(bool visible,bool tool,bool cloaked,bool owned,bool shell,int width,int height)=>visible&&!tool&&!cloaked&&!owned&&!shell&&width>=80&&height>=40;
    public static bool IsLocalLaunchPath(string? path)=>!string.IsNullOrWhiteSpace(path)&&Path.IsPathFullyQualified(path)&&!path.StartsWith(@"\\",StringComparison.Ordinal)&&path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase);
}
