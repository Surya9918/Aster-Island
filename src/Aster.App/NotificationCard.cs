using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aster.Core;
namespace Aster.App;
/// <summary>One retained card: physics-driven entrance and dismiss, detached when settled or unloaded.</summary>
public sealed class NotificationCard:Border
{
 private readonly SpringValue visibility=new(0);private readonly TranslateTransform offset=new();private readonly Stopwatch clock=new();
 private bool attached,exiting;private TaskCompletionSource? completion;
 public NotificationCard(){Opacity=0;RenderTransform=offset;Loaded+=(_,_)=>{if(!exiting){visibility.Target=1;Wake();}};Unloaded+=(_,_)=>{Stop();completion?.TrySetResult();};}
 private void Wake(){if(attached)return;attached=true;clock.Restart();CompositionTarget.Rendering+=Frame;}
 private void Stop(){if(!attached)return;attached=false;CompositionTarget.Rendering-=Frame;}
 private void Frame(object? sender,EventArgs e){var dt=Math.Min(clock.Elapsed.TotalSeconds,.05);clock.Restart();var reduced=(Window.GetWindow(this)?.DataContext as AppModel)?.Preferences.ReducedMotion==true;if(reduced)visibility.Snap(visibility.Target);else visibility.Step(dt,new(430,40,1));Opacity=Math.Clamp(visibility.Position,0,1);offset.Y=exiting?0:(1-visibility.Position)*10;offset.X=exiting?(1-visibility.Position)*24:0;if(visibility.Settled){Stop();completion?.TrySetResult();}}
 public Task ExitAsync(bool reduced){exiting=true;visibility.Target=0;if(reduced){visibility.Snap(0);Opacity=0;return Task.CompletedTask;}completion??=new(TaskCreationOptions.RunContinuationsAsynchronously);Wake();return completion.Task;}
}
/// <summary>Reflows notification groups/cards with retained velocity when arrivals or dismissals interrupt layout.</summary>
public sealed class LiquidStackPanel:Panel
{
 private readonly System.Collections.Generic.Dictionary<UIElement,SpringValue> springs=[];private readonly Stopwatch clock=new();private bool attached;
 public LiquidStackPanel(){Unloaded+=(_,_)=>Stop();Loaded+=(_,_)=>Wake();}
 protected override Size MeasureOverride(Size available){double h=0,w=0;foreach(UIElement c in InternalChildren){c.Measure(new(available.Width,double.PositiveInfinity));h+=c.DesiredSize.Height;w=Math.Max(w,c.DesiredSize.Width);}return new(w,h);}
 protected override Size ArrangeOverride(Size size){double y=0;foreach(UIElement c in InternalChildren){if(!springs.TryGetValue(c,out var spring)){spring=new(y+6);springs[c]=spring;}spring.Target=y;c.Arrange(new Rect(0,spring.Position,size.Width,c.DesiredSize.Height));y+=c.DesiredSize.Height;}foreach(var c in System.Linq.Enumerable.ToArray(springs.Keys))if(!InternalChildren.Contains(c))springs.Remove(c);Wake();return size;}
 private void Wake(){if(attached||!IsLoaded)return;attached=true;clock.Restart();CompositionTarget.Rendering+=Frame;}
 private void Stop(){if(!attached)return;attached=false;CompositionTarget.Rendering-=Frame;}
 private void Frame(object? sender,EventArgs e){double dt=Math.Min(clock.Elapsed.TotalSeconds,.05);clock.Restart();bool done=true,reduced=(Window.GetWindow(this)?.DataContext as AppModel)?.Preferences.ReducedMotion==true;foreach(var pair in springs){if(reduced)pair.Value.Snap(pair.Value.Target);else pair.Value.Step(dt,new(440,42,1));done&=pair.Value.Settled;var c=pair.Key;c.Arrange(new Rect(0,pair.Value.Position,ActualWidth,c.DesiredSize.Height));}if(done)Stop();}
}

public sealed class LiquidExpander:Expander
{
 private Border? body;private ContentPresenter? presenter;private readonly SpringValue height=new(0);private readonly Stopwatch clock=new();private bool attached;
 public LiquidExpander(){Expanded+=(_,_)=>Retarget();Collapsed+=(_,_)=>Retarget();Unloaded+=(_,_)=>Stop();}
 public override void OnApplyTemplate(){base.OnApplyTemplate();body=GetTemplateChild("PART_Body") as Border;presenter=GetTemplateChild("PART_Content") as ContentPresenter;Retarget();}
 private void Retarget(){if(body is null||presenter is null)return;presenter.Measure(new Size(Math.Max(100,ActualWidth),double.PositiveInfinity));height.Target=IsExpanded?Math.Min(160,presenter.DesiredSize.Height):0;if(!attached){attached=true;clock.Restart();CompositionTarget.Rendering+=Frame;}}
 private void Stop(){if(!attached)return;attached=false;CompositionTarget.Rendering-=Frame;}
 private void Frame(object? sender,EventArgs e){double dt=Math.Min(clock.Elapsed.TotalSeconds,.05);clock.Restart();if((Window.GetWindow(this)?.DataContext as AppModel)?.Preferences.ReducedMotion==true)height.Snap(height.Target);else height.Step(dt,new(430,40,1));if(body is not null)body.Height=Math.Max(0,height.Position);if(height.Settled)Stop();}
}
