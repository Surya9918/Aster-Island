using System.Runtime.InteropServices;
using Aster.Core;

namespace Aster.Platform.Windows;

/// <summary>Only media/explicitly mapped non-text keys are consumed. No key log or DLL injection.</summary>
public sealed class HardwareKeys:IDisposable
{
    private readonly HookProc callback;
    private readonly HashSet<int> swallowed=[];
    private IntPtr hook;
    private int handled;
    public int HandledCount=>Volatile.Read(ref handled);
    public Func<HardwareAction,bool>? CanHandle {get;set;}
    public Action<HardwareAction>? ActionRequested {get;set;}
    public bool VolumeEnabled {get;set;}
    public bool BrightnessEnabled {get;set;}
    public int DimKey {get;set;}
    public int BrightenKey {get;set;}
    public bool Learning {get;set;}
    public event Action<int>? KeyLearned;
    public bool Active=>hook!=IntPtr.Zero;
    public HardwareKeys()
    {
        callback=Filter;hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
        if(hook==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
    }
    private IntPtr Filter(int code,IntPtr message,IntPtr data)
    {
        if(code<0)return CallNextHookEx(hook,code,message,data);
        // Read only the virtual-key field first. Unrelated keystrokes are immediately forwarded.
        var key=Marshal.ReadInt32(data);var down=message.ToInt64() is 0x100 or 0x104;var up=message.ToInt64() is 0x101 or 0x105;
        try
        {
            if(up&&swallowed.Remove(key))return new IntPtr(1);
            if(!down)return CallNextHookEx(hook,code,message,data);
            if(Learning&&HardwareKeyPolicy.IsAssignableBrightnessKey(key))
            {Learning=false;swallowed.Add(key);KeyLearned?.Invoke(key);return new IntPtr(1);}
            var controlAlt=(GetAsyncKeyState(0x11)&0x8000)!=0&&(GetAsyncKeyState(0x12)&0x8000)!=0;
            var action=HardwareKeyPolicy.Resolve(key,VolumeEnabled,BrightnessEnabled,DimKey,BrightenKey,controlAlt);
            if(action is { } value&&CanHandle?.Invoke(value)==true)
            {
                swallowed.Add(key);ActionRequested?.Invoke(value);Interlocked.Increment(ref handled);return new IntPtr(1);
            }
        }
        catch{/* Fail open: Windows keeps handling input if Aster cannot enqueue the request. */}
        return CallNextHookEx(hook,code,message,data);
    }
    public void Dispose(){if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}swallowed.Clear();}
    private delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int type,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool UnhookWindowsHookEx(IntPtr handle);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr handle,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? module);
}
