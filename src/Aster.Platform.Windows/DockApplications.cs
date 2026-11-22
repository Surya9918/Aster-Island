using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Aster.Core;
namespace Aster.Platform.Windows;
public sealed record DockApplication(IntPtr Window,string Name,string Path,IntPtr Icon,bool Active);
public static class DockApplications
{
    public static IReadOnlyList<DockApplication> Read()
    {
        var result=new List<DockApplication>();var active=GetForegroundWindow();EnumWindows((h,_)=>
        {
            if(result.Count>=60)return false;GetWindowThreadProcessId(h,out var pid);if(pid==(uint)Environment.ProcessId)return true;
            var style=GetWindowLongPtr(h,-20).ToInt64();var cls=new StringBuilder(96);GetClassName(h,cls,cls.Capacity);var shell=cls.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW";
            DwmGetWindowAttribute(h,14,out var cloaked,sizeof(int));GetWindowRect(h,out var rect);
            if(!DockPolicy.Eligible(IsWindowVisible(h),(style&0x80)!=0,cloaked!=0,GetWindow(h,4)!=IntPtr.Zero&&(style&0x40000)==0,shell,IsIconic(h)?800:rect.Right-rect.Left,IsIconic(h)?600:rect.Bottom-rect.Top))return true;
            string path="",name="Application";try{using var p=Process.GetProcessById((int)pid);name=p.ProcessName;}catch{}
            var process=OpenProcess(0x1000,false,pid);if(process!=IntPtr.Zero){try{var text=new StringBuilder(32768);var size=text.Capacity;if(QueryFullProcessImageName(process,0,text,ref size))path=text.ToString();}finally{CloseHandle(process);}}
            if(!string.IsNullOrEmpty(path)){try{name=FileVersionInfo.GetVersionInfo(path).FileDescription??name;}catch{}}
            var caption=new StringBuilder(256);if(name.Contains("Edge",StringComparison.OrdinalIgnoreCase)){GetWindowText(h,caption,caption.Capacity);if(caption.ToString().Contains("Spotify",StringComparison.OrdinalIgnoreCase))name="Spotify";}
            var icon=IntPtr.Zero;SendMessageTimeout(h,0x7F,(IntPtr)2,IntPtr.Zero,2,25,out icon);if(icon==IntPtr.Zero)SendMessageTimeout(h,0x7F,(IntPtr)1,IntPtr.Zero,2,25,out icon);if(icon==IntPtr.Zero)icon=GetClassLongPtr(h,-34);
            result.Add(new(h,name,path,icon,h==active));return true;
        },IntPtr.Zero);return result;
    }
    public static void Activate(IntPtr h){if(!IsWindow(h))return;if(GetForegroundWindow()==h){ShowWindow(h,6);return;}if(IsIconic(h))ShowWindow(h,9);SetForegroundWindow(h);}
    public static bool Exists(IntPtr h)=>IsWindow(h);
    private delegate bool EnumCallback(IntPtr h,IntPtr p);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumCallback callback,IntPtr p);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")]private static extern IntPtr GetWindowLongPtr(IntPtr h,int i);
    [DllImport("user32.dll")]private static extern IntPtr GetClassLongPtr(IntPtr h,int i);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(IntPtr h,StringBuilder name,int n);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(IntPtr h,StringBuilder text,int max);
    [DllImport("user32.dll")]private static extern IntPtr GetWindow(IntPtr h,uint command);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr h,out NativeTaskbar.NativeRect rect);
    [DllImport("dwmapi.dll")]private static extern int DwmGetWindowAttribute(IntPtr h,int attr,out int value,int size);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")]private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")]private static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")]private static extern bool ShowWindow(IntPtr h,int command);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
    [DllImport("kernel32.dll")]private static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern bool QueryFullProcessImageName(IntPtr h,uint flags,StringBuilder name,ref int n);
    [DllImport("kernel32.dll")]private static extern bool CloseHandle(IntPtr h);
}
