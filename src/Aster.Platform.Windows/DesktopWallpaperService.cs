using System.Runtime.InteropServices;
namespace Aster.Platform.Windows;

public sealed record MonitorWallpaper(string Monitor,string Path);
public sealed record WallpaperSnapshot(MonitorWallpaper[] Monitors,uint Color,int Position,uint State,string[] Slides,uint Options,uint Interval);
public interface IWallpaperBackend
{
    WallpaperSnapshot Capture();
    void Apply(string path);
    bool Restore(WallpaperSnapshot original,IReadOnlyCollection<string> ownedPaths);
    bool Owns(IReadOnlyCollection<string> paths);
}

/// <summary>Windows-owned wallpaper rendering. No Explorer hooks, injection, desktop capture or network.</summary>
public sealed class DesktopWallpaperService:IWallpaperBackend
{
    private static IDesktopWallpaper Open()=>(IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"),true)!)!;
    public WallpaperSnapshot Capture()
    {
        var api=Open();try
        {
            api.GetMonitorDevicePathCount(out var count);var monitors=new List<MonitorWallpaper>();
            for(uint i=0;i<count;i++){api.GetMonitorDevicePathAt(i,out var id);api.GetWallpaper(id,out var path);monitors.Add(new(id,path??""));}
            api.GetBackgroundColor(out var color);api.GetPosition(out var position);api.GetStatus(out var state);
            api.GetSlideshowOptions(out var options,out var interval);var slides=new List<string>();
            if((state&2)!=0)
            {
                api.GetSlideshow(out var array);try{array.GetCount(out var total);if(total>200)throw new InvalidOperationException("Slideshow is too large to preserve safely.");
                    for(uint i=0;i<total;i++){array.GetItemAt(i,out var item);try{item.GetDisplayName(0x80058000,out var file);slides.Add(file);}finally{Marshal.ReleaseComObject(item);}}}
                finally{Marshal.ReleaseComObject(array);}
            }
            return new(monitors.ToArray(),color,position,state,slides.ToArray(),options,interval);
        }
        finally{Marshal.ReleaseComObject(api);}
    }
    public void Apply(string path)
    {
        if(!File.Exists(path))throw new FileNotFoundException();var api=Open();try{api.SetPosition(4);api.SetWallpaper(null,path);}finally{Marshal.ReleaseComObject(api);}
    }
    public bool Owns(IReadOnlyCollection<string> paths)
    {var current=Capture();return current.Monitors.Length>0&&current.Monitors.All(m=>paths.Contains(m.Path,StringComparer.OrdinalIgnoreCase));}
    public bool Restore(WallpaperSnapshot original,IReadOnlyCollection<string> ownedPaths)
    {
        var current=Capture();var allOwned=current.Monitors.Length>0&&current.Monitors.All(m=>ownedPaths.Contains(m.Path,StringComparer.OrdinalIgnoreCase));
        var api=Open();try
        {
            foreach(var m in current.Monitors.Where(m=>ownedPaths.Contains(m.Path,StringComparer.OrdinalIgnoreCase)))
            {var before=original.Monitors.FirstOrDefault(o=>o.Monitor==m.Monitor);if(before is not null)api.SetWallpaper(m.Monitor,before.Path);}
            if(allOwned)
            {
                api.SetPosition(original.Position);api.SetBackgroundColor(original.Color);
                if(original.Slides.Length>0)
                {
                    var ids=new List<IntPtr>();try
                    {
                        foreach(var file in original.Slides){Marshal.ThrowExceptionForHR(SHParseDisplayName(file,IntPtr.Zero,out var id,0,out _));ids.Add(id);}
                        Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)ids.Count,ids.ToArray(),out var items));
                        try{api.SetSlideshow(items);api.SetSlideshowOptions(original.Options,original.Interval);}finally{Marshal.ReleaseComObject(items);}
                    }
                    finally{foreach(var id in ids)Marshal.FreeCoTaskMem(id);}
                }
                if((original.State&1)==0)api.Enable(false);
            }
        }
        finally{Marshal.ReleaseComObject(api);}
        return !Capture().Monitors.Any(m=>ownedPaths.Contains(m.Path,StringComparer.OrdinalIgnoreCase));
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern int SHParseDisplayName(string name,IntPtr bind,out IntPtr id,uint attrs,out uint returned);
    [DllImport("shell32.dll")]private static extern int SHCreateShellItemArrayFromIDLists(uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]IntPtr[] ids,out IShellItemArray items);
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string? monitor,[MarshalAs(UnmanagedType.LPWStr)]string path);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string? monitor,[MarshalAs(UnmanagedType.LPWStr)]out string path);
        void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)]out string id);
        void GetMonitorDevicePathCount(out uint count);
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)]string id,out NativeRect rect);
        void SetBackgroundColor(uint color);void GetBackgroundColor(out uint color);
        void SetPosition(int position);void GetPosition(out int position);
        void SetSlideshow(IShellItemArray items);void GetSlideshow(out IShellItemArray items);
        void SetSlideshowOptions(uint options,uint interval);void GetSlideshowOptions(out uint options,out uint interval);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)]string? monitor,int direction);
        void GetStatus(out uint state);void Enable([MarshalAs(UnmanagedType.Bool)]bool enabled);
    }
    [ComImport,Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IShellItemArray
    {
        void BindToHandler(IntPtr a,ref Guid b,ref Guid c,out IntPtr d);void GetPropertyStore(int a,ref Guid b,out IntPtr c);void GetPropertyDescriptionList(IntPtr a,ref Guid b,out IntPtr c);void GetAttributes(int a,uint b,out uint c);
        void GetCount(out uint count);void GetItemAt(uint index,out IShellItem item);void EnumItems(out IntPtr items);
    }
    [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IShellItem
    {
        void BindToHandler(IntPtr a,ref Guid b,ref Guid c,out IntPtr d);void GetParent(out IShellItem parent);
        void GetDisplayName(uint kind,[MarshalAs(UnmanagedType.LPWStr)]out string name);void GetAttributes(uint mask,out uint value);void Compare(IShellItem other,uint hint,out int order);
    }
}
