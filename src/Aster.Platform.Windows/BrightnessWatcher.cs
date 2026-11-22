using System.Management;

namespace Aster.Platform.Windows;

public sealed class BrightnessWatcher:IDisposable
{
    private readonly ManagementEventWatcher watcher;
    public BrightnessWatcher(Action<int> changed)
    {
        watcher=new(new ManagementScope(@"\\.\root\WMI"),new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
        watcher.EventArrived+=(_,args)=>
        {try{using var value=args.NewEvent;changed(Convert.ToInt32(value["Brightness"]));}catch{/* Ignore provider events without a usable brightness field. */}};
        try{watcher.Start();}catch{watcher.Dispose();throw;}
    }
    public void Dispose(){try{watcher.Stop();}catch(ManagementException){}watcher.Dispose();}
}
