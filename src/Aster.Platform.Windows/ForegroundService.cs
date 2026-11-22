using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Aster.Platform.Windows;

public sealed record ForegroundReading(bool OwnWindow,bool ShellWindow,bool Maximized,bool Fullscreen,bool FrontmostFallback=false,bool AnyApplicationVisible=true);
public static class ForegroundService
{
    public static ForegroundReading Read(IntPtr island)
    {
        var any=FrontmostApplication(MonitorFromWindow(island,2))!=IntPtr.Zero;var hwnd=GetForegroundWindow();var fallback=false;
        if(hwnd==IntPtr.Zero){hwnd=FrontmostApplication();fallback=true;}
        if(hwnd==IntPtr.Zero)return new(false,true,false,false,fallback,any);
        GetWindowThreadProcessId(hwnd,out var pid);var own=pid==Environment.ProcessId;
        var name=new StringBuilder(96);GetClassName(hwnd,name,name.Capacity);
        var shell=name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW";
        var maximized=IsZoomed(hwnd);var fullscreen=false;
        if(!own&&!shell&&GetWindowRect(hwnd,out var r))
        {
            var target=MonitorFromWindow(island,2);var current=MonitorFromWindow(hwnd,2);
            if(target==current)
            {
                var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
                if(GetMonitorInfo(current,ref info))fullscreen=r.Left<=info.Monitor.Left+2&&r.Top<=info.Monitor.Top+2&&r.Right>=info.Monitor.Right-2&&r.Bottom>=info.Monitor.Bottom-2;
            }
            else maximized=false;
        }
        return new(own,shell,maximized,fullscreen,fallback,any);
    }
    private static IntPtr FrontmostApplication(IntPtr monitor=default)
    {
        // During activation hand-offs Windows can temporarily report no foreground window.
        // Use z-order only in that case; ignore shell, tool, cloaked and our own windows.
        for(var hwnd=GetTopWindow(IntPtr.Zero);hwnd!=IntPtr.Zero;hwnd=GetWindow(hwnd,2))
        {
            if(!IsWindowVisible(hwnd)||IsIconic(hwnd)||(monitor!=IntPtr.Zero&&MonitorFromWindow(hwnd,2)!=monitor))continue;GetWindowThreadProcessId(hwnd,out var pid);if(pid==Environment.ProcessId)continue;
            var style=GetWindowLongPtr(hwnd,-20).ToInt64();if((style&(0x80|0x08000000))!=0)continue;
            var name=new StringBuilder(96);GetClassName(hwnd,name,name.Capacity);
            if(name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW")continue;
            if(DwmGetWindowAttribute(hwnd,14,out var cloaked,sizeof(int))==0&&cloaked!=0)continue;
            if(!GetWindowRect(hwnd,out var rect)||rect.Right-rect.Left<300||rect.Bottom-rect.Top<200)continue;
            return hwnd;
        }
        return IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{public int Size;public Rect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern IntPtr GetTopWindow(IntPtr parent);
    [DllImport("user32.dll")]private static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("dwmapi.dll")]private static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int bytes);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(IntPtr hwnd,StringBuilder name,int count);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
}
