namespace Aster.Core;
/// <summary>Reset only presentation; preserve permissions, device mappings and unrelated activity choices.</summary>
public static class AppearanceDefaults
{
 public static Preferences Apply(Preferences p)=>p with{Theme="Dark",Accent="#E6E6E6",BackgroundColor="",CardColor="",TextColor="",Font="Segoe UI",TextScale=1,OverlayWidth=560,CompactWidth=224,Radius=20,Opacity=1,TopOffset=12,ReducedMotion=false,SpringStiffness=420,SpringDamping=38,SpringMass=1,GlassOpacity=.98,IslandScale=1,GlassShine=1.4,EyeScale=1,EyeSpeed=1,NightDim=.14};
}
