using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Threading;
using Aster.Core;
using Aster.Platform.Windows;

namespace Aster.App;

/// <summary>Owns desktop subscriptions and their lifetime. Hook callbacks only enqueue work.</summary>
public sealed class DesktopIntegration:IDisposable
{
    private readonly StartupReveal launchReveal=new(new SystemClock());
    private readonly AppModel model;private readonly OverlayWindow island;private readonly Dispatcher dispatcher;
    private readonly Channel<HardwareAction> actions=Channel.CreateBounded<HardwareAction>(new BoundedChannelOptions(48){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true});
    private readonly CancellationTokenSource stop=new();private readonly TaskbarMaterialService taskbar=new();
    private readonly DispatcherTimer foreground,learningTimeout,deviceHealth;
    private IDisposable? outputDevices;private volatile bool rebindAudio=true;private bool healthBusy;
    private SpotifyWallpaperController? wallpaper;
    private HardwareKeys? keys;private IDisposable? audio,brightness;
    private bool learningUp;private volatile bool disposed,volumeReady,brightnessReady;private int taskbarTicks;
    public int HandledKeyCount=>keys?.HandledCount??0;
    public bool VolumeControlReady=>volumeReady;public bool BrightnessControlReady=>brightnessReady;
    public DesktopIntegration(AppModel model,OverlayWindow island)
    {
        this.model=model;this.island=island;dispatcher=Dispatcher.CurrentDispatcher;
        foreground=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};foreground.Tick+=(_,_)=>ReadForeground();
        deviceHealth=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};deviceHealth.Tick+=async(_,_)=>await RefreshDeviceHealthAsync();
        learningTimeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(12)};learningTimeout.Tick+=(_,_)=>{learningTimeout.Stop();if(keys is not null)keys.Learning=false;model.SetHardwareStatus("No interceptable brightness key detected. Ctrl+Alt+Up/Down remains available.");};
    }
    public async Task StartAsync()
    {
        wallpaper=new(model);
        volumeReady=model.AudioAvailable;brightnessReady=model.Brightness.HasValue;
        try{outputDevices=AudioDeviceService.Watch(()=>rebindAudio=true);}catch{model.Log.Record("AUDIO_DEVICE_WATCH_UNAVAILABLE");}
        try
        {
            keys=new HardwareKeys{CanHandle=action=>island.Machine.Target!=IslandState.Hidden&&(action is HardwareAction.BrightnessDown or HardwareAction.BrightnessUp?brightnessReady:volumeReady),ActionRequested=action=>actions.Writer.TryWrite(action)};
            keys.KeyLearned+=key=>dispatcher.BeginInvoke(()=>Learned(key));
            model.SetHardwareStatus("Volume keys use Aster. Brightness: Ctrl+Alt+Up/Down, or teach your hardware keys.");
        }
        catch{model.SetHardwareStatus("Keyboard interception unavailable; Windows keeps handling your keys.");model.Log.Record("MEDIA_HOOK_UNAVAILABLE");}
        model.AppearanceChanged+=ApplyPreferences;model.LearnBrightnessRequested+=Learn;
        ApplyPreferences();foreground.Start();deviceHealth.Start();_ =ProcessActionsAsync();await RefreshDeviceHealthAsync();
    }
    private async Task RefreshDeviceHealthAsync()
    {
        if(disposed||healthBusy)return;healthBusy=true;
        try
        {
            AudioReading? currentAudio=null;int? currentBrightness=null;
            await Task.Run(()=>{try{currentAudio=AudioService.Read();}catch{}try{currentBrightness=BrightnessService.Read();}catch{}});
            if(disposed)return;
            volumeReady=currentAudio is not null;if(!volumeReady)rebindAudio=true;brightnessReady=currentBrightness.HasValue;
            if(currentAudio is not null)model.ReceiveAudio(currentAudio);
            if(currentBrightness is { } level)model.ReceiveBrightness(level);
            if(volumeReady&&(rebindAudio||audio is null))
            {
                rebindAudio=false;audio?.Dispose();audio=null;
                try{audio=AudioService.Watch(value=>dispatcher.BeginInvoke(()=>{if(!disposed){volumeReady=true;model.ReceiveAudio(value);}}));}catch{rebindAudio=true;}
            }
            if(brightness is null&&brightnessReady)
            {
                IDisposable? subscription=null;
                try{subscription=await Task.Run(()=>new BrightnessWatcher(value=>dispatcher.BeginInvoke(()=>{if(!disposed){brightnessReady=true;model.ReceiveBrightness(value);}})));}
                catch{/* Polling remains active while the Windows event provider recovers. */}
                if(disposed)subscription?.Dispose();else brightness=subscription;
            }
        }
        finally{healthBusy=false;}
    }
    public void RecoverDeviceIntegrations(){if(disposed)return;volumeReady=brightnessReady=false;rebindAudio=true;brightness?.Dispose();brightness=null;_ =RefreshDeviceHealthAsync();}
    public void ApplyPreferences()
    {
        var p=model.Preferences;
        if(keys is not null){keys.VolumeEnabled=p.ReplaceVolumeKeys&&p.VolumeHudEnabled;keys.BrightnessEnabled=p.ReplaceBrightnessKeys&&p.BrightnessHudEnabled;keys.DimKey=p.BrightnessDownKey;keys.BrightenKey=p.BrightnessUpKey;}
        if(p.TaskbarGlass){taskbar.Apply(p.TaskbarTintOpacity);model.SetTaskbarStatus(taskbar.Status,taskbar.CanTint);}
        else{taskbar.Restore();model.SetTaskbarStatus(taskbar.Status);}
        if(!p.AutoStowMaximized)island.Machine.SetStowed(false);
        ReadForeground();
    }
    private void ReadForeground()
    {
        if(disposed)return;
        var hwnd=new WindowInteropHelper(island).Handle;var f=ForegroundService.Read(hwnd);
        // Activating the island for a peek must not erase the previous foreground-app stow policy.
        if(!f.OwnWindow||!f.AnyApplicationVisible)island.Machine.SetStowed(launchReveal.AllowStow(ForegroundPolicy.ShouldStow(model.Preferences.AutoStowMaximized,f.OwnWindow,f.ShellWindow,f.Maximized,f.Fullscreen,f.AnyApplicationVisible)));
        if(++taskbarTicks%15==0&&model.Preferences.TaskbarGlass){taskbar.Apply(model.Preferences.TaskbarTintOpacity);model.SetTaskbarStatus(taskbar.Status,taskbar.CanTint);}
    }
    private void Learn(bool up)
    {
        if(keys is null){model.SetHardwareStatus("The keyboard hook is unavailable.");return;}
        learningUp=up;keys.Learning=true;learningTimeout.Stop();learningTimeout.Start();model.SetHardwareStatus(up?"Press your brightness-up key now. Learning expires in 12 seconds.":"Press your brightness-down key now. Learning expires in 12 seconds.");
    }
    private void Learned(int key)
    {
        learningTimeout.Stop();if(disposed)return;
        var p=model.Preferences;
        if((learningUp?p.BrightnessDownKey:p.BrightnessUpKey)==key){model.SetHardwareStatus("Use different keys for brightness up and down.");return;}
        model.UpdatePreferences(learningUp?p with{BrightnessUpKey=key,ReplaceBrightnessKeys=true}:p with{BrightnessDownKey=key,ReplaceBrightnessKeys=true});
        model.SetHardwareStatus("Brightness key learned. Only that function/media key will be intercepted.");
    }
    private async Task ProcessActionsAsync()
    {
        try
        {
            await foreach(var action in actions.Reader.ReadAllAsync(stop.Token).ConfigureAwait(false))
            {
                try
                {
                    if(action is HardwareAction.BrightnessDown or HardwareAction.BrightnessUp)
                    {
                        var current=BrightnessService.Read()??throw new NotSupportedException();
                        var value=Math.Clamp(current+(action==HardwareAction.BrightnessUp?5:-5),0,100);BrightnessService.Set(value);
                        await dispatcher.InvokeAsync(()=>{if(!disposed)model.ReceiveBrightness(value,true);});
                    }
                    else
                    {
                        var value=AudioService.Read();
                        if(action==HardwareAction.VolumeMute)AudioService.ToggleMute();else AudioService.SetVolume(value.Volume+(action==HardwareAction.VolumeUp?.02f:-.02f));
                        var updated=AudioService.Read();await dispatcher.InvokeAsync(()=>{if(!disposed)model.ReceiveAudio(updated,true);});
                    }
                }
                catch
                {
                    if(action is HardwareAction.BrightnessDown or HardwareAction.BrightnessUp)brightnessReady=false;else volumeReady=false;
                    await dispatcher.InvokeAsync(()=>{if(!disposed)model.SetHardwareStatus("Device control failed. Those keys now fall back to Windows.");});
                }
            }
        }
        catch(OperationCanceledException){}
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;foreground.Stop();deviceHealth.Stop();outputDevices?.Dispose();learningTimeout.Stop();model.AppearanceChanged-=ApplyPreferences;model.LearnBrightnessRequested-=Learn;
        wallpaper?.Dispose();keys?.Dispose();audio?.Dispose();brightness?.Dispose();actions.Writer.TryComplete();stop.Cancel();taskbar.Dispose();stop.Dispose();
    }
}

