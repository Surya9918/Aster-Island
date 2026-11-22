using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace Aster.Platform.Windows;
public sealed record NativeTaskbarState(uint State,bool Visible);
public sealed record DockLease(Guid Id,int Parent,NativeTaskbarState Original);
public static class NativeTaskbar
{
    public static IEnumerable<IntPtr> Windows()
    {var main=FindWindow("Shell_TrayWnd",null);if(main!=IntPtr.Zero)yield return main;var p=IntPtr.Zero;while((p=FindWindowEx(IntPtr.Zero,p,"Shell_SecondaryTrayWnd",null))!=IntPtr.Zero)yield return p;}
    public static NativeTaskbarState Capture(){var data=Data();return new((uint)SHAppBarMessage(4,ref data).ToUInt64(),Windows().Any(IsWindowVisible));}
    public static void Hide(){foreach(var h in Windows())ShowWindow(h,0);}
    public static void Show(){foreach(var h in Windows())ShowWindow(h,5);}
    public static NativeRect WorkingArea(IntPtr window)
    {var monitor=MonitorFromWindow(window,2);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(!GetMonitorInfo(monitor,ref info))throw new InvalidOperationException();return info.Work;}
    public static bool Hidden=>Windows().All(h=>!IsWindowVisible(h));
    public static void SetState(uint state){var data=Data();data.Parameter=(IntPtr)state;SHAppBarMessage(10,ref data);}
    public static void Restore(NativeTaskbarState state){SetState(state.State);if(state.Visible)Show();else Hide();}
    public static void OpenStart(){var hwnd=FindWindow("Shell_TrayWnd",null);if(hwnd==IntPtr.Zero||!PostMessage(hwnd,0x0112,(IntPtr)0xF130,IntPtr.Zero))throw new InvalidOperationException("Windows Start is unavailable.");}
    public static void OpenNotifications()=>Launch("ms-actioncenter:");
    public static void Launch(string target)=>Process.Start(new ProcessStartInfo(target){UseShellExecute=true});
    public static async Task GuardAsync(int parent,string file)
    {
        DockLease? lease;try{lease=JsonSerializer.Deserialize<DockLease>(File.ReadAllText(file));}catch{return;}if(lease is null||lease.Parent!=parent)return;
        using var cancellation=new CancellationTokenSource();using var watcher=new FileSystemWatcher(Path.GetDirectoryName(file)!,Path.GetFileName(file));
        var changed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check(){try{if(!File.Exists(file)||JsonSerializer.Deserialize<DockLease>(File.ReadAllText(file))?.Id!=lease.Id)changed.TrySetResult();}catch(IOException){}}
        watcher.Deleted+=(_,_)=>Check();watcher.Changed+=(_,_)=>Check();watcher.Renamed+=(_,_)=>Check();watcher.EnableRaisingEvents=true;
        try{using var process=Process.GetProcessById(parent);await Task.WhenAny(process.WaitForExitAsync(cancellation.Token),changed.Task);}
        catch(ArgumentException){}finally{cancellation.Cancel();}
        try{if(File.Exists(file)&&JsonSerializer.Deserialize<DockLease>(File.ReadAllText(file))?.Id==lease.Id){Restore(lease.Original);File.Delete(file);}}catch{Show();}
    }
    public static void Recover(string file)
    {if(!File.Exists(file))return;var lease=JsonSerializer.Deserialize<DockLease>(File.ReadAllText(file));if(lease is not null)Restore(lease.Original);File.Delete(file);}
    internal static AppBarData Data(IntPtr window=default)=>new(){Size=(uint)Marshal.SizeOf<AppBarData>(),Window=window==IntPtr.Zero?FindWindow("Shell_TrayWnd",null):window};
    [StructLayout(LayoutKind.Sequential)]internal struct AppBarData{public uint Size;public IntPtr Window;public uint Callback,Edge;public NativeRect Rect;public IntPtr Parameter;}
    [StructLayout(LayoutKind.Sequential)]public struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{public int Size;public NativeRect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr h,uint flags);
    [DllImport("user32.dll")]private static extern bool GetMonitorInfo(IntPtr h,ref MonitorInfo info);
    [DllImport("shell32.dll")]internal static extern UIntPtr SHAppBarMessage(uint message,ref AppBarData data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindow(string name,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string name,string? title);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
}
public sealed class DockAppBar:IDisposable
{
    private IntPtr window;private NativeTaskbar.NativeRect? last;
    public const int Notification=0x801A;
    public void Register(IntPtr hwnd){window=hwnd;var data=NativeTaskbar.Data(hwnd);data.Callback=Notification;if(NativeTaskbar.SHAppBarMessage(0,ref data)==UIntPtr.Zero)throw new InvalidOperationException("Dock reservation unavailable.");}
    public void Reserve(int left,int top,int right,int bottom,int height)
    {
        if(window==IntPtr.Zero)return;var data=NativeTaskbar.Data(window);data.Edge=3;data.Rect=new(){Left=left,Top=bottom-height,Right=right,Bottom=bottom};NativeTaskbar.SHAppBarMessage(2,ref data);data.Rect.Top=data.Rect.Bottom-height;
        if(last is { } r&&r.Left==data.Rect.Left&&r.Top==data.Rect.Top&&r.Right==data.Rect.Right&&r.Bottom==data.Rect.Bottom)return;
        NativeTaskbar.SHAppBarMessage(3,ref data);last=data.Rect;
    }
    public void Dispose(){if(window==IntPtr.Zero)return;var data=NativeTaskbar.Data(window);NativeTaskbar.SHAppBarMessage(1,ref data);window=IntPtr.Zero;last=null;}
}
