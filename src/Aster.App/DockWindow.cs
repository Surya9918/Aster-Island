using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aster.Core;
using Aster.Platform.Windows;
namespace Aster.App;

internal sealed class DockWindow:Window,IDisposable
{
    private readonly AppModel model;private readonly Action settings;private readonly Action<IslandPage> openIsland;private readonly bool testing;
    private readonly Canvas stage=new(){Height=110};private readonly Border face=new(){Height=66,CornerRadius=new(25),BorderThickness=new(.8),BorderBrush=new SolidColorBrush(Color.FromArgb(48,255,255,255))};
    private readonly StackPanel content=new(){Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
    private readonly StackPanel apps=new(){Orientation=Orientation.Horizontal};private readonly ScrollViewer appScroll;
    private readonly Image cover=new(){Width=34,Height=34,Stretch=Stretch.UniformToFill};private readonly TextBlock play=new(){FontSize=18,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center};
    private readonly TextBlock time=new(){Foreground=Brushes.White,FontSize=14,FontWeight=FontWeights.SemiBold},date=new(){Foreground=Brushes.White,FontSize=11,Opacity=.85};
    private readonly DispatcherTimer update;private readonly DockAppBar appbar=new();private readonly Dictionary<Button,SpringValue> hovers=[];
    private readonly SpringValue appear=new(0),bodyWidth=new(580),lift=new(0);private readonly Dictionary<string,Button> appButtons=[];private readonly Dictionary<string,ImageSource?> icons=[];
    private string signature="";private bool refreshing,rendering,disposed,placing,leaseActive,fullscreen;private double desiredWidth=580,uiScale=1;private TimeSpan lastFrame;
    private DateTime revealUntil;private string? leaseFile;private NativeTaskbarState? original;private Process? guardian;
    public bool Exiting{get;set;}
    public bool IsTest=>testing;
    public Rect SurfaceBounds{get;private set;}
    public int ApplicationButtonCount=>appButtons.Count;
    public DockWindow(AppModel model,Action settings,Action<IslandPage> openIsland,bool testing=false)
    {
        this.model=model;this.settings=settings;this.openIsland=openIsland;this.testing=testing;
        Title="Aster · Dock";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=testing;ShowActivated=false;Height=180;Width=1200;
        Resources[typeof(Button)]=(Style)XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Foreground' Value='White'/><Setter Property='Background' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='Face' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' CornerRadius='12'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Face' Property='Background' Value='#16FFFFFF'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Face' Property='Background' Value='#26FFFFFF'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
        Resources[typeof(ContextMenu)]=(Style)XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContextMenu'><Setter Property='Background' Value='#10131A'/><Setter Property='Foreground' Value='White'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ContextMenu'><Border Background='{TemplateBinding Background}' BorderBrush='#30FFFFFF' BorderThickness='.8' CornerRadius='14' Padding='5'><ItemsPresenter/></Border></ControlTemplate></Setter.Value></Setter></Style>");
        Resources[typeof(MenuItem)]=(Style)XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='MenuItem'><Setter Property='Foreground' Value='White'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='MenuItem'><Border x:Name='Face' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' CornerRadius='9' Padding='12,8' Background='Transparent'><ContentPresenter ContentSource='Header'/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Face' Property='Background' Value='#20FFFFFF'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='Face' Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
        face.BorderBrush=new SolidColorBrush(Color.FromArgb(45,255,255,255));cover.Clip=new RectangleGeometry(new Rect(0,0,34,34),9,9);
        face.Background=new LinearGradientBrush(new GradientStopCollection{new(Color.FromArgb(252,17,19,24),0),new(Color.FromArgb(250,5,7,10),1)},new Point(0,0),new Point(0,1));
        face.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=20,ShadowDepth=5,Opacity=.35,Color=Colors.Black};
        face.Child=content;face.Padding=new Thickness(10,5,10,5);stage.Children.Add(face);Content=stage;
        var start=new System.Windows.Shapes.Path{Data=Geometry.Parse("M0,0 H10 V10 H0 Z M14,0 H24 V10 H14 Z M0,14 H10 V24 H0 Z M14,14 H24 V24 H14 Z"),Fill=Brushes.White,Width=25,Height=25,Stretch=Stretch.Uniform};
        content.Children.Add(MakeButton(start,"Open Windows Start",()=>{RevealNative();NativeTaskbar.OpenStart();},46));
        appScroll=new ScrollViewer{Content=apps,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalAlignment=VerticalAlignment.Center};
        appScroll.PreviewMouseWheel+=(_,e)=>{appScroll.ScrollToHorizontalOffset(appScroll.HorizontalOffset-e.Delta/3d);e.Handled=true;};content.Children.Add(appScroll);AddSeparator();
        var art=new Border{Width=36,Height=36,CornerRadius=new(10),ClipToBounds=true,Background=new SolidColorBrush(Color.FromRgb(32,35,43))};
        var artGrid=new Grid();artGrid.Children.Add(new IslandIcon{Kind="music",Width=22,Height=22,Stroke=Brushes.White});artGrid.Children.Add(cover);art.Child=artGrid;
        content.Children.Add(MakeButton(art,"Open current media",()=>openIsland(IslandPage.Media),44));
        var transport=MakeButton(play,"Play or pause",()=>model.MediaCommand.Execute("toggle"),30);transport.SetBinding(IsEnabledProperty,new System.Windows.Data.Binding(nameof(AppModel.MediaAvailable)){Source=model});content.Children.Add(transport);AddSeparator();
        content.Children.Add(MakeButton(new IslandIcon{Kind="back",Width=16,Height=20},"Show Windows taskbar for 12 seconds",RevealNative,30));
        content.Children.Add(MakeButton(new IslandIcon{Kind="wifi",Width=23,Height=23},"Network settings",()=>NativeTaskbar.Launch("ms-settings:network-status"),38));
        content.Children.Add(MakeButton(new IslandIcon{Kind="volume",Width=23,Height=23},"Sound and brightness",()=>openIsland(IslandPage.Controls),38));
        var clock=new StackPanel{Margin=new(8,0,8,0),VerticalAlignment=VerticalAlignment.Center};clock.Children.Add(time);clock.Children.Add(date);content.Children.Add(MakeButton(clock,"Calendar",ShowCalendar,104));
        content.Children.Add(MakeButton(new IslandIcon{Kind="bell",Width=23,Height=25},"Open Windows notifications",()=>{RevealNative();NativeTaskbar.OpenNotifications();},40));
        var menu=new ContextMenu{Style=(Style)Resources[typeof(ContextMenu)],ItemContainerStyle=(Style)Resources[typeof(MenuItem)]};void Item(string text,Action action){var m=new MenuItem{Header=text};m.Click+=(_,_)=>action();menu.Items.Add(m);}
        Item("Aster settings",settings);Item("Show Windows taskbar",RevealNative);Item("Restore Windows taskbar / disable dock",()=>model.UpdatePreferences(model.Preferences with{CustomDock=false}));
        face.ContextMenu=menu;
        update=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};update.Tick+=(_,_)=>Tick();
        model.PropertyChanged+=ModelChanged;SourceInitialized+=(_,_)=>{var hwnd=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(hwnd)?.AddHook(Messages);if(!ShowInTaskbar){var style=GetWindowLongPtr(hwnd,-20).ToInt64();SetWindowLongPtr(hwnd,-20,(IntPtr)(style|0x08000080));}};
        Loaded+=(_,_)=>{try{Place();appear.Target=1;Render();UpdateMedia();RefreshApps();if(!testing)TakeTaskbar();update.Start();}catch{ReleaseTaskbar();model.Error("DOCK_UNAVAILABLE","Dock could not start; the Windows taskbar was restored.");model.UpdatePreferences(model.Preferences with{CustomDock=false});}};
        Closing+=(_,e)=>{if(!Exiting){e.Cancel=true;model.UpdatePreferences(model.Preferences with{CustomDock=false});}};Closed+=(_,_)=>Dispose();
    }
    private Button MakeButton(object child,string name,Action action,double width=44)
    {
        var button=new Button{Content=child,Width=width,Height=48,Margin=new(1,0,1,0),Focusable=true,RenderTransformOrigin=new(.5,.6),RenderTransform=new ScaleTransform(1,1)};
        AutomationProperties.SetName(button,name);button.ToolTip=new ToolTip{Content=name,Background=new SolidColorBrush(Color.FromRgb(19,22,29)),Foreground=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(58,64,76)),Padding=new(12,7,12,7)};
        ToolTipService.SetInitialShowDelay(button,240);hovers[button]=new(1);button.MouseEnter+=(_,_)=>{hovers[button].Target=1.085;Render();};button.MouseLeave+=(_,_)=>{hovers[button].Target=1;Render();};
        button.Click+=(_,_)=>{try{action();}catch{model.Error("DOCK_ACTION_FAILED","Windows could not complete that dock action.");RevealNative();}};return button;
    }
    private void AddSeparator()=>content.Children.Add(new Border{Width=1,Height=26,Margin=new(8,0,8,0),Background=new SolidColorBrush(Color.FromArgb(38,255,255,255))});
    private void ModelChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName is nameof(AppModel.MediaArtwork) or nameof(AppModel.MediaPlaying) or nameof(AppModel.MediaTitle))UpdateMedia();if(e.PropertyName==nameof(AppModel.Preferences))Place();}
    private void UpdateMedia(){cover.Source=model.MediaArtwork;play.Text=model.MediaPlaying?"Ⅱ":"▶";time.Text=DateTime.Now.ToString("h:mm tt");date.Text=DateTime.Now.ToString("M'/'d'/'yyyy");}
    private async void RefreshApps()
    {
        if(refreshing||disposed)return;refreshing=true;
        try
        {
            var items=await System.Threading.Tasks.Task.Run(DockApplications.Read);if(disposed)return;
            var key=string.Join("|",items.Select(i=>$"{i.Window}:{i.Active}"))+string.Join("|",model.Preferences.DockPinnedPaths)+model.Preferences.DockBrowserShortcut;if(key==signature)return;signature=key;
            var rows=items.GroupBy(i=>string.IsNullOrEmpty(i.Path)?i.Window.ToString():i.Path+":"+i.Icon,StringComparer.OrdinalIgnoreCase).Select(g=>g.FirstOrDefault(i=>i.Active)??g.First()).ToList();
            var wanted=new List<(string Key,string Name,string Path,IntPtr Window,IntPtr Icon,bool Active)>();
            var explorer=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");var browsers=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Google","Chrome","Application","chrome.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Google","Chrome","Application","chrome.exe")};var browser=browsers.FirstOrDefault(File.Exists);
            var pins=new[]{explorer}.Concat(browser is null||!model.Preferences.DockBrowserShortcut?Array.Empty<string>():new[]{browser}).Concat(model.Preferences.DockPinnedPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach(var path in pins){if(!File.Exists(path))continue;var row=rows.FirstOrDefault(i=>i.Path.Equals(path,StringComparison.OrdinalIgnoreCase));wanted.Add((path,path.Equals(explorer,StringComparison.OrdinalIgnoreCase)?"File Explorer":row?.Name??(Path.GetFileNameWithoutExtension(path).Equals("chrome",StringComparison.OrdinalIgnoreCase)?"Google Chrome":Path.GetFileNameWithoutExtension(path)),path,row?.Window??IntPtr.Zero,row?.Icon??IntPtr.Zero,row?.Active??false));if(row is not null)rows.Remove(row);}
            foreach(var row in rows)wanted.Add((row.Window.ToString(),row.Name,row.Path,row.Window,row.Icon,row.Active));
            var old=new HashSet<string>(appButtons.Keys);
            foreach(var row in wanted)
            {
                old.Remove(row.Key);
                if(!appButtons.TryGetValue(row.Key,out var button))
                {
                    var captured=row;var image=LoadAppIcon(row.Path,row.Icon);var grid=new Grid{Width=42,Height=46};
                    if(image is not null)grid.Children.Add(new Image{Source=image,Width=30,Height=30,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center});else grid.Children.Add(new IslandIcon{Kind="today",Width=25,Height=25});
                    grid.Children.Add(new Border{Tag="indicator",Width=5,Height=3,CornerRadius=new(1.5),Background=Brushes.White,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,0,1)});
                    button=MakeButton(grid,row.Name,()=>{var current=buttonWindow.GetValueOrDefault(captured.Key);if(current!=IntPtr.Zero&&DockApplications.Exists(current))DockApplications.Activate(current);else if(DockPolicy.IsLocalLaunchPath(captured.Path))NativeTaskbar.Launch(captured.Path);});
                    var context=new ContextMenu{Style=(Style)Resources[typeof(ContextMenu)],ItemContainerStyle=(Style)Resources[typeof(MenuItem)]};if(browser is not null&&row.Path.Equals(browser,StringComparison.OrdinalIgnoreCase)&&model.Preferences.DockBrowserShortcut){var hide=new MenuItem{Header="Hide browser shortcut"};hide.Click+=(_,_)=>model.UpdatePreferences(model.Preferences with{DockBrowserShortcut=false});context.Items.Add(hide);}
                    else if(DockPolicy.IsLocalLaunchPath(row.Path)&&!row.Path.Equals(explorer,StringComparison.OrdinalIgnoreCase))
                    {var pin=new MenuItem{Header=model.Preferences.DockPinnedPaths.Contains(row.Path,StringComparer.OrdinalIgnoreCase)?"Unpin from Aster dock":"Pin to Aster dock"};pin.Click+=(_,_)=>{var p=model.Preferences.DockPinnedPaths.ToList();if(p.RemoveAll(x=>x.Equals(captured.Path,StringComparison.OrdinalIgnoreCase))==0)p.Add(captured.Path);model.UpdatePreferences(model.Preferences with{DockPinnedPaths=p});};context.Items.Add(pin);}
                    var close=new MenuItem{Header="Close window",IsEnabled=row.Window!=IntPtr.Zero};close.Click+=(_,_)=>{var h=buttonWindow.GetValueOrDefault(captured.Key);if(h!=IntPtr.Zero)PostMessage(h,0x10,IntPtr.Zero,IntPtr.Zero);};context.Items.Add(close);button.ContextMenu=context;
                    appButtons[row.Key]=button;apps.Children.Add(button);
                }
                buttonWindow[row.Key]=row.Window;foreach(var command in button.ContextMenu.Items.OfType<MenuItem>().Where(m=>m.Header as string=="Close window"))command.IsEnabled=row.Window!=IntPtr.Zero;var indicator=((Grid)button.Content).Children.OfType<Border>().First();indicator.Opacity=row.Window==IntPtr.Zero?0:row.Active?1:.35;indicator.Width=row.Active?13:5;
            }
            foreach(var keyOld in old){var b=appButtons[keyOld];apps.Children.Remove(b);hovers.Remove(b);appButtons.Remove(keyOld);buttonWindow.Remove(keyOld);}
            Reflow();
        }
        catch{model.Error("DOCK_APPS_UNAVAILABLE","Dock app list could not refresh. Windows taskbar remains available from the chevron.");}
        finally{refreshing=false;}
    }
    private readonly Dictionary<string,IntPtr> buttonWindow=[];
    private ImageSource? LoadAppIcon(string path,IntPtr handle)
    {
        var key=path+":"+handle;if(icons.TryGetValue(key,out var cached))return cached;ImageSource? result=null;
        try{if(handle!=IntPtr.Zero){var image=Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(48,48));image.Freeze();result=image;}else if(DockPolicy.IsLocalLaunchPath(path)){using var icon=System.Drawing.Icon.ExtractAssociatedIcon(path);if(icon is not null){var image=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(48,48));image.Freeze();result=image;}}}catch{}
        if(icons.Count>120)icons.Clear();icons[key]=result;return result;
    }
    private void Reflow(){appScroll.Width=Math.Min(Math.Max(46,apps.Children.Count*46),DockPolicy.AppsViewport(Width,uiScale));content.Width=double.NaN;content.Measure(new Size(double.PositiveInfinity,70));desiredWidth=Math.Min(Width/uiScale-20,content.DesiredSize.Width+22);content.Width=desiredWidth-22;content.HorizontalAlignment=HorizontalAlignment.Center;bodyWidth.Target=desiredWidth;Render();}
    private void Render(){if(rendering||disposed)return;rendering=true;lastFrame=TimeSpan.Zero;CompositionTarget.Rendering+=Frame;}
    private void Frame(object? sender,EventArgs e)
    {
        if(e is not RenderingEventArgs args||args.RenderingTime==lastFrame)return;var dt=lastFrame==TimeSpan.Zero?1d/120:Math.Min(.05,(args.RenderingTime-lastFrame).TotalSeconds);lastFrame=args.RenderingTime;
        if(model.Preferences.ReducedMotion){appear.Snap(appear.Target);bodyWidth.Snap(bodyWidth.Target);lift.Snap(lift.Target);}else{appear.Step(dt,new(420,38,1));bodyWidth.Step(dt,new(480,39,1));lift.Step(dt,new(420,38,1));}
        foreach(var p in hovers){if(model.Preferences.ReducedMotion)p.Value.Snap(p.Value.Target);else p.Value.Step(dt,new(540,38,1));((ScaleTransform)p.Key.RenderTransform).ScaleX=((ScaleTransform)p.Key.RenderTransform).ScaleY=p.Value.Position;}
        var w=Math.Clamp(bodyWidth.Position,300,Math.Max(300,Width/uiScale-12));face.Width=w;Canvas.SetLeft(face,(stage.Width-w*uiScale)/2);Canvas.SetTop(face,(96-lift.Position+16*(1-appear.Position))*uiScale);face.Opacity=Math.Clamp(appear.Position,0,1);face.Clip=new RectangleGeometry(new Rect(0,0,w,66),25,25);
        face.RenderTransform=new ScaleTransform(uiScale,uiScale);face.RenderTransformOrigin=new(0,0);SurfaceBounds=new((stage.Width-w*uiScale)/2,(96-lift.Position+16*(1-appear.Position))*uiScale,w*uiScale,66*uiScale);
        if(appear.Settled&&bodyWidth.Settled&&lift.Settled&&hovers.Values.All(v=>v.Settled)){CompositionTarget.Rendering-=Frame;rendering=false;}
    }
    public void Place()
    {
        if(placing||disposed)return;placing=true;try
        {
            var hwnd=new WindowInteropHelper(this).Handle;if(hwnd==IntPtr.Zero)return;var screen=System.Windows.Forms.Screen.PrimaryScreen!;var area=screen.Bounds;var dpi=GetDpiForWindow(hwnd)/96d;uiScale=model.Preferences.DockScale;
            Width=Math.Min(1200,area.Width/dpi-20);Height=180*uiScale;stage.Width=Width;stage.Height=Height;
            if(leaseActive)appbar.Reserve(area.Left,area.Top,area.Right,area.Bottom,(int)(84*uiScale*dpi));
            SetWindowPos(hwnd,new IntPtr(-1),area.Left+(area.Width-(int)(Width*dpi))/2,area.Bottom-(int)(Height*dpi),(int)(Width*dpi),(int)(Height*dpi),0x10);Reflow();
        }
        finally{placing=false;}
    }
    private void Tick()
    {
        UpdateMedia();RefreshApps();if(!leaseActive)return;
        var now=DateTime.UtcNow;if(now<revealUntil)NativeTaskbar.Show();else{NativeTaskbar.Hide();if(revealUntil!=DateTime.MinValue){revealUntil=DateTime.MinValue;lift.Target=0;Render();NativeTaskbar.SetState(original!.State|1);Place();}}
        var foreground=ForegroundService.Read(new WindowInteropHelper(this).Handle);if(!foreground.OwnWindow&&foreground.Fullscreen!=fullscreen){fullscreen=foreground.Fullscreen;Visibility=fullscreen?Visibility.Hidden:Visibility.Visible;}
    }
    public void RevealNative(){if(!leaseActive)return;revealUntil=DateTime.UtcNow.AddSeconds(12);lift.Target=54;Render();NativeTaskbar.SetState(original!.State);NativeTaskbar.Show();Place();}
    internal void HideNativeNow(){revealUntil=DateTime.MinValue;lift.Target=0;Render();if(original is not null)NativeTaskbar.SetState(original.State|1);NativeTaskbar.Hide();Place();}
    private void TakeTaskbar()
    {
        leaseFile=Path.Combine(model.Store.Root,"dock-restore.json");Directory.CreateDirectory(model.Store.Root);NativeTaskbar.Recover(leaseFile);original=NativeTaskbar.Capture();
        model.Store.Save("dock-restore.json",new DockLease(Guid.NewGuid(),Environment.ProcessId,original));
        guardian=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--dock-guardian",Environment.ProcessId.ToString(),leaseFile}});
        if(guardian is null||guardian.HasExited)throw new InvalidOperationException("Dock recovery helper unavailable.");
        NativeTaskbar.SetState(original.State|1);appbar.Register(new WindowInteropHelper(this).Handle);leaseActive=true;Place();NativeTaskbar.Hide();
    }
    private void ReleaseTaskbar()
    {
        appbar.Dispose();if(original is not null)NativeTaskbar.Restore(original);leaseActive=false;
        if(leaseFile is not null){try{File.Delete(leaseFile);File.Delete(leaseFile+".bak");}catch{}}
        if(guardian is not null){try{if(!guardian.HasExited)guardian.WaitForExit(1000);}catch{}guardian.Dispose();guardian=null;}
    }
    private void ShowCalendar()
    {
        var month=DateTime.Today;var panel=new StackPanel{Width=270,Margin=new(16)};var popup=new Popup{StaysOpen=false,AllowsTransparency=true,PlacementTarget=face,Placement=PlacementMode.Top,VerticalOffset=-8};
        void Draw(){panel.Children.Clear();var title=new TextBlock{Text=month.ToString("MMMM yyyy"),Foreground=Brushes.White,FontSize=18,Margin=new(4,0,4,12)};panel.Children.Add(title);var nav=new StackPanel{Orientation=Orientation.Horizontal};var prev=new Button{Content="‹",Width=40,Height=32,MinHeight=0,FontSize=22,Style=(Style)Resources[typeof(Button)]};var next=new Button{Content="›",Width=40,Height=32,MinHeight=0,FontSize=22,Style=(Style)Resources[typeof(Button)]};prev.Click+=(_,_)=>{month=month.AddMonths(-1);Draw();};next.Click+=(_,_)=>{month=month.AddMonths(1);Draw();};nav.Children.Add(prev);nav.Children.Add(next);panel.Children.Add(nav);var grid=new UniformGrid{Columns=7};foreach(var d in new[]{"M","T","W","T","F","S","S"})grid.Children.Add(new TextBlock{Text=d,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,Margin=new(0,8,0,8)});var first=new DateTime(month.Year,month.Month,1);for(int i=0;i<((int)first.DayOfWeek+6)%7;i++)grid.Children.Add(new TextBlock());for(int day=1;day<=DateTime.DaysInMonth(month.Year,month.Month);day++){var current=new DateTime(month.Year,month.Month,day);grid.Children.Add(new Border{CornerRadius=new(8),Background=current==DateTime.Today?new SolidColorBrush(Color.FromRgb(48,94,84)):Brushes.Transparent,Child=new TextBlock{Text=day.ToString(),Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,Margin=new(4,8,4,8)}});}panel.Children.Add(grid);}
        Draw();popup.Child=new Border{Child=panel,CornerRadius=new(20),Background=new SolidColorBrush(Color.FromRgb(12,15,21)),BorderBrush=new SolidColorBrush(Color.FromArgb(45,255,255,255)),BorderThickness=new(.8)};popup.IsOpen=true;
    }
    private IntPtr Messages(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled)
    {
        if(message==0x84){var x=(short)(l.ToInt64()&0xffff);var y=(short)((l.ToInt64()>>16)&0xffff);if(!SurfaceBounds.Contains(PointFromScreen(new Point(x,y)))){handled=true;return new IntPtr(-1);}}
        if(message==0x2E0||message==0x7E||message==DockAppBar.Notification&&w.ToInt32()==1)Dispatcher.BeginInvoke(Place);return IntPtr.Zero;
    }
    public void Dispose(){if(disposed)return;disposed=true;update.Stop();CompositionTarget.Rendering-=Frame;model.PropertyChanged-=ModelChanged;ReleaseTaskbar();}
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
    [DllImport("user32.dll")]private static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
    [DllImport("user32.dll")]private static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
}
