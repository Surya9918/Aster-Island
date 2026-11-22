using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aster.Core;
using Aster.Platform.Windows;

namespace Aster.App;

internal static class DeviceIntegrationProbe
{
    public static async Task RunAsync(AppModel model,OverlayWindow island,DesktopIntegration integration,string path)
    {
        var report=new Dictionary<string,object?>();var volume=AudioService.Read();int? brightness;
        try{brightness=await Task.Run(BrightnessService.Read);}catch{brightness=null;report["brightnessProvider"]="unavailable";}
        try
        {
            await Task.Delay(2400);integration.RecoverDeviceIntegrations();await Task.Delay(2400);
            report["audioReadinessRecovered"]=integration.VolumeControlReady;
            report["brightnessReadinessRecovered"]=brightness is null||integration.BrightnessControlReady;
            island.OpenIsland();await Task.Delay(500);
            var external=Math.Clamp(volume.Volume+(volume.Volume>.96?-.02f:.02f),0,1);AudioService.SetVolume(external);await Task.Delay(350);
            report["externalVolumeCallbackObserved"]=Math.Abs(model.Volume-Math.Round(external*100))<1;
            AudioService.SetVolume(volume.Volume);await Task.Delay(350);
            var before=integration.HandledKeyCount;var direction=volume.Volume>.96?0xAE:0xAF;
            Send((ushort)direction,false);Send((ushort)direction,true);await Task.Delay(650);
            var changed=AudioService.Read();report["volumeKeyIntercepted"]=integration.HandledKeyCount>before;
            report["volumeActuallyChanged"]=Math.Abs(changed.Volume-volume.Volume)>.005;
            report["volumeHudShown"]=island.Machine.Target==IslandState.SystemHud&&island.Machine.HudKind==SystemHudKind.Volume;
            Capture(island,Path.ChangeExtension(path,"volume.png"));
            if(brightness is { } initial)
            {
                var value=Math.Clamp(initial+(initial>=95?-5:5),0,100);await Task.Run(()=>BrightnessService.Set(value));await Task.Delay(650);
                report["brightnessActuallyChanged"]=BrightnessService.Read()==value;
                report["brightnessEventObserved"]=model.Brightness==value;
                report["brightnessHudShown"]=island.Machine.Target==IslandState.SystemHud&&island.Machine.HudKind==SystemHudKind.Brightness;
                Capture(island,Path.ChangeExtension(path,"brightness.png"));
                var keyCount=integration.HandledKeyCount;ushort arrow=(ushort)(value>=95?0x28:0x26);
                try{Send(0x11,false);Send(0x12,false);Send(arrow,false);Send(arrow,true);}finally{Send(0x12,true);Send(0x11,true);}
                await Task.Delay(650);report["brightnessShortcutIntercepted"]=integration.HandledKeyCount>keyCount;
                report["brightnessShortcutHudShown"]=island.Machine.Target==IslandState.SystemHud&&island.Machine.HudKind==SystemHudKind.Brightness;

            }
            report["brightnessHardwareSuppression"]="Firmware keys require learning/on-device verification; no global suppression claim";
            report["hardwareStatus"]=model.HardwareStatus;
        }
        finally
        {
            AudioService.SetVolume(volume.Volume);
            if(AudioService.Read().Muted!=volume.Muted)AudioService.ToggleMute();
            if(brightness is { } initial)await Task.Run(()=>BrightnessService.Set(initial));
        }
        report["originalVolumeRestored"]=Math.Abs(AudioService.Read().Volume-volume.Volume)<.001;
        report["originalBrightnessRestored"]=brightness is null||BrightnessService.Read()==brightness;
        island.Machine.Expand(IslandPage.Controls);await Task.Delay(900);Capture(island,Path.ChangeExtension(path,"controls.png"));
        island.Machine.Select(IslandPage.Today);await Task.Delay(800);Capture(island,Path.ChangeExtension(path,"today.png"));
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void Capture(OverlayWindow window,string path)
    {var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);}
    private static void Send(ushort key,bool up)
    {var input=new Input{Type=1,Data=new InputUnion{Keyboard=new Keyboard{Key=key,Flags=up?2u:0}}};if(SendInput(1,new[]{input},Marshal.SizeOf<Input>())!=1)throw new System.ComponentModel.Win32Exception();}
    [StructLayout(LayoutKind.Sequential)]private struct Input{public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Explicit)]private struct InputUnion{[FieldOffset(0)]public Keyboard Keyboard;[FieldOffset(0)]public Mouse Mouse;}
    [StructLayout(LayoutKind.Sequential)]private struct Keyboard{public ushort Key,Scan;public uint Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct Mouse{public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
}
