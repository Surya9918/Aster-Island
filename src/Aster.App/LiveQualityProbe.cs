using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using Aster.Core;
using Aster.Platform.Windows;
namespace Aster.App;
internal static class LiveQualityProbe
{
 static void Capture(OverlayWindow island,string path){island.UpdateLayout();var b=new RenderTargetBitmap((int)island.ActualWidth,(int)island.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(island);var p=new PngBitmapEncoder();p.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(path);p.Save(f);}
 static T? Find<T>(DependencyObject node,Func<T,bool> match) where T:DependencyObject{if(node is T t&&match(t))return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var result=Find(VisualTreeHelper.GetChild(node,i),match);if(result is not null)return result;}return null;}
 public static async Task NightAsync(OverlayWindow island,AppModel model,string directory)
 {
  Directory.CreateDirectory(directory);island.Machine.Expand(IslandPage.Controls);await Task.Delay(1000);
  var button=Find<System.Windows.Controls.Button>(island,b=>b.Command==model.ToggleNightCommand)??throw new InvalidOperationException("Night toggle missing.");
  button.Command.Execute(null);await Task.Delay(1000);if(!model.Preferences.NightMode||!model.Store.Load("preferences.json",new Preferences()).NightMode)throw new InvalidOperationException("Night mode did not persist.");Capture(island,Path.Combine(directory,"night-controls.png"));
  button.Command.Execute(null);await Task.Delay(900);if(model.Preferences.NightMode)throw new InvalidOperationException("Night mode could not be turned off.");Capture(island,Path.Combine(directory,"day-controls.png"));
  island.Machine.Collapse();await Task.Delay(800);Directory.CreateDirectory(Path.Combine(directory,"eye-frames"));
  for(int i=0;i<150;i++){await Task.Delay(50);Capture(island,Path.Combine(directory,"eye-frames",$"{i:000}.png"));}
  await File.WriteAllTextAsync(Path.Combine(directory,"night.json"),"{\"toggleBound\":true,\"persisted\":true,\"toggleOff\":true,\"naturalEyeRecording\":true}");
 }
 public static async Task RunAsync(OverlayWindow island,AppModel model,string directory)
 {
  Directory.CreateDirectory(directory);var checks=new List<object>();
  async Task State(string name,Action action){island.ClearFrameSamples();action();await Task.Delay(1200);var frames=island.FrameSamples.Where(f=>f.IntervalMs>0&&f.IntervalMs<100).ToArray();var times=frames.Select(f=>f.IntervalMs).Order().ToArray();checks.Add(new{name,frames=times.Length,medianMs=times.Length>0?times[times.Length/2]:0,p95Ms=times.Length>0?times[(int)((times.Length-1)*.95)]:0,maxUpdateMs=frames.Length>0?frames.Max(f=>f.UpdateMs):0,settled=island.Machine.Target==island.Machine.Current});Capture(island,Path.Combine(directory,name+".png"));}
  await State("01-eye-idle",()=>island.Machine.Collapse());
  await State("02-hover",()=>island.Machine.Hover(true));
  foreach(var page in Enum.GetValues<IslandPage>())await State("page-"+page,()=>island.Machine.Expand(page));
  await State("03-headphones",()=>{island.Machine.Collapse();model.PublishNotice("Headphones connected","Local audio output · ready to listen");});
  await State("04-volume",()=>island.Machine.ShowSystemHud(SystemHudKind.Volume,68));
  await State("05-brightness",()=>island.Machine.ShowSystemHud(SystemHudKind.Brightness,40));
  await State("06-focus",()=>{model.ToggleFocusCommand.Execute(null);island.Machine.Collapse();});
  model.ToggleFocusCommand.Execute(null);
  await State("07-night-controls",()=>{model.UpdatePreferences(model.Preferences with{NightMode=true});island.Machine.Expand(IslandPage.Controls);});
  await State("08-night-eye",()=>island.Machine.Collapse());
  await State("09-stowed",()=>island.Machine.SetStowed(true));
  await State("10-hidden",()=>island.Machine.SetHidden(true));
  model.UpdatePreferences(model.Preferences with{NightMode=false});island.SetVisible(true);
  // Sample older transitions without capture overhead, repeatedly changing their targets mid-flight.
  island.ClearFrameSamples();for(int i=0;i<30;i++){island.Machine.Expand((IslandPage)(i%7));await Task.Delay(180);island.Machine.Collapse();await Task.Delay(170);}await Task.Delay(900);
  var all=island.FrameSamples.Where(f=>f.IntervalMs>0&&f.IntervalMs<100).ToArray();var order=all.Select(f=>f.IntervalMs).Order().ToArray();
  if(all.Any(f=>!double.IsFinite(f.Width)||!double.IsFinite(f.Height))||island.Machine.Current!=island.Machine.Target)throw new InvalidOperationException("Interrupted legacy animation failed.");
  var watchEvents=0;using(var subscription=AudioDeviceService.Watch(()=>System.Threading.Interlocked.Increment(ref watchEvents))){var endpoints=await Task.Run(AudioDeviceService.Read);if(endpoints.Any(e=>string.IsNullOrWhiteSpace(e.Id)))throw new InvalidOperationException("Invalid endpoint enumeration.");}
  await File.WriteAllTextAsync(Path.Combine(directory,"quality.json"),JsonSerializer.Serialize(new{checks,legacyTransitionFrames=all.Length,legacyMedianMs=order.Length>0?order[order.Length/2]:0,legacyP95Ms=order.Length>0?order[(int)((order.Length-1)*.95)]:0,legacyMaxUpdateMs=all.Length>0?all.Max(f=>f.UpdateMs):0,nativeAudioEnumeration=true,nativeAudioSubscription=true,physicalHeadphonePlugTested=false,source="WPF composition callback timing; not physical photon or GPU-present latency"},new JsonSerializerOptions{WriteIndented=true}));
 }
}
