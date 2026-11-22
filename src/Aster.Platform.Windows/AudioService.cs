using System.Runtime.InteropServices;

namespace Aster.Platform.Windows;

public sealed record AudioReading(float Volume, bool Muted);
public static class AudioService
{
    public static IDisposable Watch(Action<AudioReading> changed)=>new Watcher(changed);
    public static AudioReading Read(bool microphone = false) => WithEndpoint(microphone, endpoint =>
    { Check(endpoint.GetMasterVolumeLevelScalar(out var volume)); Check(endpoint.GetMute(out var muted)); return new AudioReading(volume, muted); });
    public static void SetVolume(float value) => WithEndpoint(false, endpoint => { var context = Guid.Empty; Check(endpoint.SetMasterVolumeLevelScalar(Math.Clamp(value, 0, 1), ref context)); return true; });
    public static void ToggleMute(bool microphone = false) => WithEndpoint(microphone, endpoint =>
    { Check(endpoint.GetMute(out var muted)); var context = Guid.Empty; Check(endpoint.SetMute(!muted, ref context)); return true; });
    private static T WithEndpoint<T>(bool microphone, Func<IAudioEndpointVolume, T> action)
    {
        IMMDeviceEnumerator? enumerator = null; IMMDevice? device = null; object? endpoint = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)CoreAudioFactory.CreateEnumerator();
            Check(enumerator.GetDefaultAudioEndpoint(microphone ? 1 : 0, microphone ? 2 : 0, out device));
            var iid = typeof(IAudioEndpointVolume).GUID;
            Check(device.Activate(ref iid, 23, IntPtr.Zero, out endpoint));
            return action((IAudioEndpointVolume)endpoint);
        }
        finally
        {
            if (endpoint is not null) Marshal.ReleaseComObject(endpoint);
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }
    private static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify([MarshalAs(UnmanagedType.Interface)] IEndpointVolumeCallback notify);
        [PreserveSig] int UnregisterControlChangeNotify([MarshalAs(UnmanagedType.Interface)] IEndpointVolumeCallback notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
    [ComVisible(true),Guid("657804FA-D6AD-4496-8A60-352752AF4F89"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEndpointVolumeCallback{[PreserveSig]int OnNotify(IntPtr data);}
    [ComVisible(true),ClassInterface(ClassInterfaceType.None)]
    private sealed class Callback(Action<AudioReading> changed):IEndpointVolumeCallback
    {
        public int OnNotify(IntPtr data)
        {
            try{var n=Marshal.PtrToStructure<Notification>(data);changed(new(n.Volume,n.Muted!=0));}catch{/* Never throw across the COM callback boundary. */}return 0;
        }
    }
    [StructLayout(LayoutKind.Sequential)]private struct Notification{public Guid Context;public int Muted;public float Volume;public uint Channels;}
    private sealed class Watcher:IDisposable
    {
        private IMMDeviceEnumerator? enumerator;private IMMDevice? device;private object? endpoint;private readonly Callback callback;
        public Watcher(Action<AudioReading> changed)
        {
            callback=new(changed);
            try{enumerator=(IMMDeviceEnumerator)CoreAudioFactory.CreateEnumerator();Check(enumerator.GetDefaultAudioEndpoint(0,0,out device));var iid=typeof(IAudioEndpointVolume).GUID;Check(device.Activate(ref iid,23,IntPtr.Zero,out endpoint));Check(((IAudioEndpointVolume)endpoint).RegisterControlChangeNotify(callback));}
            catch{Dispose();throw;}
        }
        public void Dispose()
        {
            if(endpoint is IAudioEndpointVolume volume){volume.UnregisterControlChangeNotify(callback);Marshal.ReleaseComObject(endpoint);endpoint=null;}
            if(device is not null){Marshal.ReleaseComObject(device);device=null;}if(enumerator is not null){Marshal.ReleaseComObject(enumerator);enumerator=null;}
        }
    }
}
