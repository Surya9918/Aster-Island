using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Aster.Core;
namespace Aster.App;
/// <summary>Local vector eye. Sleeps between blinks; pointer input only inside its own window.</summary>
public sealed class LivingEye:FrameworkElement
{
 private readonly SpringValue gazeX=new(0),gazeY=new(0);private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(6.4)};
 private readonly Stopwatch frameClock=new(),blinkClock=new();private bool attached,blinking;private double openness=1;private Window? owner;
 public LivingEye(){IsHitTestVisible=false;Loaded+=(_,_)=>{owner=Window.GetWindow(this);if(owner is not null){owner.MouseMove+=Look;owner.MouseLeave+=Rest;}timer.Start();};Unloaded+=(_,_)=>{timer.Stop();Stop();if(owner is not null){owner.MouseMove-=Look;owner.MouseLeave-=Rest;owner=null;}};timer.Tick+=(_,_)=>{if(!IsVisible||Opacity<.01||(DataContext as AppModel)?.Preferences.ReducedMotion==true)return;blinking=true;blinkClock.Restart();Wake();};}
 private void Look(object sender,MouseEventArgs e){if(!IsVisible||Opacity<.01||owner is null)return;var p=e.GetPosition(owner);double strength=(DataContext as AppModel)?.Preferences.NightMode==true?1.2:2.3;gazeX.Target=Math.Clamp((p.X/Math.Max(1,owner.ActualWidth)-.5)*2,-1,1)*strength;gazeY.Target=Math.Clamp((p.Y/Math.Max(1,owner.ActualHeight)-.3),-.7,.7)*strength;Wake();}
 private void Rest(object sender,MouseEventArgs e){gazeX.Target=gazeY.Target=0;Wake();}
 private void Wake(){if(attached||!IsLoaded)return;attached=true;frameClock.Restart();CompositionTarget.Rendering+=Frame;}
 private void Stop(){if(!attached)return;attached=false;CompositionTarget.Rendering-=Frame;}
 private void Frame(object? sender,EventArgs e){double dt=Math.Min(frameClock.Elapsed.TotalSeconds,.05);frameClock.Restart();var p=(DataContext as AppModel)?.Preferences;var c=NightMotion.Adapt(new(350,37,1),p?.NightMode==true);if(p?.ReducedMotion==true){gazeX.Snap(gazeX.Target);gazeY.Snap(gazeY.Target);blinking=false;openness=1;}else{gazeX.Step(dt,c);gazeY.Step(dt,c);if(blinking){double t=blinkClock.Elapsed.TotalSeconds/(p?.NightMode==true?.36:.24);if(t>=1){blinking=false;openness=1;}else openness=1-.96*Math.Pow(Math.Sin(Math.PI*t),2);}}InvalidateVisual();if(!blinking&&gazeX.Settled&&gazeY.Settled)Stop();}
 protected override void OnRender(DrawingContext dc){double x=ActualWidth/2,y=ActualHeight/2;dc.PushTransform(new ScaleTransform(1,openness,x,y));dc.DrawEllipse(Brushes.White,null,new(x,y),ActualWidth*.47,ActualHeight*.29);dc.DrawEllipse(Brushes.Black,null,new(x+gazeX.Position,y+gazeY.Position*.5),3.1,3.9);dc.DrawEllipse(Brushes.White,null,new(x+gazeX.Position+1,y+gazeY.Position*.5-1.4),.8,.8);dc.Pop();}
}
