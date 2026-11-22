using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using Aster.Core;
namespace Aster.App;
internal static class NotificationProbe
{
 static T? Find<T>(DependencyObject root,Func<T,bool> match) where T:DependencyObject{if(root is T t&&match(t))return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var hit=Find(VisualTreeHelper.GetChild(root,i),match);if(hit!=null)return hit;}return null;}
 static void Capture(OverlayWindow island,string path){island.UpdateLayout();var b=new RenderTargetBitmap((int)island.ActualWidth,(int)island.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(island);var p=new PngBitmapEncoder();p.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(path);p.Save(f);}
 public static async Task RunAsync(OverlayWindow island,AppModel model,string directory)
 {
  Directory.CreateDirectory(directory);Directory.CreateDirectory(Path.Combine(directory,"frames"));
  var now=DateTimeOffset.Now;
  model.SeedNotificationTest(new[]{new IslandNotification("1","messages","Messages","Evening plans","Dinner at seven? Let me know when you’re ready.",now,true),new("2","messages","Messages","A quiet moment","Your messages stay on this laptop. Nothing is uploaded.",now.AddMinutes(-2),true),new("3","calendar","Calendar","Design review","Starts in 15 minutes · Studio",now.AddMinutes(-5),true)});
  island.Machine.Expand(IslandPage.Notifications);await Task.Delay(1400);
  if(Find<Button>(island,b=>b.Content as string=="Clear island")?.Command is null)throw new InvalidOperationException("Inbox controls missing.");
  Capture(island,Path.Combine(directory,"01-grouped.png"));
  var expander=Find<Expander>(island,_=>true)??throw new InvalidOperationException("Message detail missing.");expander.IsExpanded=true;await Task.Delay(600);Capture(island,Path.Combine(directory,"02-detail.png"));
  var dismiss=Find<Button>(island,b=>b.Content as string=="×")??throw new InvalidOperationException("Dismiss missing.");
  dismiss.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(1200);if(model.NotificationItems.Count!=2)throw new InvalidOperationException("Animated dismiss failed.");Capture(island,Path.Combine(directory,"03-dismissed.png"));
  island.ClearFrameSamples();
  for(int i=0;i<24;i++){if(i%4==0){if(i%8==0)island.Machine.Collapse();else island.Machine.Expand(IslandPage.Notifications);}await Task.Delay(25);Capture(island,Path.Combine(directory,"frames",$"frame-{i:000}.png"));}
  island.Machine.Expand(IslandPage.Notifications);await Task.Delay(1200);
  island.ClearFrameSamples();for(int i=0;i<8;i++){island.Machine.Collapse();await Task.Delay(300);island.Machine.Expand(IslandPage.Notifications);await Task.Delay(500);}await Task.Delay(900);
  var frames=island.FrameSamples.Where(f=>f.IntervalMs>0).ToArray();var ordered=frames.Select(f=>f.IntervalMs).Order().ToArray();
  if(frames.Any(f=>!double.IsFinite(f.Width)||!double.IsFinite(f.Height))||island.Machine.Current!=IslandState.Expanded)throw new InvalidOperationException("Interrupted spring failed to settle.");
  model.ClearNotificationsCommand.Execute(null);await Task.Delay(900);if(model.NotificationItems.Count!=0)throw new InvalidOperationException("Clear failed.");Capture(island,Path.Combine(directory,"04-empty.png"));
  island.Machine.Collapse();model.UpdatePreferences(model.Preferences with{NotificationPreviews=false});
  string? banner=null;void Observe(string title,string body)=>banner=body;model.IslandNotice+=Observe;model.PublishNotice("Privacy check","Private sample body");model.IslandNotice-=Observe;
  if(banner=="Private sample body"||model.NotificationUnread!=1)throw new InvalidOperationException("Preview privacy or unread tracking failed.");
  island.Machine.Expand(IslandPage.Notifications);if(model.NotificationUnread!=0)throw new InvalidOperationException("Read tracking failed.");
  model.UpdatePreferences(model.Preferences with{NotificationPreviews=true});model.ClearNotificationsCommand.Execute(null);
  model.SeedNotificationTest(new[]{new IslandNotification("4","chat","Chat","Private","Private content",now,true)});model.LockNotifications(true);island.ClearNotificationPrivacy();if(model.NotificationItems.Count!=0||island.Machine.NoticeTitle!="")throw new InvalidOperationException("Lock privacy failed.");model.LockNotifications(false);
  foreach(var scale in new[]{.8,1.0,1.2}){model.UpdatePreferences(model.Preferences with{IslandScale=scale});await Task.Delay(900);Capture(island,Path.Combine(directory,$"scale-{scale:0.0}.png"));}
  await File.WriteAllTextAsync(Path.Combine(directory,"notifications.json"),JsonSerializer.Serialize(new{groupedCards=true,detail=true,animatedDismiss=true,clear=true,lockPrivacy=true,previewPrivacy=true,unreadTracking=true,interruptionsSettled=true,samples=frames.Length,medianFrameMs=ordered.Length>0?ordered[ordered.Length/2]:0,p95FrameMs=ordered.Length>0?ordered[(int)((ordered.Length-1)*.95)]:0,maxUpdateMs=frames.Length>0?frames.Max(f=>f.UpdateMs):0,liveWindowsPermissionTested=false},new JsonSerializerOptions{WriteIndented=true}));
 }
}
