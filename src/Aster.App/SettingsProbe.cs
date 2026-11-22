using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aster.Core;
namespace Aster.App;
internal static class SettingsProbe
{
 static T? Find<T>(DependencyObject node,string name)where T:DependencyObject{if(node is T t&&AutomationProperties.GetName(node)==name)return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var result=Find<T>(VisualTreeHelper.GetChild(node,i),name);if(result is not null)return result;}return null;}
 static void Capture(Window window,string path){window.UpdateLayout();var b=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(window);var p=new PngBitmapEncoder();p.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(path);p.Save(f);}
 public static async Task RunAsync(OverlayWindow island,AppModel model,string directory)
 {
  Directory.CreateDirectory(directory);var opened=new System.Collections.Generic.List<string>();var window=new PersonalizationWindow(model,opened.Add,true);window.Show();await Task.Delay(900);Capture(window,Path.Combine(directory,"01-look.png"));
  window.SelectForTest("Eyes");await Task.Delay(600);Capture(window,Path.Combine(directory,"02-eyes.png"));
  var toggle=Find<QuietSwitch>(window,"Show the eyes")??throw new InvalidOperationException("Eye switch missing.");toggle.IsChecked=false;await Task.Delay(350);if(model.Preferences.EyesVisible||model.Store.Load("preferences.json",new Preferences()).EyesVisible)throw new InvalidOperationException("Eye switch did not save.");
  model.UpdatePreferences(model.Preferences with{AutoStowMaximized=false});window.UndoLast();if(!model.Preferences.EyesVisible||model.Preferences.AutoStowMaximized)throw new InvalidOperationException("Undo overwrote an unrelated preference.");
  var range=Find<Slider>(window,"Eye size")??throw new InvalidOperationException("Eye slider missing.");if(VisualTreeHelper.HitTest(range,new Point(range.ActualWidth*.25,5)) is null)throw new InvalidOperationException("Slider touch target missing.");
  range.Value=.9;await Task.Delay(350);if(Math.Abs(model.Preferences.EyeScale-.9)>.001)throw new InvalidOperationException("Eye size not applied.");
  window.SearchForTest("brightness");if(window.VisibleGroupCount!=1)throw new InvalidOperationException("Search did not find the device controls.");Capture(window,Path.Combine(directory,"03-search.png"));window.SearchForTest("");
  foreach(var category in new[]{"Activities","Alerts","Privacy & startup"}){window.SelectForTest(category);await Task.Delay(700);Capture(window,Path.Combine(directory,category.StartsWith("Privacy")?"06-privacy.png":category=="Activities"?"04-activities.png":"05-alerts.png"));}
  window.SelectForTest("Activities");var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle(Brushes.LightGray,null,new Rect(0,0,8,8));var image=new RenderTargetBitmap(8,8,96,96,PixelFormats.Pbgra32);image.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=new MemoryStream()){encoder.Save(stream);model.UpdateMedia(new("Original local artwork fixture","",true,false,stream.ToArray()));}await Task.Delay(350);if(model.DisplayedMediaArtwork is null)throw new InvalidOperationException("Artwork fixture missing.");
  var artToggle=Find<QuietSwitch>(window,"Album covers")??throw new InvalidOperationException("Artwork switch missing.");artToggle.IsChecked=false;await Task.Delay(350);if(model.DisplayedMediaArtwork is not null||model.MediaArtwork is null)throw new InvalidOperationException("Artwork display switch failed.");
  model.UpdatePreferences(model.Preferences with{FocusMinutes=40,BreakMinutes=7});model.ResetFocusCommand.Execute("focus");if(model.TimerText!="40:00")throw new InvalidOperationException("Focus length not applied.");model.ResetFocusCommand.Execute("break");if(model.TimerText!="07:00")throw new InvalidOperationException("Break length not applied.");
  model.UpdatePreferences(model.Preferences with{NotificationBanners=false});island.Machine.Collapse();model.PublishNotice("Quiet test","Stored without a banner");if(island.Machine.Target==IslandState.Notification||model.NotificationItems.Count==0)throw new InvalidOperationException("Quiet notification policy failed.");
  model.UpdatePreferences(model.Preferences with{VolumeHudEnabled=false});island.Machine.Collapse();model.ReceiveAudio(new(.35f,false),true);if(island.Machine.Target==IslandState.SystemHud)throw new InvalidOperationException("Disabled volume HUD appeared.");
  model.UpdatePreferences(model.Preferences with{VolumeHudEnabled=true});model.ReceiveAudio(new(.35f,false),true);if(island.Machine.Target!=IslandState.SystemHud)throw new InvalidOperationException("Enabled volume HUD missing.");
  model.UpdatePreferences(model.Preferences with{NotificationBanners=true});model.PublishNotice("Background message","This must not interrupt a volume adjustment.");if(island.Machine.Target!=IslandState.SystemHud)throw new InvalidOperationException("Notification interrupted a system control.");
  var rootButton=new Button();var caption=new TextBlock{Text="Remove"};rootButton.Content=caption;rootButton.ApplyTemplate();if(!InteractionOrigin.IsButton(caption))throw new InvalidOperationException("Nested button content may start a file drag.");
  model.UpdatePreferences(model.Preferences with{ShowMedia=false});island.Machine.Collapse();model.UpdateMedia(new("Local fixture","",true,true));await Task.Delay(300);if(island.Machine.PrimaryActivity?.Kind==ActivityKind.Media)throw new InvalidOperationException("Disabled media activity appeared.");
  window.SelectForTest("Look & feel");var clockToggle=Find<QuietSwitch>(window,"Small clock")??throw new InvalidOperationException("Clock toggle missing.");clockToggle.IsChecked=false;await Task.Delay(350);island.Machine.SetActivity(null,ActivityKind.Media);island.Machine.Collapse();await Task.Delay(900);var eye=(FrameworkElement)island.FindName("CompactEye");if(Math.Abs(Canvas.GetLeft(eye)-(island.SurfaceBounds.X+island.SurfaceBounds.Width/2-15))>1)throw new InvalidOperationException("Eyes-only layout not centered.");
  window.Width=850;window.Height=640;window.SelectForTest("Look & feel");await Task.Delay(700);Capture(window,Path.Combine(directory,"07-small-window.png"));
  window.Exiting=true;window.Close();await File.WriteAllTextAsync(Path.Combine(directory,"settings.json"),JsonSerializer.Serialize(new{realEyeSwitchPersisted=true,realEyeSliderApplied=true,undoPreservesOtherChoices=true,search=true,focusDurations=true,quietNotificationInbox=true,volumeHudToggle=true,artworkDisplayToggle=true,mediaVisibility=true,systemControlsKeepPriority=true,nestedButtonDragGuard=true,smallWindowRendered=true,eyesOnlyCentered=true,sliderTouchTarget=true,systemStartupModified=false,externalPermissionRequested=false}));
 }
}
