using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Aster.Core;

namespace Aster.App;

/// <summary>Retained vector glass. No desktop sampling, blur shader or native-window resizing.</summary>
public sealed class GlassSurface : FrameworkElement
{
    private readonly SolidColorBrush ink=new(Color.FromArgb(240,7,7,7)), white=new(Color.FromRgb(245,245,245)),muted=new(Color.FromRgb(150,150,150)),mint=new(Color.FromRgb(212,212,212));
    private readonly LinearGradientBrush wash=new(new GradientStopCollection{new(Color.FromArgb(37,157,157,157),0),new(Color.FromArgb(5,30,30,30),.48),new(Color.FromArgb(16,70,70,70),1)},new Point(0,0),new Point(1,1));
    private readonly LinearGradientBrush rim=new(new GradientStopCollection{new(Color.FromArgb(110,225,225,225),0),new(Color.FromArgb(20,182,182,182),.32),new(Color.FromArgb(10,127,127,127),.7),new(Color.FromArgb(64,189,189,189),1)},new Point(0,0),new Point(1,1));
    private readonly RadialGradientBrush sheen=new(new GradientStopCollection{new(Color.FromArgb(25,236,236,236),0),new(Colors.Transparent,1)}){RadiusX=.7,RadiusY=.7};
    private readonly Dictionary<(string,string,double,double),FormattedText> cache=[];
    private readonly RectangleGeometry surfaceClip=new();
    private readonly Pen rimPen;
    private readonly SolidColorBrush[] shadows=new SolidColorBrush[8];
    private ImageSource? cachedArt;private readonly ImageBrush artBrush=new(){Stretch=Stretch.UniformToFill};
    public GlassSurface(){rimPen=new(rim,.8);for(int i=0;i<shadows.Length;i++){shadows[i]=new(Color.FromArgb((byte)(i==1?35:8),0,0,0));shadows[i].Freeze();}}
    private AppModel? model; private IslandStateMachine? state;
    public Rect Bounds {get;private set;}=new(256,20,248,48);
    public double Radius {get;private set;}=24;
    private double nightLevel;
    private double alpha=1,expanded,clock,hover,progress,notice,hud,hudValue;private Point pointer=new(.5,.1);
    public void ClearPrivateText(){cache.Clear();InvalidateVisual();}
    public void Bind(AppModel value,IslandStateMachine machine){model=value;state=machine;}
    public void Frame(double width,double height,double radius,double opacity,double reveal,double time,double hoverAmount,double focusProgress,double notificationOpacity,Point light,double surfaceTop=20,double hudOpacity=0,double level=0,double nightAmount=0)
    {nightLevel=nightAmount;Bounds=new((ActualWidth-width)/2,surfaceTop,width,height);Radius=radius;alpha=opacity;expanded=reveal;clock=time;hover=hoverAmount;progress=focusProgress;notice=notificationOpacity;pointer=light;hud=hudOpacity;hudValue=level;InvalidateVisual();}
    protected override void OnRender(DrawingContext dc)
    {
        if(model is null||state is null||alpha<.001)return;
        var r=Bounds;var rad=Math.Min(Radius,r.Height/2);dc.PushOpacity(Math.Clamp(alpha,0,1));
        for(var i=r.Height<18?0:7;i>=1;i--)dc.DrawRoundedRectangle(shadows[i],null,new Rect(r.X-i*1.8,r.Y+3-i*.65,r.Width+i*3.6,r.Height+i*1.8),rad+i*1.8,rad+i*1.8);
        double night=nightLevel;
        ink.Color=Color.FromArgb((byte)(model.Preferences.GlassOpacity*255),7,7,7);
        sheen.GradientStops[0].Color=Color.FromArgb((byte)(25*model.Preferences.GlassShine*(1-.75*night)),235,235,235);
        dc.DrawRoundedRectangle(ink,null,r,rad,rad);dc.PushOpacity(1-.6*night);dc.DrawRoundedRectangle(wash,null,r,rad,rad);dc.Pop();
        sheen.Center=sheen.GradientOrigin=new(Math.Clamp(pointer.X,.1,.9),Math.Clamp(pointer.Y,0,.8));
        dc.DrawRoundedRectangle(sheen,null,r,rad,rad);dc.DrawRoundedRectangle(null,rimPen,new Rect(r.X+.5,r.Y+.5,r.Width-1,r.Height-1),rad,rad);
        rimPen.Thickness=.8+hover*.25;surfaceClip.Rect=r;surfaceClip.RadiusX=surfaceClip.RadiusY=rad;dc.PushClip(surfaceClip);dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(25,247,247,247)),null,new Rect(r.X+r.Width*.22,r.Y+.7,r.Width*.42,.8),.4,.4);
        if(r.Height<18){dc.Pop();dc.Pop();return;}
        var gate=Math.Clamp((125-r.Height)/40,0,1);gate=gate*gate*(3-2*gate);
        var hudAlpha=Math.Clamp(hud,0,1)*Math.Clamp((r.Height-35)/35,0,1)*gate;
        if(hudAlpha>.001)
        {
            dc.PushOpacity(hudAlpha);var brightness=state.HudKind==SystemHudKind.Brightness;
            Text(dc,brightness?"Brightness":state.HudMuted?"Muted":"Volume",13,white,new(r.X+29,r.Y+13),r.Width-100);
            Text(dc,$"{Math.Round(hudValue):0}%",12,mint,new(r.Right-58,r.Y+14),42);
            var track=new Rect(r.X+28,r.Y+43,r.Width-56,5);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(35,255,255,255)),null,track,2.5,2.5);
            if(hudValue>.01)dc.DrawRoundedRectangle(mint,null,new Rect(track.X,track.Y,track.Width*Math.Clamp(hudValue/100,0,1),5),2.5,2.5);
            dc.Pop();
        }
        if(notice>.001)
        {
            dc.PushOpacity(Math.Clamp(notice,0,1));
            Text(dc,state.NoticeTitle,16,white,new(r.X+55,r.Y+16),r.Width-80);Text(dc,state.NoticeDetail,12,muted,new(r.X+55,r.Y+42),r.Width-82);dc.Pop();
        }
        if(expanded<.999&&notice<.999&&hudAlpha<.999)
        {
            var compactPhase=Math.Clamp((r.Height-60)/45,0,1);compactPhase=1-compactPhase*compactPhase*(3-2*compactPhase);
            var stowFade=Math.Clamp((r.Height-16)/22,0,1);compactPhase*=stowFade*stowFade*(3-2*stowFade);
            dc.PushOpacity(Math.Clamp(compactPhase*(1-notice)*(1-hudAlpha),0,1));
            if(model.DisplayedMediaArtwork is { } art&&model.MediaAvailable)
            {
                var tile=new Rect(r.X+14,r.Y+r.Height/2-13,26,26);
                dc.PushClip(new RectangleGeometry(tile,7,7));
                if(!ReferenceEquals(cachedArt,art)){cachedArt=art;artBrush.ImageSource=art;}dc.DrawRectangle(artBrush,null,tile);dc.Pop();
            }

            var active=state.PrimaryActivity;var label=active?.Kind==ActivityKind.Focus?"Focus":active?.Kind==ActivityKind.Media?Trim(model.MediaTitle,24):"";
            if(label.Length>0)Text(dc,label,13.5,white,new(r.X+47,r.Y+r.Height/2-10),r.Width-125);
            if(active?.Kind==ActivityKind.Focus){Text(dc,model.TimerText,12.5,mint,new(r.Right-75,r.Y+r.Height/2-9),56);dc.DrawEllipse(mint,null,new(r.Right-20,r.Y+r.Height/2),2.5,2.5);}
            else if(active?.Kind==ActivityKind.Media&&model.MediaPlaying){for(var i=0;i<5;i++){var h=4+10*(.5+.5*Math.Sin(clock*4+i*1.6));dc.DrawRoundedRectangle(mint,null,new Rect(r.Right-45+i*4,r.Y+r.Height/2-h/2,2,h),1,1);}}
            else if(model.Preferences.ShowClock){double clockWidth=model.Preferences.Use12HourClock?76:50;Text(dc,model.ClockText,12,muted,new(model.Preferences.EyesVisible?r.Right-clockWidth-12:r.X+r.Width/2-clockWidth/2,r.Y+r.Height/2-9),clockWidth);}
            dc.Pop();
        }
        if(model.FocusRunning&&r.Height<100&&state.Target is not(IslandState.Notification or IslandState.SystemHud))
        {dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(45,212,212,212)),null,new Rect(r.X+24,r.Bottom-4,r.Width-48,1.5),.75,.75);if(progress>.0001)dc.DrawRoundedRectangle(mint,null,new Rect(r.X+24,r.Bottom-4,(r.Width-48)*Math.Clamp(progress,0,1),1.5),.75,.75);}
        dc.Pop();dc.Pop();
    }
    private void Text(DrawingContext dc,string text,double size,Brush brush,Point p,double max)
    {
        brush=Brushes.White;var dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;var key=(text,model?.Preferences.Font??"Segoe UI",size,dpi);
        if(!cache.TryGetValue(key,out var value)){if(cache.Count>80)cache.Clear();value=new(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(model?.Preferences.Font??"Segoe UI"),size,brush,dpi){Trimming=TextTrimming.CharacterEllipsis,MaxLineCount=1};cache[key]=value;}
        value.MaxTextWidth=Math.Max(1,max);dc.DrawText(value,p);
    }
    private static string Trim(string text,int max)=>text.Length>max?text[..max]+"…":text;
}
