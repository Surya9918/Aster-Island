using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Aster.Core;
using Aster.Platform.Windows;
namespace Aster.App;
public sealed partial class AppModel
{
 private readonly NotificationInbox inbox=new();
 private readonly System.Collections.Generic.HashSet<string> unreadNotifications=[];
 public int NotificationUnread=>unreadNotifications.Count;
 public string NotificationBadge=>NotificationUnread==0?"":NotificationUnread>99?"99+":NotificationUnread.ToString();
 internal void MarkNotificationsRead(){if(unreadNotifications.Count==0)return;unreadNotifications.Clear();NotificationProperties();}
 private readonly NotificationService notificationService=new();
 public ObservableCollection<IslandNotification> NotificationItems {get;}=[];
 public ICollectionView NotificationGroups {get;private set;}=null!;
 public string NotificationStatus {get;private set;}="Connect Windows notifications · permission required";
 public string NotificationEmpty=>NotificationItems.Count==0?"You’re all caught up.":"";
 public string NotificationSummary=>$"{NotificationItems.Count} notification{(NotificationItems.Count==1?"":"s")} · this session";
 public string NotificationConnectionLabel=>Preferences.NotificationsEnabled?"Disconnect":"Connect Windows";
 public string NotificationPreviewLabel=>Preferences.NotificationPreviews?"Previews on":"Previews off";
 public ICommand ConnectNotificationsCommand {get;private set;}=null!;
 public ICommand ClearNotificationsCommand {get;private set;}=null!;
 public ICommand HideNotificationCommand {get;private set;}=null!;
 public ICommand NotificationSettingsCommand {get;private set;}=null!;
 public ICommand PreviewNotificationsCommand {get;private set;}=null!;
 private bool notificationBusy,notificationLocked;
 private int notificationGeneration,notificationTick;
 private void InitializeNotifications()
 {
  NotificationGroups=CollectionViewSource.GetDefaultView(NotificationItems);NotificationGroups.GroupDescriptions.Add(new PropertyGroupDescription(nameof(IslandNotification.AppName)));
  ConnectNotificationsCommand=new AsyncCommand(async _=>{
   if(Preferences.NotificationsEnabled){notificationGeneration++;inbox.Disconnect();UpdatePreferences(Preferences with{NotificationsEnabled=false});NotificationStatus="Disconnected · Windows messages cleared from memory";RefreshNotificationItems();}
   else{NotificationStatus=await notificationService.RequestAsync();if(NotificationService.Allowed){UpdatePreferences(Preferences with{NotificationsEnabled=true});await RefreshWindowsNotifications();}}
   NotificationProperties();
  },()=>{NotificationStatus="Unable to connect. Check Windows notification permissions.";NotificationProperties();});
  HideNotificationCommand=new ActionCommand(p=>{if(p is string key){inbox.Hide(key);RefreshNotificationItems();}});
  ClearNotificationsCommand=new ActionCommand(_=>{inbox.ClearVisible();RefreshNotificationItems();});
  NotificationSettingsCommand=new ActionCommand(_=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:notifications"){UseShellExecute=true});}catch{NotificationStatus="Open Windows Settings → System → Notifications.";NotificationProperties();}});
  PreviewNotificationsCommand=new ActionCommand(_=>{UpdatePreferences(Preferences with{NotificationPreviews=!Preferences.NotificationPreviews});NotificationProperties();});
 }
 internal void PublishNotice(string title,string body)
 {
  if(notificationLocked||closing)return;inbox.AddLocal(title,body,DateTimeOffset.Now);unreadNotifications.Add(inbox.Items.First().Key);RefreshNotificationItems();IslandNotice?.Invoke(title,Preferences.NotificationPreviews?body:"Open Notifications to read.");
 }
 private void TickNotifications()
 {
  if(++notificationTick%2==0&&Preferences.NotificationsEnabled&&!notificationLocked&&!closing)_=RefreshWindowsNotifications();
 }
 internal async Task RefreshWindowsNotifications()
 {
  if(notificationBusy||notificationLocked||closing||!Preferences.NotificationsEnabled)return;
  notificationBusy=true;var generation=notificationGeneration;
  try{
   if(!NotificationService.Allowed){inbox.Disconnect();NotificationStatus=NotificationService.HasIdentity?"Windows access is unavailable. Reconnect or check privacy settings.":"Notification identity is not installed. See the setup guide.";RefreshNotificationItems();return;}
   var snapshot=await notificationService.ReadAsync();if(generation!=notificationGeneration||notificationLocked||closing)return;
   var arrived=inbox.Sync(snapshot);foreach(var item in arrived)unreadNotifications.Add(item.Key);RefreshNotificationItems();NotificationStatus="Connected · local, session-only notification inbox";
   if(arrived.Count>0){var latest=arrived[0];IslandNotice?.Invoke(Preferences.NotificationPreviews?latest.AppName+" · "+latest.Title:latest.AppName,Preferences.NotificationPreviews?latest.Body:$"{arrived.Count} new notification{(arrived.Count==1?"":"s")} · open to read");}
  }catch{if(generation==notificationGeneration&&!closing){NotificationStatus="Windows notifications temporarily unavailable. Retrying while connected.";}}
  finally{notificationBusy=false;if(!closing)NotificationProperties();}
 }
 private void RefreshNotificationItems()
 {
  var wanted=notificationLocked?Array.Empty<IslandNotification>():inbox.Items.ToArray();
  for(int i=NotificationItems.Count-1;i>=0;i--)if(!wanted.Contains(NotificationItems[i]))NotificationItems.RemoveAt(i);
  for(int i=0;i<wanted.Length;i++){var at=NotificationItems.IndexOf(wanted[i]);if(at<0)NotificationItems.Insert(i,wanted[i]);else if(at!=i)NotificationItems.Move(at,i);}
  unreadNotifications.IntersectWith(wanted.Select(n=>n.Key));NotificationProperties();
 }
 private void NotificationProperties(){Changed(nameof(NotificationUnread));Changed(nameof(NotificationBadge));Changed(nameof(NotificationSummary));Changed(nameof(NotificationEmpty));Changed(nameof(NotificationStatus));Changed(nameof(NotificationConnectionLabel));Changed(nameof(NotificationPreviewLabel));}
 internal void LockNotifications(bool locked){notificationLocked=locked;notificationGeneration++;unreadNotifications.Clear();inbox.ClearAll();RefreshNotificationItems();}
 private void StopNotifications(){notificationGeneration++;unreadNotifications.Clear();inbox.ClearAll();NotificationItems.Clear();}
 internal void SeedNotificationTest(IslandNotification[] records){inbox.Sync([]);inbox.Sync(records);RefreshNotificationItems();}
}
