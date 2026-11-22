using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Aster.Infrastructure;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace Aster.App;
public partial class App : Application
{
    private AppModel? model;private MainWindow? center;private OverlayWindow? overlay;
    private Forms.NotifyIcon? tray;private HwndSource? source;private Mutex? mutex;
    private IntPtr handle;private bool listening,hotkey,ownsMutex,quitting;
    private DesktopIntegration? desktop;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length==2&&e.Args[0]=="--wallpaper-test")
        {try{await WallpaperProbe.RunAsync(e.Args[1]);Shutdown(0);}catch(Exception error){await File.WriteAllTextAsync(e.Args[1]+".failure",error.ToString());Shutdown(1);}return;}
        if(e.Args.Length==2&&e.Args[0]=="--platform-probe")
        {try{await PlatformProbe.RunAsync(e.Args[1]);Shutdown(0);}catch{Shutdown(1);}return;}
        if(e.Args.Length==2&&e.Args[0]=="--taskbar-probe")
        {try{await TaskbarProbe.RunAsync(e.Args[1]);Shutdown(0);}catch{Shutdown(1);}return;}
        if(e.Args.Length==1&&e.Args[0]=="--foreground-helper")
        {var helper=new Window{Title="Aster maximized-window test",ShowInTaskbar=true,Background=Brushes.Black};helper.Loaded+=(_,_)=>{helper.WindowState=WindowState.Maximized;helper.Activate();};helper.Show();helper.Activate();await System.Threading.Tasks.Task.Delay(2200);helper.Close();Shutdown();return;}
        if(e.Args.Length==2&&e.Args[0]=="--audio-probe")
        {try{var endpoints=await System.Threading.Tasks.Task.Run(Aster.Platform.Windows.AudioDeviceService.Read);using var watch=Aster.Platform.Windows.AudioDeviceService.Watch(()=>{});var volume=Aster.Platform.Windows.AudioService.Read();using var volumeWatch=Aster.Platform.Windows.AudioService.Watch(_=>{});await File.WriteAllTextAsync(e.Args[1],System.Text.Json.JsonSerializer.Serialize(new{nativeEnumeration=true,nativeSubscription=true,volumeServiceAfterDeviceDiscovery=true,volumeWatchAfterDeviceDiscovery=true,endpoints=endpoints.Count,headphoneEndpoints=endpoints.Count(x=>x.Headphone),activeOutputs=endpoints.Count(x=>x.Active)}));Shutdown(0);}catch(Exception error){await File.WriteAllTextAsync(e.Args[1]+".failure",error.ToString());Shutdown(1);}return;}
        var startupTest=e.Args.Length==2&&(e.Args[0]=="--startup-test"||e.Args[0]=="--shelf-test"||e.Args[0]=="--notification-test"||e.Args[0]=="--notification-live-test"||e.Args[0]=="--quality-test"||e.Args[0]=="--night-test");
        var visual=e.Args.Length==2&&e.Args[0]=="--visual-test";
        var deviceTest=e.Args.Length==2&&e.Args[0]=="--device-test";
        var uiTest=Array.IndexOf(e.Args,"--ui-test")>=0;
        var testProfile=startupTest||visual||uiTest||deviceTest||Array.IndexOf(e.Args,"--test-profile")>=0;
        mutex=new Mutex(true,testProfile?"Local\\AsterDesktop.Test."+Guid.NewGuid():"Local\\AsterDesktop.Application",out ownsMutex);
        if(!ownsMutex){MessageBox.Show("Aster is already running. Open the island from its tray icon or press Ctrl+Alt+Space.","Aster");Shutdown();return;}
        var root=testProfile?Path.Combine(Path.GetTempPath(),"AsterDesktop-Tests",Guid.NewGuid().ToString("N")):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AsterDesktop");
        if(startupTest)new LocalStore(root).Save("preferences.json",new Aster.Core.Preferences{ShowOverlay=false,TaskbarGlass=false});
        model=new(new LocalStore(root));
        model.EnsureIslandShown();
        DispatcherUnhandledException+=(_,args)=>{model.Error("UI_ERROR","An operation failed. Restart Aster if the problem persists.");args.Handled=true;};
        model.RequestQuit+=Quit;model.AppearanceChanged+=ApplyAppearance;model.ClipboardPermissionChanged+=ListenClipboard;
        overlay=new(model,OpenSettings){IsVisualTesting=visual||startupTest,IsHitTestVisible=!(visual||startupTest)};MainWindow=overlay;
        // Test-only taskbar exposure lets the desktop automation helper enumerate a tool overlay.
        if(uiTest)overlay.ShowInTaskbar=true;
        overlay.SourceInitialized+=(_,_)=>
        {
            handle=new WindowInteropHelper(overlay).Handle;source=HwndSource.FromHwnd(handle);source?.AddHook(WindowMessage);
            if(!visual){hotkey=RegisterHotKey(handle,1,0x0001|0x0002|0x4000,0x20);if(!hotkey)model.Error("HOTKEY_UNAVAILABLE","Ctrl+Alt+Space is in use. Open Aster from the tray.");}
        };
        ApplyAppearance();
        tray=new Forms.NotifyIcon{Text="Aster · Your island",Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??System.Drawing.SystemIcons.Application,Visible=!visual};
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("Open island",null,(_,_)=>Dispatcher.Invoke(OpenIsland));
        var nightItem=new Forms.ToolStripMenuItem("Night mode"){Checked=model.Preferences.NightMode};nightItem.Click+=(_,_)=>Dispatcher.Invoke(()=>{model.ToggleNightCommand.Execute(null);nightItem.Checked=model.Preferences.NightMode;});menu.Items.Add(nightItem);menu.Opening+=(_,_)=>nightItem.Checked=model.Preferences.NightMode;
        menu.Items.Add("Settings & local tools",null,(_,_)=>Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Hide island",null,(_,_)=>Dispatcher.Invoke(()=>model.UpdatePreferences(model.Preferences with{ShowOverlay=false})));
        menu.Items.Add("Quit Aster",null,(_,_)=>Dispatcher.Invoke(Quit));
        tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(OpenIsland);
        SystemEvents.DisplaySettingsChanged+=DisplayChanged;SystemEvents.SessionSwitch+=SessionChanged;SystemEvents.PowerModeChanged+=PowerChanged;
        overlay.Show();overlay.Place(model.Preferences);overlay.SetVisible(model.Preferences.ShowOverlay);
        if(Array.IndexOf(e.Args,"--connect-notifications")>=0){overlay.Machine.Expand(Aster.Core.IslandPage.Notifications);overlay.Activate();model.ConnectNotificationsCommand.Execute(null);}
        if(startupTest)
        {
            try{if(e.Args[0]=="--night-test")await LiveQualityProbe.NightAsync(overlay,model,e.Args[1]);else if(e.Args[0]=="--quality-test")await LiveQualityProbe.RunAsync(overlay,model,e.Args[1]);else if(e.Args[0]=="--notification-live-test")await NotificationLiveProbe.RunAsync(overlay,model,e.Args[1]);else if(e.Args[0]=="--notification-test")await NotificationProbe.RunAsync(overlay,model,e.Args[1]);else if(e.Args[0]=="--shelf-test")await ShelfProbe.RunAsync(overlay,model,e.Args[1]);else await StartupProbe.RunAsync(overlay,model,OpenIsland,e.Args[1]);Quit();}
            catch(Exception error){Directory.CreateDirectory(e.Args[1]);await File.WriteAllTextAsync(Path.Combine(e.Args[1],"failure.txt"),error.ToString());Quit();Environment.ExitCode=1;}
            return;
        }
        if(visual)
        {
            try{await IslandVisualTests.RunAsync(overlay,model,e.Args[1]);Quit();}
            catch(Exception error){await File.WriteAllTextAsync(Path.Combine(e.Args[1],"failure.txt"),error.ToString());Quit();Environment.ExitCode=1;}
        }
        else
        {
            desktop=new(model,overlay);var deviceStartup=desktop.StartAsync();await model.StartAsync();await deviceStartup;
            if(deviceTest){try{await DeviceIntegrationProbe.RunAsync(model,overlay,desktop,e.Args[1]);}catch(Exception error){await File.WriteAllTextAsync(e.Args[1]+".failure",error.ToString());}finally{Quit();}}
        }
    }
    private void OpenIsland()
    {
        if(model is null||overlay is null||quitting)return;
        model.EnsureIslandShown();overlay.OpenIsland();
    }
    private void OpenSettings()
    {
        if(model is null||quitting)return;
        if(center is null){center=new(model){Title="Aster · Settings & local tools"};center.Opacity=model.Preferences.Opacity;}
        center.Show();if(center.WindowState==WindowState.Minimized)center.WindowState=WindowState.Normal;center.Activate();
    }
    private void PowerChanged(object sender,PowerModeChangedEventArgs e){if(e.Mode==PowerModes.Resume)Dispatcher.BeginInvoke(()=>desktop?.RecoverDeviceIntegrations());}
    private void SessionChanged(object sender,SessionSwitchEventArgs e)=>Dispatcher.BeginInvoke(()=>{if(e.Reason==SessionSwitchReason.SessionLock){model?.LockNotifications(true);overlay?.ClearNotificationPrivacy();overlay?.Machine.SetHidden(true);}else if(e.Reason==SessionSwitchReason.SessionUnlock){model?.LockNotifications(false);if(model?.Preferences.ShowOverlay==true)overlay?.SetVisible(true);}});
    private void DisplayChanged(object? sender,EventArgs args)=>Dispatcher.BeginInvoke(()=>{if(model is not null&&!quitting)overlay?.Place(model.Preferences);});
    private void ApplyAppearance()
    {
        if(model is null)return;var p=model.Preferences;var light=p.Theme=="Light";
        Brush BrushFor(string value){var c=(Color)ColorConverter.ConvertFromString(value);byte g=(byte)Math.Round(.2126*c.R+.7152*c.G+.0722*c.B);return new SolidColorBrush(Color.FromArgb(c.A,g,g,g));}
        Resources["BackgroundBrush"]=BrushFor(p.BackgroundColor.Length>0?p.BackgroundColor:light?"#F3F6FA":"#10141D");
        Resources["CardBrush"]=BrushFor(p.CardColor.Length>0?p.CardColor:light?"#FFFFFF":"#1A2130");
        Resources["TextBrush"]=BrushFor(p.TextColor.Length>0?p.TextColor:light?"#182439":"#EFF4FA");
        Resources["MutedBrush"]=BrushFor(light?"#50627A":"#A7B4C8");Resources["BorderBrush"]=BrushFor(light?"#C5D1E0":"#344052");
        Resources["AccentBrush"]=BrushFor(p.Accent);Resources["AppFont"]=new FontFamily(p.Font);Resources["TextSize"]=14d*p.TextScale;Resources["CardRadius"]=new CornerRadius(p.Radius);
        if(center is not null)center.Opacity=p.Opacity;
        if(overlay is not null){overlay.Place(p);overlay.SetVisible(p.ShowOverlay);}
    }
    private void ListenClipboard(bool enabled)
    {
        if(handle==IntPtr.Zero)return;
        if(enabled&&!listening){listening=AddClipboardFormatListener(handle);if(!listening){model!.ClipboardEnabled=false;model.Error("CLIPBOARD_LISTENER_FAILED","Clipboard history could not start.");}}
        else if(!enabled&&listening){RemoveClipboardFormatListener(handle);listening=false;}
    }
    private IntPtr WindowMessage(IntPtr hwnd,int message,IntPtr wp,IntPtr lp,ref bool handled)
    {
        if(message==0x0312&&wp.ToInt32()==1){OpenIsland();handled=true;}
        if(message==0x031D&&listening)model?.CaptureClipboard();return IntPtr.Zero;
    }
    private void Quit()
    {
        if(quitting)return;quitting=true;ListenClipboard(false);if(hotkey)UnregisterHotKey(handle,1);
        source?.RemoveHook(WindowMessage);SystemEvents.DisplaySettingsChanged-=DisplayChanged;SystemEvents.SessionSwitch-=SessionChanged;SystemEvents.PowerModeChanged-=PowerChanged;desktop?.Dispose();tray?.Dispose();tray=null;model?.Dispose();
        if(center is not null)center.Exiting=true;if(overlay is not null)overlay.Exiting=true;overlay?.Close();center?.Close();Shutdown();
    }
    protected override void OnExit(ExitEventArgs e){if(!quitting){desktop?.Dispose();tray?.Dispose();model?.Dispose();}if(ownsMutex)mutex?.ReleaseMutex();mutex?.Dispose();base.OnExit(e);}
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}

