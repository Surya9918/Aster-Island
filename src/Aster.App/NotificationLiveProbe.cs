using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Aster.Platform.Windows;
namespace Aster.App;
internal static class NotificationLiveProbe
{
 public static async Task RunAsync(OverlayWindow island,AppModel model,string directory)
 {
  Directory.CreateDirectory(directory);
  if(!NotificationService.HasIdentity||!NotificationService.Allowed)throw new InvalidOperationException("Windows identity or user permission missing.");
  model.UpdatePreferences(model.Preferences with{NotificationsEnabled=true});
  var marker="Aster delivery check "+Guid.NewGuid().ToString("N");
  var xml=new XmlDocument();xml.LoadXml("<toast><visual><binding template='ToastGeneric'><text>"+marker+"</text><text>Temporary local notification pipeline test. This message will remove itself.</text></binding></visual></toast>");
  var toast=new ToastNotification(xml){Tag="astertest",Group="validation",SuppressPopup=true};
  var notifier=ToastNotificationManager.CreateToastNotifier();bool received=false;
  try{
   notifier.Show(toast);
   for(int i=0;i<10&&!received;i++){await Task.Delay(500);await model.RefreshWindowsNotifications();received=model.NotificationItems.Any(n=>n.Title==marker);}
   if(!received)throw new InvalidOperationException("Windows fixture did not reach the actual inbox.");
   var listener=UserNotificationListener.Current;
   foreach(var n in await listener.GetNotificationsAsync(NotificationKinds.Toast))
   {try{if(n.AppInfo.AppUserModelId.StartsWith("Aster.Island.Desktop_",StringComparison.Ordinal)&&n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements().Any(t=>t.Text==marker)==true)listener.RemoveNotification(n.Id);}catch{}}
   await Task.Delay(1000);await model.RefreshWindowsNotifications();bool removed=!model.NotificationItems.Any(n=>n.Title==marker);
   if(!removed)throw new InvalidOperationException("Removed Windows fixture stayed in inbox.");
   model.ConnectNotificationsCommand.Execute(null);await Task.Delay(300);bool disconnected=!model.Preferences.NotificationsEnabled&&!model.NotificationItems.Any(n=>n.External);
   if(!disconnected)throw new InvalidOperationException("Disconnect failed.");
   await File.WriteAllTextAsync(Path.Combine(directory,"windows-delivery.json"),JsonSerializer.Serialize(new{packageIdentity=true,windowsPermission=true,actualWindowsToastReceived=received,windowsRemovalSynced=removed,disconnectClearsMemory=disconnected,personalNotificationTextRecorded=false}));
  }finally{notifier.Hide(toast);}
 }
}
