using Aster.Core;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
namespace Aster.Platform.Windows;

/// <summary>Explicit consent, local Windows notification center only. No network or disk history.</summary>
public sealed class NotificationService
{
 public static bool HasIdentity {get{try{_ = global::Windows.ApplicationModel.Package.Current.Id.Name;return true;}catch{return false;}}}
 public static bool Allowed {get{try{return HasIdentity&&UserNotificationListener.Current.GetAccessStatus()==UserNotificationListenerAccessStatus.Allowed;}catch{return false;}}}
 public async Task<string> RequestAsync()
 {
  if(!HasIdentity)return "Install Aster’s notification identity package first. See the notification setup guide.";
  try{return await UserNotificationListener.Current.RequestAccessAsync() switch
  {UserNotificationListenerAccessStatus.Allowed=>"Connected · notifications stay on this laptop",UserNotificationListenerAccessStatus.Denied=>"Access denied. Allow Aster in Windows notification privacy settings, then reconnect.",_=>"Permission was not granted. Choose Connect to try again."};}
  catch{return "Windows could not request notification access. Check the installed identity and Windows privacy settings.";}
 }
 public async Task<IReadOnlyList<IslandNotification>> ReadAsync()
 {
  if(!Allowed)throw new UnauthorizedAccessException();
  var result=new List<IslandNotification>();
  foreach(var item in (await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast)).OrderByDescending(n=>n.CreationTime).Take(100))
  {
   try{
    var binding=item.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
    if(binding is null)continue;var text=binding.GetTextElements().Select(t=>t.Text??"").ToArray();
    var app=item.AppInfo.AppUserModelId;var key=$"{app}:{item.Id}:{item.CreationTime.UtcTicks}";
    result.Add(new(key,app,item.AppInfo.DisplayInfo.DisplayName,text.FirstOrDefault()??"Notification",string.Join("\n",text.Skip(1)),item.CreationTime,true));
   }catch{/* An expired or malformed item must not block other applications. */}
  }
  return result;
 }
}

