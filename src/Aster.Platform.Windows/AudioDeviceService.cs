using System.Runtime.InteropServices;
using Aster.Core;
namespace Aster.Platform.Windows;
public static class AudioDeviceService
{
 public static IDisposable Watch(Action changed)=>new Subscription(changed);
 public static IReadOnlyList<OutputEndpoint> Read()
 {
  Enumerator? owner=null;ICollection? devices=null;IDevice? current=null;var result=new List<OutputEndpoint>();
  try{owner=(Enumerator)new DeviceEnumerator();string selected="";if(owner.GetDefaultAudioEndpoint(0,0,out current)>=0){Check(current.GetId(out selected));Marshal.ReleaseComObject(current);current=null;}
   Check(owner.EnumAudioEndpoints(0,15,out devices));Check(devices.GetCount(out uint count));
   for(uint i=0;i<count;i++){IDevice? device=null;IProperties? props=null;try{
    Check(devices.Item(i,out device));Check(device.GetId(out var id));Check(device.GetState(out uint state));Check(device.OpenPropertyStore(0,out props));
    var name=ReadProperty(props,new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),14));var form=ReadProperty(props,new(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"),0));
    result.Add(new(id,name as string??"Audio output",state==1,form is uint n&&(n==3||n==5),id==selected));
   }finally{if(props is not null)Marshal.ReleaseComObject(props);if(device is not null)Marshal.ReleaseComObject(device);}}
   return result;
  }finally{if(current is not null)Marshal.ReleaseComObject(current);if(devices is not null)Marshal.ReleaseComObject(devices);if(owner is not null)Marshal.ReleaseComObject(owner);}
 }
 static object? ReadProperty(IProperties props,Key key){if(props.GetValue(ref key,out var v)<0)return null;try{return v.Type==31?Marshal.PtrToStringUni(v.Pointer):v.Type==19?v.Number:null;}finally{PropVariantClear(ref v);}}
 static void Check(int hr)=>Marshal.ThrowExceptionForHR(hr);
 [DllImport("ole32.dll")]static extern int PropVariantClear(ref Variant v);
 [StructLayout(LayoutKind.Sequential)]public struct Key(Guid format,uint id){public Guid Format=format;public uint Id=id;}
 [StructLayout(LayoutKind.Explicit,Size=24)]public struct Variant{[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public IntPtr Pointer;[FieldOffset(8)]public uint Number;}
 [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]class DeviceEnumerator{}
 [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Enumerator{
 [PreserveSig]int EnumAudioEndpoints(int flow,uint state,out ICollection devices);[PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);[PreserveSig]int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IDevice device);[PreserveSig]int RegisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)]INotify callback);[PreserveSig]int UnregisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)]INotify callback);}
 [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ICollection{[PreserveSig]int GetCount(out uint count);[PreserveSig]int Item(uint index,out IDevice device);}
 [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDevice{[PreserveSig]int Activate(ref Guid id,uint context,IntPtr args,out IntPtr result);[PreserveSig]int OpenPropertyStore(uint access,out IProperties properties);[PreserveSig]int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);[PreserveSig]int GetState(out uint state);}
 [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IProperties{[PreserveSig]int GetCount(out uint n);[PreserveSig]int GetAt(uint i,out Key key);[PreserveSig]int GetValue(ref Key key,out Variant value);[PreserveSig]int SetValue(ref Key key,ref Variant v);[PreserveSig]int Commit();}
 [ComVisible(true),Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]public interface INotify{[PreserveSig]int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)]string id,uint state);[PreserveSig]int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)]string id);[PreserveSig]int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)]string id);[PreserveSig]int OnDefaultDeviceChanged(int flow,int role,[MarshalAs(UnmanagedType.LPWStr)]string? id);[PreserveSig]int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)]string id,Key key);}
 [ComVisible(true),ClassInterface(ClassInterfaceType.None)]sealed class Callback(Action changed):INotify{
 int Signal(){try{changed();}catch{}return 0;}public int OnDeviceStateChanged(string id,uint state)=>Signal();public int OnDeviceAdded(string id)=>Signal();public int OnDeviceRemoved(string id)=>Signal();public int OnDefaultDeviceChanged(int flow,int role,string? id)=>flow==0?Signal():0;public int OnPropertyValueChanged(string id,Key key)=>Signal();}
 sealed class Subscription:IDisposable{Enumerator? owner;readonly Callback callback;public Subscription(Action changed){callback=new(changed);try{owner=(Enumerator)new DeviceEnumerator();Check(owner.RegisterEndpointNotificationCallback(callback));}catch{Dispose();throw;}}public void Dispose(){if(owner is null)return;owner.UnregisterEndpointNotificationCallback(callback);Marshal.ReleaseComObject(owner);owner=null;GC.KeepAlive(callback);}}
}

