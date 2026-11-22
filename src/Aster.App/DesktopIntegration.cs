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
    private readonly DispatcherTimer foreground,learningTimeout;
    private SpotifyWallpaperController? wallpaper;
    private HardwareKeys? keys;private IDisposable? audio,brightness;
    private bool learningUp;private volatile bool disposed,volumeReady,brightnessReady;private int taskbarTicks;
    public int HandledKeyCount=>keys?.HandledCount??0;
    public DesktopIntegration(AppModel model,OverlayWindow island)
    {
        this.model=model;this.island=island;dispatcher=Dispatcher.CurrentDispatcher;
        foreground=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};foreground.Tick+=(_,_)=>ReadForeground();
        learningTimeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(12)};learningTimeout.Tick+=(_,_)=>{learningTimeout.Stop();if(keys is not null)keys.Learning=false;model.SetHardwareStatus("No interceptable brightness key detected. Ctrl+Alt+Up/Down remains available.");};
    }
    public async Task StartAsync()
    {
        wallpaper=new(model);
        volumeReady=model.AudioAvailable;brightnessReady=model.Brightness.HasValue;
        try{audio=AudioService.Watch(reading=>dispatcher.BeginInvoke(()=>{if(!disposed)model.ReceiveAudio(reading);}));}catch{model.Log.Record("AUDIO_WATCH_UNAVAILABLE");}
        try{brightness=await Task.Run(()=>new BrightnessWatcher(value=>dispatcher.BeginInvoke(()=>{if(!disposed)model.ReceiveBrightness(value);})));}catch{model.Log.Record("BRIGHTNESS_WATCH_UNAVAILABLE");}
        try
        {
            keys=new HardwareKeys{CanHandle=action=>island.Machine.Target!=IslandState.Hidden&&(action is HardwareAction.BrightnessDown or HardwareAction.BrightnessUp?brightnessReady:volumeReady),ActionRequested=action=>actions.Writer.TryWrite(action)};
            keys.KeyLearned+=key=>dispatcher.BeginInvoke(()=>Learned(key));
            model.SetHardwareStatus("Volume keys use Aster. Brightness: Ctrl+Alt+Up/Down, or teach your hardware keys.");
        }
        catch{model.SetHardwareStatus("Keyboard interception unavailable; Windows keeps handling your keys.");model.Log.Record("MEDIA_HOOK_UNAVAILABLE");}
        model.AppearanceChanged+=ApplyPreferences;model.LearnBrightnessRequested+=Learn;
        ApplyPreferences();foreground.Start();_ =ProcessActionsAsync();
    }
    public void ApplyPreferences()
    {
        var p=model.Preferences;
        if(keys is not null){keys.VolumeEnabled=p.ReplaceVolumeKeys;keys.BrightnessEnabled=p.ReplaceBrightnessKeys;keys.DimKey=p.BrightnessDownKey;keys.BrightenKey=p.BrightnessUpKey;}
        if(p.TaskbarGlass&&!p.CustomDock){taskbar.Apply(p.TaskbarTintOpacity);model.SetTaskbarStatus(taskbar.Status,taskbar.CanTint);}
        else{taskbar.Restore();model.SetTaskbarStatus(taskbar.Status);}
        ReadForeground();
    }
    private void ReadForeground()
    {
        if(disposed)return;
        var hwnd=new WindowInteropHelper(island).Handle;var f=ForegroundService.Read(hwnd);
        // Activating the island for a peek must not erase the previous foreground-app stow policy.
        if(!f.OwnWindow||!f.AnyApplicationVisible)island.Machine.SetStowed(launchReveal.AllowStow(ForegroundPolicy.ShouldStow(model.Preferences.AutoStowMaximized,f.OwnWindow,f.ShellWindow,f.Maximized,f.Fullscreen,f.AnyApplicationVisible)));
        if(++taskbarTicks%15==0&&model.Preferences.TaskbarGlass&&!model.Preferences.CustomDock){taskbar.Apply(model.Preferences.TaskbarTintOpacity);model.SetTaskbarStatus(taskbar.Status,taskbar.CanTint);}
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
                        await dispatcher.InvokeAsync(()=>{if(!disposed)model.ReceiveBrightness(value);});
                    }
                    else
                    {
                        var value=AudioService.Read();
                        if(action==HardwareAction.VolumeMute)AudioService.ToggleMute();else AudioService.SetVolume(value.Volume+(action==HardwareAction.VolumeUp?.02f:-.02f));
                        var updated=AudioService.Read();await dispatcher.InvokeAsync(()=>{if(!disposed)model.ReceiveAudio(updated);});
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
        if(disposed)return;disposed=true;foreground.Stop();learningTimeout.Stop();model.AppearanceChanged-=ApplyPreferences;model.LearnBrightnessRequested-=Learn;
        wallpaper?.Dispose();keys?.Dispose();audio?.Dispose();brightness?.Dispose();actions.Writer.TryComplete();stop.Cancel();taskbar.Dispose();stop.Dispose();
    }
}

