using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Aster.Core;
using Aster.Platform.Windows;
using ActivityKind=Aster.Core.ActivityKind;

namespace Aster.App;

public sealed record IslandFrame(double TimeMs,double IntervalMs,double UpdateMs,double Width,double Height,string Target);
public partial class OverlayWindow : Window
{
    private readonly AppModel model;
    private readonly Action settings;
    public IslandStateMachine Machine { get; }
    private readonly SpringValue width=new(224),height=new(40),radius=new(20),opacity=new(1),hover=new(0),progress=new(0),notice=new(0),top=new(8),hud=new(0),hudValue=new(0),content=new(0),nightBlend=new(0);
    private readonly SpringValue[] pages={new(0),new(0),new(0),new(0),new(0),new(1),new(0)};
    private readonly RectangleGeometry contentClip=new();
    private readonly SpringValue[] frameSprings;
    private readonly FrameworkElement[] pageViews;
    private readonly TranslateTransform[] pageOffsets;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly DispatcherTimer expiry,longPress,leave;
    private readonly List<IslandFrame> frames=[];
    private TimeSpan lastRender=TimeSpan.MinValue;
    private bool rendering,disposed,pressed,held,moved,touching,edgeReveal;
    private Point pressPoint,filePoint,light=new(.5,.1);
    private SpringConfiguration configuration=new();
    private IslandMotionProfile motion=IslandMotionProfile.Create(new(),true);
    private double scale=1;
    public bool IsVisualTesting {get;set;}
    public bool Exiting {get;set;}
    public IReadOnlyList<IslandFrame> FrameSamples=>frames;
    public void ClearFrameSamples()=>frames.Clear();
    public Rect SurfaceBounds=>Glass.Bounds;
    public int RenderingTier=>RenderCapability.Tier>>16;
    public OverlayWindow(AppModel model,Action settings)
    {
        this.model=model;this.settings=settings;Machine=new(new SystemClock());
        InitializeComponent();frameSprings=[width,height,radius,opacity,hover,progress,notice,top,hud,hudValue,content,nightBlend];pageViews=[MediaPage,FocusPage,ShelfPage,NotesPage,ControlsPage,TodayPage,NotificationsPage];pageOffsets=new TranslateTransform[pageViews.Length];for(int i=0;i<pageViews.Length;i++){pageOffsets[i]=new();pageViews[i].RenderTransform=pageOffsets[i];}DataContext=model;Glass.Bind(model,Machine);ExpandedContent.Clip=contentClip;
        expiry=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        expiry.Tick+=(_,_)=>Machine.Tick();
        longPress=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(480)};
        longPress.Tick+=(_,_)=>{longPress.Stop();if(!pressed||(!touching&&!Glass.Bounds.Contains(Mouse.GetPosition(Stage))))return;held=true;Machine.Expand(IslandPage.Controls);Activate();};
        leave=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(140)};
        leave.Tick+=(_,_)=>{leave.Stop();if(ContainsSurface(Mouse.GetPosition(Stage)))return;if(edgeReveal){edgeReveal=false;Machine.Collapse();}else Machine.Hover(false);};
        Machine.Changed+=Retarget;model.PropertyChanged+=ModelChanged;model.IslandNotice+=Notice;
        model.SystemLevelChanged+=SystemHud;
        Loaded+=(_,_)=>{Place(model.Preferences);Retarget();};
        SourceInitialized+=(_,_)=>{var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);source?.AddHook(NativeMessage);};
        Deactivated+=(_,_)=>{if(!IsVisualTesting&&Machine.Target==IslandState.Expanded)Machine.Collapse();};
        Closed+=(_,_)=>DisposeMotion();
        Closing+=(_,args)=>{if(!Exiting){args.Cancel=true;Machine.SetHidden(true);}};
        LostTouchCapture+=(_,_)=>{touching=pressed=false;longPress.Stop();};
        LostMouseCapture+=(_,_)=>{if(!touching){pressed=false;longPress.Stop();}};
    }
    private void ModelChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(AppModel.NotificationUnread)&&Machine.Target==IslandState.Expanded&&Machine.Page==IslandPage.Notifications)model.MarkNotificationsRead();
        if(e.PropertyName==nameof(AppModel.FocusRunning))Machine.SetActivity(model.FocusRunning?new(ActivityKind.Focus,20,"Focus"):null,ActivityKind.Focus);
        if(e.PropertyName==nameof(AppModel.MediaPlaying))Machine.SetActivity(model.MediaPlaying?new(ActivityKind.Media,10,"Music"):null,ActivityKind.Media);
        if(e.PropertyName is nameof(AppModel.FocusProgress)){progress.Target=model.FocusProgress;Wake();}
        if(e.PropertyName is nameof(AppModel.TimerText) or nameof(AppModel.ClockText) or nameof(AppModel.MediaTitle) or nameof(AppModel.MediaArtwork))Wake();
    }
    private void Notice(string title,string detail){if(Machine.Target==IslandState.Expanded)return;Machine.Notify(title,detail);}
    private void SystemHud(SystemHudKind kind,double value,bool muted)
    {
        edgeReveal=false;
        if(Machine.Target==IslandState.Expanded&&Machine.Page==IslandPage.Controls){Footer.Text=$"{kind} · {Math.Round(value):0}% · on-device control";return;}
        Machine.ShowSystemHud(kind,value,muted);
    }
    private void Retarget()
    {
        var state=Machine.Target;nightBlend.Target=model.Preferences.NightMode?1:0;
        if(state==IslandState.Expanded&&Machine.Page==IslandPage.Notifications)model.MarkNotificationsRead();
        ExpandedContent.Height=Machine.Page==IslandPage.Notifications?342:286;
        if(state is IslandState.Notification or IslandState.SystemHud)expiry.Start();else expiry.Stop();
        (width.Target,height.Target,radius.Target)=state switch
        {
            IslandState.Expanded=>(model.Preferences.OverlayWidth,Machine.Page==IslandPage.Notifications?390d:334d,36d),
            IslandState.Notification=>(430d,92d,30d),
            IslandState.PersistentActivity=>(300d,46d,23d),
            IslandState.Hover=>(Machine.ActivityCount>0?310d:240d,44d,22d),
            IslandState.Hidden=>(220d,42d,21d),
            IslandState.Stowed=>(96d,12d,6d),
            IslandState.SystemHud=>(350d,74d,30d),
            _=>(224d,40d,20d)
        };
        motion=IslandMotionProfile.Create(configuration,height.Target>height.Position+.5);
        content.Target=state==IslandState.Expanded?1:0;
        opacity.Target=state==IslandState.Hidden?0:1;hover.Target=state==IslandState.Hover?1:0;
        notice.Target=state==IslandState.Notification?1:0;
        top.Target=state==IslandState.Stowed?-10:Math.Clamp(model.Preferences.TopOffset,4,160);EdgeSensor.IsHitTestVisible=state!=IslandState.Hidden;EdgeSensor.Visibility=state==IslandState.Hidden?Visibility.Hidden:Visibility.Visible;hud.Target=state==IslandState.SystemHud?1:0;hudValue.Target=Machine.HudValue;
        for(var i=0;i<pages.Length;i++)pages[i].Target=(int)Machine.Page==i?1:0;
        var buttons=new[]{MediaTab,FocusTab,ShelfTab,NotesTab,ControlsTab,TodayTab,NotificationsTab};
        for(var i=0;i<buttons.Length;i++){buttons[i].Background=new SolidColorBrush(Color.FromArgb((byte)((int)Machine.Page==i?30:0),255,255,255));buttons[i].Foreground=new SolidColorBrush((int)Machine.Page==i?Color.FromRgb(219,219,219):Color.FromRgb(152,152,152));}
        Footer.Text=Machine.ActivityCount>1?"Focus + music · both activities are running.":Machine.Page==IslandPage.Notes?"Saved locally. No sync, no account.":"Local by default. Quiet by nature.";
        Wake();
    }
    private void Wake()
    {
        if(disposed||rendering)return;rendering=true;lastRender=TimeSpan.MinValue;CompositionTarget.Rendering+=RenderFrame;
    }
    private void RenderFrame(object? sender,EventArgs args)
    {
        if(args is not RenderingEventArgs timing||timing.RenderingTime==lastRender)return;
        var start=Stopwatch.GetTimestamp();
        var interval=lastRender==TimeSpan.MinValue?0:(timing.RenderingTime-lastRender).TotalSeconds;lastRender=timing.RenderingTime;
        var dt=interval<=0?1d/120:Math.Min(interval,.1);
        var springs=frameSprings;
        foreach(var spring in springs)
        {
            if(model.Preferences.ReducedMotion){spring.Snap(spring.Target);continue;}
            var response=ReferenceEquals(spring,width)?motion.Width:ReferenceEquals(spring,height)?motion.Height:
                ReferenceEquals(spring,radius)||ReferenceEquals(spring,top)?motion.Shape:
                ReferenceEquals(spring,content)?motion.Content:motion.Fade;
            spring.Step(dt,response);
        }
        foreach(var spring in pages){if(model.Preferences.ReducedMotion)spring.Snap(spring.Target);else spring.Step(dt,motion.Content);}
        var geometryReveal=Smooth((height.Position-95)/205);
        var reveal=Math.Clamp(content.Position,0,1)*geometryReveal;
        if(Machine.Target==IslandState.Notification)reveal*=Math.Clamp((height.Position-100)/120,0,1);
        var alpha=Math.Clamp(opacity.Position,0,1);
        var paintWidth=Math.Clamp(width.Position,80,720);var paintHeight=Math.Clamp(height.Position,4,390);var paintRadius=Math.Clamp(radius.Position,2,paintHeight/2);
        var noticeAlpha=Math.Clamp(notice.Position,0,1)*Smooth((150-height.Position)/45)*Smooth((height.Position-45)/30);
        Glass.Frame(paintWidth,paintHeight,paintRadius,alpha,reveal,clock.Elapsed.TotalSeconds,hover.Position,progress.Position,noticeAlpha,light,top.Position,hud.Position,hudValue.Position,Math.Clamp(nightBlend.Position,0,1));
        var compactGlyph=Smooth((105-paintHeight)/45)*Smooth((paintHeight-16)/22)*(1-noticeAlpha)*(1-Math.Clamp(hud.Position,0,1));
        CompactEye.Opacity=alpha*Math.Clamp(noticeAlpha+((model.MediaArtwork is not null&&model.MediaAvailable)?0:compactGlyph),0,1)*(1-.2*Math.Clamp(nightBlend.Position,0,1));
        Canvas.SetLeft(CompactEye,Glass.Bounds.X+11);Canvas.SetTop(CompactEye,Glass.Bounds.Y+(noticeAlpha>.5?19:paintHeight/2-11));
        HitPad.Width=paintWidth;HitPad.Height=Math.Max(14,paintHeight);HitPad.CornerRadius=new(paintRadius);Canvas.SetLeft(HitPad,(760-paintWidth)/2);Canvas.SetTop(HitPad,top.Position);
        HitPad.IsHitTestVisible=Machine.Target!=IslandState.Hidden;
        HitPad.Opacity=alpha;
        var contentInteractive=Machine.Target==IslandState.Expanded&&reveal>.8;
        ExpandedContent.Opacity=reveal*alpha*(1-.14*Math.Clamp(nightBlend.Position,0,1));ExpandedContent.IsHitTestVisible=contentInteractive;
        ExpandedContent.IsEnabled=contentInteractive;
        // Hidden only after opacity reaches zero: retain layout/views without invisible tab stops.
        ExpandedContent.Visibility=reveal*alpha>.0001?Visibility.Visible:Visibility.Hidden;
        ContentScale.ScaleX=ContentScale.ScaleY=.955+.045*Math.Clamp(content.Position,0,1);ContentOffset.Y=12*(1-Math.Clamp(content.Position,0,1));
        contentClip.Rect=new Rect((760-paintWidth)/2-130,top.Position-43,paintWidth,paintHeight);contentClip.RadiusX=contentClip.RadiusY=paintRadius;
        var views=pageViews;
        for(var i=0;i<views.Length;i++){var active=(int)Machine.Page==i&&pages[i].Position>.8;views[i].Opacity=Math.Clamp(pages[i].Position,0,1);views[i].IsHitTestVisible=active;views[i].IsEnabled=active;views[i].Visibility=views[i].Opacity>.0001?Visibility.Visible:Visibility.Hidden;pageOffsets[i].X=(1-pages[i].Position)*10;}
        var update=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;
        if(IsVisualTesting&&frames.Count<30000)frames.Add(new(clock.Elapsed.TotalMilliseconds,interval*1000,update,width.Position,height.Position,Machine.Target.ToString()));
        if(springs.All(s=>s.Settled)&&pages.All(s=>s.Settled))
        {
            Machine.MarkSettled();
            if(!model.MediaPlaying||Machine.Target is IslandState.Expanded or IslandState.Hidden)
            {CompositionTarget.Rendering-=RenderFrame;rendering=false;}
        }
    }
    private static double Smooth(double value){value=Math.Clamp(value,0,1);return value*value*(3-2*value);}
    public void Place(Preferences p)
    {
        configuration=NightMotion.Adapt(new(p.SpringStiffness,p.SpringDamping,p.SpringMass),p.NightMode);
        FontFamily=new FontFamily(p.Font);
        var hwnd=new WindowInteropHelper(this).Handle;if(hwnd==IntPtr.Zero)return;
        var screens=System.Windows.Forms.Screen.AllScreens;var screen=screens[Math.Min(p.Monitor,screens.Length-1)];var area=screen.WorkingArea;
        var monitor=MonitorFromPoint(new NativePoint{X=area.Left+area.Width/2,Y=area.Top+area.Height/2},2);
        var dpi=GetDpiForMonitor(monitor,0,out var dpiX,out _)==0?dpiX/96d:GetDpiForWindow(hwnd)/96d;
        scale=Math.Min(p.IslandScale*.9,Math.Min(area.Width/(760*dpi),area.Height/(460*dpi)));
        Stage.LayoutTransform=new ScaleTransform(scale,scale);Width=760*scale;Height=460*scale;
        SetWindowPos(hwnd,new IntPtr(-1),area.Left+(area.Width-(int)(Width*dpi))/2,screen.Bounds.Top,(int)(Width*dpi),(int)(Height*dpi),0x0010);
        Retarget();
    }
    public void SetVisible(bool visible){if(!IsVisible)Show();if(visible&&Machine.Target!=IslandState.Hidden)return;Machine.SetHidden(!visible);}
    public void OpenIsland(){SetVisible(true);Machine.Expand(Machine.Target==IslandState.Notification?IslandPage.Notifications:null);Activate();}
    public void ClearNotificationPrivacy(){Machine.ClearNotice();Glass.ClearPrivateText();}
    private async void DismissNotification(object sender,RoutedEventArgs e)
    {
        e.Handled=true;if(sender is not Button button||button.DataContext is not IslandNotification item)return;
        DependencyObject? node=button;while(node is not null&&node is not NotificationCard)node=VisualTreeHelper.GetParent(node);
        button.IsEnabled=false;if(node is NotificationCard card)await card.ExitAsync(model.Preferences.ReducedMotion);
        if(!disposed)model.HideNotificationCommand.Execute(item.Key);
    }
    private void SettingsClick(object sender,RoutedEventArgs e)=>settings();
    private void CollapseClick(object sender,RoutedEventArgs e)=>Machine.Collapse();
    private void SelectPage(object sender,RoutedEventArgs e){if(sender is Button button&&Enum.TryParse<IslandPage>(button.Tag as string,out var page))Machine.Select(page);}
    private void EnterEdge(object sender,MouseEventArgs e){leave.Stop();if(touching||Machine.Target is IslandState.Hidden or IslandState.SystemHud or IslandState.Notification)return;if(Machine.Target!=IslandState.Expanded){edgeReveal=true;Machine.Expand();}}
    private void EnterIsland(object sender,MouseEventArgs e)=>EnterEdge(sender,e);
    private void LeaveIsland(object sender,MouseEventArgs e){leave.Stop();leave.Start();}
    private static bool Interactive(object? source)
    {
        var node=source as DependencyObject;
        while(node is not null){if(node is ButtonBase or TextBoxBase or Slider or GlassFader or ListBoxItem)return true;node=VisualTreeHelper.GetParent(node);}return false;
    }
    private Point Position(MouseEventArgs e)=>e.GetPosition(Stage);
    private void PointerDown(object sender,MouseButtonEventArgs e)
    {
        edgeReveal=false;if(e.StylusDevice is not null||Interactive(e.OriginalSource))return;
        pressPoint=Position(e);pressed=true;held=moved=false;
        CaptureMouse();
        if(e.ClickCount==2&&Machine.Target==IslandState.Notification){held=true;Machine.Expand(IslandPage.Notifications);Activate();return;}
        if(e.ClickCount==2){held=true;if(Machine.Page is IslandPage.Focus or IslandPage.Today)model.ToggleFocusCommand.Execute(null);else model.MediaCommand.Execute("toggle");return;}
        longPress.Start();
    }
    private void PointerMove(object sender,MouseEventArgs e)
    {
        var point=Position(e);var bounds=Glass.Bounds;
        light=new((point.X-bounds.X)/Math.Max(bounds.Width,1),(point.Y-bounds.Y)/Math.Max(bounds.Height,1));
        if(IsMouseOver)Wake();
        if(!pressed||e.LeftButton!=MouseButtonState.Pressed)return;
        var delta=point-pressPoint;if(delta.Length>12){moved=true;longPress.Stop();}
        if(delta.Y>32)Machine.Expand();else if(delta.Y< -28)Machine.Collapse();
    }
    private void PointerUp(object sender,MouseButtonEventArgs e)
    {
        if(e.StylusDevice is not null)return;longPress.Stop();
        if(pressed&&!held&&!moved&&Machine.Target!=IslandState.Expanded){Machine.Expand(Machine.Target==IslandState.Notification?IslandPage.Notifications:null);Activate();}pressed=false;if(IsMouseCaptured)ReleaseMouseCapture();
    }
    private void FingerDown(object sender,TouchEventArgs e)
    {
        edgeReveal=false;if(Interactive(e.OriginalSource))return;touching=true;pressed=true;held=moved=false;pressPoint=e.GetTouchPoint(Stage).Position;CaptureTouch(e.TouchDevice);longPress.Start();e.Handled=true;
    }
    private void FingerMove(object sender,TouchEventArgs e)
    {
        if(!touching)return;var delta=e.GetTouchPoint(Stage).Position-pressPoint;if(delta.Length>12){moved=true;longPress.Stop();}
        if(delta.Y>28)Machine.Expand();if(delta.Y< -28)Machine.Collapse();e.Handled=true;
    }
    private void FingerUp(object sender,TouchEventArgs e)
    {
        if(!touching)return;longPress.Stop();var delta=e.GetTouchPoint(Stage).Position-pressPoint;
        if(Machine.Target==IslandState.Expanded&&Math.Abs(delta.X)>55)Machine.Select((IslandPage)(((int)Machine.Page+(delta.X<0?1:6))%7));
        else if(!held&&!moved){Machine.Expand(Machine.Target==IslandState.Notification?IslandPage.Notifications:null);Activate();}pressed=touching=false;ReleaseTouchCapture(e.TouchDevice);e.Handled=true;
    }
    private void KeyPressed(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape){Machine.Collapse();e.Handled=true;}
        if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key is >=Key.D1 and <=Key.D7){Machine.Select((IslandPage)(e.Key-Key.D1));e.Handled=true;}
    }
    private void FilesOver(object sender,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop)){Machine.Expand(IslandPage.Shelf);e.Effects=DragDropEffects.Link|DragDropEffects.Copy;e.Handled=true;}}
    private void FilesDropped(object sender,DragEventArgs e){if(e.Data.GetData(DataFormats.FileDrop) is string[] paths){model.AddFiles(paths);e.Handled=true;}}
    private void FileMouseDown(object sender,MouseButtonEventArgs e)=>filePoint=e.GetPosition(this);
    private void FileMouseMove(object sender,MouseEventArgs e)
    {
        if(e.LeftButton!=MouseButtonState.Pressed||sender is not ListBox list||list.SelectedItem is not ShelfRow file||Interactive(e.OriginalSource)&&e.OriginalSource is Button)return;
        if((e.GetPosition(this)-filePoint).Length<8||!System.IO.File.Exists(file.Path))return;
        DragDrop.DoDragDrop(list,new DataObject(DataFormats.FileDrop,new[]{file.Path}),DragDropEffects.Copy|DragDropEffects.Link);
    }
    private void VolumeReleased(object sender,MouseButtonEventArgs e)=>model.SetVolume(IslandVolume.Value);
    private void VolumeTouchReleased(object sender,TouchEventArgs e)=>model.SetVolume(IslandVolume.Value);
    private void VolumeKeyReleased(object sender,KeyEventArgs e){if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down)model.SetVolume(IslandVolume.Value);}
    private void RefreshAudio(object sender,RoutedEventArgs e)=>model.RefreshAudio();
    private void VolumeFaderCommitted(object? sender,EventArgs e)=>model.SetVolume(IslandVolume.Value);
    private async void BrightnessFaderCommitted(object? sender,EventArgs e)=>await model.SetBrightnessAsync((int)IslandBrightness.Value);
    private IntPtr NativeMessage(IntPtr hwnd,int message,IntPtr wp,IntPtr lp,ref bool handled)
    {
        if(message==0x0084){var x=(short)(lp.ToInt64()&0xffff);var y=(short)((lp.ToInt64()>>16)&0xffff);var p=PointFromScreen(new Point(x,y));p=new(p.X/scale,p.Y/scale);if(!ContainsSurface(p)||Machine.Target==IslandState.Hidden){handled=true;return new IntPtr(-1);}}
        if(message==0x02E0)Dispatcher.BeginInvoke(()=>Place(model.Preferences));
        return IntPtr.Zero;
    }
    private void DisposeMotion(){disposed=true;CompositionTarget.Rendering-=RenderFrame;expiry.Stop();leave.Stop();longPress.Stop();model.PropertyChanged-=ModelChanged;model.IslandNotice-=Notice;model.SystemLevelChanged-=SystemHud;Machine.Changed-=Retarget;}
    private bool ContainsSurface(Point point)
    {
        if(Machine.Target!=IslandState.Hidden&&new Rect(232,0,296,12).Contains(point))return true;
        var r=Glass.Bounds;if(Machine.Target==IslandState.Stowed){r.Inflate(8,8);return r.Contains(point);}if(!r.Contains(point))return false;var corner=Math.Min(Glass.Radius,r.Height/2);
        var x=Math.Clamp(point.X,r.Left+corner,r.Right-corner);var y=Math.Clamp(point.Y,r.Top+corner,r.Bottom-corner);
        return (point-new Point(x,y)).LengthSquared<=corner*corner;
    }
    [StructLayout(LayoutKind.Sequential)]private struct NativePoint{public int X,Y;}
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromPoint(NativePoint point,uint flags);
    [DllImport("shcore.dll")]private static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
}
