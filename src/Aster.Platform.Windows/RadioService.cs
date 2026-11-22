using Windows.Devices.Radios;

namespace Aster.Platform.Windows;

public static class RadioService
{
    public static async Task<string> ReadAsync(bool bluetooth)
    {
        var list=await Radio.GetRadiosAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var radio=list.FirstOrDefault(r=>r.Kind==(bluetooth?RadioKind.Bluetooth:RadioKind.WiFi));
        return radio?.State.ToString()??"Unavailable";
    }
    public static async Task<string> ToggleAsync(bool bluetooth)
    {
        var access=await Radio.RequestAccessAsync();if(access!=RadioAccessStatus.Allowed)throw new UnauthorizedAccessException("Windows did not allow radio control.");
        var list=await Radio.GetRadiosAsync();var radio=list.FirstOrDefault(r=>r.Kind==(bluetooth?RadioKind.Bluetooth:RadioKind.WiFi))??throw new NotSupportedException();
        var result=await radio.SetStateAsync(radio.State==RadioState.On?RadioState.Off:RadioState.On);
        if(result!=RadioAccessStatus.Allowed)throw new UnauthorizedAccessException("Windows declined the radio change.");return radio.State.ToString();
    }
}
