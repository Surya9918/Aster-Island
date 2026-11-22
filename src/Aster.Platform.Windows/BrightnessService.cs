using System.Management;
namespace Aster.Platform.Windows;
public static class BrightnessService
{
    public static int? Read()
    {
        using var query=new ManagementObjectSearcher(@"root\WMI","SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE");
        query.Options.Timeout=TimeSpan.FromSeconds(3);
        using var results=query.Get();
        foreach(ManagementObject monitor in results)using(monitor)return Convert.ToInt32(monitor["CurrentBrightness"]);
        return null;
    }
    public static void Set(int percent)
    {
        using var query=new ManagementObjectSearcher(@"root\WMI","SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=TRUE");
        query.Options.Timeout=TimeSpan.FromSeconds(3);
        using var results=query.Get();var changed=false;
        foreach(ManagementObject monitor in results)
        {
            using(monitor)
            {
                var result=monitor.InvokeMethod("WmiSetBrightness",new object[]{(uint)0,(byte)Math.Clamp(percent,0,100)});
                if(result is not null&&Convert.ToUInt32(result)!=0)throw new InvalidOperationException("Windows declined the brightness change.");
                changed=true;
            }
        }
        if(!changed)throw new NotSupportedException("This display has no Windows brightness provider.");
    }
}
