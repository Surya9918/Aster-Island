using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Aster.Core;
using Aster.Platform.Windows;
namespace Aster.App;
public sealed partial class AppModel
{
 public string NightModeLabel=>Preferences.NightMode?"Night mode · on":"Night mode · off";
 public string HeadphoneStatus {get;private set;}="Checking audio outputs…";
 public ICommand ToggleNightCommand {get;private set;}=null!;
 private readonly HeadphoneTracker headphones=new();private IDisposable? headphoneWatch;private int headphonesDirty=1;private bool headphonesBusy;
 private void InitializeLive()=>ToggleNightCommand=new ActionCommand(_=>{UpdatePreferences(Preferences with{NightMode=!Preferences.NightMode});Changed(nameof(NightModeLabel));});
 private async Task StartHeadphones(){try{headphoneWatch=AudioDeviceService.Watch(()=>Interlocked.Exchange(ref headphonesDirty,1));await ReadHeadphones();}catch{HeadphoneStatus="Audio connection tracking unavailable";Changed(nameof(HeadphoneStatus));}}
 private void TickHeadphones(){if(!headphonesBusy&&Interlocked.Exchange(ref headphonesDirty,0)==1)_=ReadHeadphones();}
 private async Task ReadHeadphones(){if(headphonesBusy||closing)return;headphonesBusy=true;try{var devices=await Task.Run(AudioDeviceService.Read);if(closing)return;foreach(var message in headphones.Update(devices))if(Preferences.HeadphoneCards)PublishNotice(message.Title,message.Detail);HeadphoneStatus=devices.Count>0?"Audio connection cards enabled":"No audio output detected";Changed(nameof(HeadphoneStatus));}catch{HeadphoneStatus="Audio outputs temporarily unavailable";Changed(nameof(HeadphoneStatus));}finally{headphonesBusy=false;}}
 private void StopHeadphones(){headphoneWatch?.Dispose();headphoneWatch=null;}
}
