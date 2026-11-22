using System.Runtime.InteropServices;

namespace Aster.Platform.Windows;

/// <summary>Best-effort native shell material; undocumented Windows API, no injection or registry edits.</summary>
public sealed class TaskbarMaterialService : IDisposable
{
    private readonly Dictionary<IntPtr,AccentPolicy> originals=[];
    private readonly Dictionary<IntPtr,int> backdropOriginals=[];
    public string Status {get;private set;}="Windows taskbar unchanged";
    public int StyledCount {get;private set;}
    public bool RestoreSucceeded {get;private set;}=true;
    public bool CanTint {get;private set;}
    public bool Apply(int opacity)
    {
        var styled=0;CanTint=false;
        try
        {
            foreach(var hwnd in Windows())
            {
                if(!originals.ContainsKey(hwnd))
                {
                    var previous=new AccentPolicy();
                    if(!Read(hwnd,ref previous))
                    {
                        // Windows 11's supported compositor backdrop is a second, independently restorable path.
                        if(TryBackdrop(hwnd))styled++;
                        continue;
                    }
                    originals[hwnd]=previous;
                }
                // ABGR tint. Effects are accepted by Windows or the service reports failure.
                var policy=new AccentPolicy{State=4,Flags=0,Color=unchecked((uint)(Math.Clamp(opacity,40,220)<<24))|0x00241B13};
                if(Write(hwnd,policy)){styled++;CanTint=true;}else if(TryBackdrop(hwnd)){originals.Remove(hwnd);styled++;}
            }
        }
        catch(EntryPointNotFoundException){Status="Taskbar material API unavailable on this Windows build";return false;}
        StyledCount=styled;
        Status=styled>0?(CanTint?$"Accent requested on {styled} taskbar(s); appearance is build-dependent":"Windows backdrop requested; Windows controls the tint and final appearance"):"Windows declined the taskbar material";
        return styled>0;
    }
    public void Restore()
    {
        RestoreSucceeded=true;
        foreach(var entry in originals)if(IsWindow(entry.Key))try{RestoreSucceeded &= Write(entry.Key,entry.Value);}catch(EntryPointNotFoundException){RestoreSucceeded=false;}
        foreach(var entry in backdropOriginals)if(IsWindow(entry.Key)){var value=entry.Value;RestoreSucceeded &= DwmSetWindowAttribute(entry.Key,38,ref value,sizeof(int))==0;}
        backdropOriginals.Clear();
        originals.Clear();StyledCount=0;Status=RestoreSucceeded?"Original Windows taskbar material restored":"Windows could not confirm taskbar restoration";
    }
    private static IEnumerable<IntPtr> Windows()
    {
        var main=FindWindow("Shell_TrayWnd",null);if(main!=IntPtr.Zero)yield return main;
        var next=IntPtr.Zero;while((next=FindWindowEx(IntPtr.Zero,next,"Shell_SecondaryTrayWnd",null))!=IntPtr.Zero)yield return next;
    }
    private static bool Read(IntPtr hwnd,ref AccentPolicy policy)
    {
        var ptr=Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
        try{Marshal.StructureToPtr(policy,ptr,false);var data=new CompositionData{Attribute=19,Data=ptr,Size=(nuint)Marshal.SizeOf<AccentPolicy>()};var ok=GetWindowCompositionAttribute(hwnd,ref data);if(ok)policy=Marshal.PtrToStructure<AccentPolicy>(ptr);return ok;}finally{Marshal.FreeHGlobal(ptr);}
    }
    private static bool Write(IntPtr hwnd,AccentPolicy policy)
    {
        var ptr=Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
        try{Marshal.StructureToPtr(policy,ptr,false);var data=new CompositionData{Attribute=19,Data=ptr,Size=(nuint)Marshal.SizeOf<AccentPolicy>()};return SetWindowCompositionAttribute(hwnd,ref data);}finally{Marshal.FreeHGlobal(ptr);}
    }
    public void Dispose()=>Restore();
    private bool TryBackdrop(IntPtr hwnd)
    {
        if(!backdropOriginals.ContainsKey(hwnd))
        {if(DwmGetWindowAttribute(hwnd,38,out var original,sizeof(int))!=0)return false;backdropOriginals[hwnd]=original;}
        var acrylic=3;return DwmSetWindowAttribute(hwnd,38,ref acrylic,sizeof(int))==0;
    }
    [StructLayout(LayoutKind.Sequential)]private struct AccentPolicy{public int State,Flags;public uint Color;public int Animation;}
    [StructLayout(LayoutKind.Sequential)]private struct CompositionData{public int Attribute;public IntPtr Data;public nuint Size;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindow(string name,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string name,string? title);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetWindowCompositionAttribute(IntPtr hwnd,ref CompositionData data);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowCompositionAttribute(IntPtr hwnd,ref CompositionData data);
    [DllImport("dwmapi.dll")]private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int bytes);
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int bytes);
}
