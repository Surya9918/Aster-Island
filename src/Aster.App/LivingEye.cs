using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Aster.Core;
namespace Aster.App;
/// <summary>Two friendly vector eyes. Only transient pointer coordinates drive gaze; no screen/camera access.</summary>
public sealed class LivingEye:FrameworkElement
{
 private readonly SpringValue gazeX=new(0),gazeY=new(0),joy=new(.72);
 private readonly DispatcherTimer attention=new(){Interval=TimeSpan.FromMilliseconds(80)},blinkTimer=new(){Interval=TimeSpan.FromSeconds(6.4)};
 private readonly Stopwatch lifetime=Stopwatch.StartNew(),frameClock=new(),blinkClock=new();
 private readonly StreamGeometry[] eyes=[new(),new()];private bool attached,blinking,wasReduced;private double openness=1,happyUntil=1.6;private Window? owner;private TimeSpan lastFrame=TimeSpan.MinValue;
 [StructLayout(LayoutKind.Sequential)]private struct Pointer{public int X,Y;}
 [DllImport("user32.dll")]private static extern bool GetCursorPos(out Pointer point);
 public LivingEye()
 {
  IsHitTestVisible=false;
  Loaded+=(_,_)=>{owner=Window.GetWindow(this);attention.Start();blinkTimer.Start();UpdateAttention();Wake();};
  Unloaded+=(_,_)=>{attention.Stop();blinkTimer.Stop();Stop();owner=null;};
  attention.Tick+=(_,_)=>{if(UpdateAttention())Wake();};
  blinkTimer.Tick+=(_,_)=>{if(!CanAnimate()||(DataContext as AppModel)?.Preferences.ReducedMotion==true)return;blinking=true;blinkClock.Restart();happyUntil=lifetime.Elapsed.TotalSeconds+1.3;UpdateAttention();Wake();};
 }
 private bool CanAnimate()=>owner is not null&&IsVisible&&Opacity>.01&&owner.IsVisible;
 private bool UpdateAttention()
 {
  if(!CanAnimate())return false;var prefs=(DataContext as AppModel)?.Preferences;
  if(prefs?.ReducedMotion==true){if(!wasReduced){gazeX.Snap(0);gazeY.Snap(0);joy.Snap(.55);blinking=false;openness=1;InvalidateVisual();}wasReduced=true;return false;}wasReduced=false;
  if(!GetCursorPos(out var cursor))return false;
  Point pointer;try{pointer=owner!.PointFromScreen(new Point(cursor.X,cursor.Y));}catch{return false;}
  double gx=Math.Clamp((pointer.X/Math.Max(1,owner!.ActualWidth)-.5)*2,-1,1),gy=Math.Clamp(pointer.Y/Math.Max(1,owner.ActualHeight),-.6,1);
  bool nearby=false;if(owner is OverlayWindow island){double scale=owner.ActualWidth/760;nearby=island.SurfaceBounds.Contains(new Point(pointer.X/scale,pointer.Y/scale));}
  double happiness=nearby?.92:lifetime.Elapsed.TotalSeconds<happyUntil?.78:.22;
  bool changed=Math.Abs(gazeX.Target-gx)>.005||Math.Abs(gazeY.Target-gy)>.005||Math.Abs(joy.Target-happiness)>.005;
  gazeX.Target=gx;gazeY.Target=gy;joy.Target=happiness;return changed;
 }
 private void Wake(){if(attached||!IsLoaded)return;attached=true;lastFrame=TimeSpan.MinValue;frameClock.Restart();CompositionTarget.Rendering+=Frame;}
 private void Stop(){if(!attached)return;attached=false;CompositionTarget.Rendering-=Frame;}
 private void Frame(object? sender,EventArgs e)
 {
  if(e is RenderingEventArgs timing){if(lastFrame==timing.RenderingTime)return;lastFrame=timing.RenderingTime;}
  if(!CanAnimate()){Stop();return;}UpdateAttention();double dt=Math.Min(frameClock.Elapsed.TotalSeconds,.05);frameClock.Restart();var prefs=(DataContext as AppModel)?.Preferences;
  if(prefs?.ReducedMotion==true){Stop();return;}
  var response=NightMotion.Adapt(new(350,37,1),prefs?.NightMode==true);gazeX.Step(dt,response);gazeY.Step(dt,response);joy.Step(dt,response);
  if(blinking){double t=blinkClock.Elapsed.TotalSeconds/(prefs?.NightMode==true?.36:.24);if(t>=1){blinking=false;openness=1;}else openness=1-.98*Math.Pow(Math.Sin(Math.PI*t),2);}
  InvalidateVisual();if(!blinking&&gazeX.Settled&&gazeY.Settled&&joy.Settled)Stop();
 }
 protected override void OnRender(DrawingContext dc)
 {
  double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;double smile=Math.Clamp(joy.Position,0,1),rx=w*.177,ry=h*.26,y=h*.55;
  double strength=(DataContext as AppModel)?.Preferences.NightMode==true?.65:1;
  for(int i=0;i<2;i++)
  {
   double x=w*(i==0?.26:.74),lower=ry*(1.33*(1-smile)-.75*smile);
   var geometry=eyes[i];using(var c=geometry.Open()){c.BeginFigure(new(x-rx,y),true,true);c.BezierTo(new(x-rx,y-ry*1.33),new(x+rx,y-ry*1.33),new(x+rx,y),true,false);c.BezierTo(new(x+rx,y+lower),new(x-rx,y+lower),new(x-rx,y),true,false);}
   dc.PushTransform(new ScaleTransform(1,openness,x,y));dc.DrawGeometry(Brushes.White,null,geometry);
   dc.PushClip(geometry);dc.PushOpacity(1-smile*.95);
   var pupil=new Point(x+gazeX.Position*rx*.32*strength,y+gazeY.Position*ry*.26*strength);
   dc.DrawEllipse(Brushes.Black,null,pupil,rx*.48,ry*.66);dc.DrawEllipse(Brushes.White,null,new(pupil.X+.65,pupil.Y-1),.65,.65);
   dc.Pop();dc.Pop();dc.Pop();
  }
 }
}
