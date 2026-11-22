namespace Aster.Core;

public enum HardwareAction { VolumeDown, VolumeUp, VolumeMute, BrightnessDown, BrightnessUp }
public static class HardwareKeyPolicy
{
    public static bool IsAssignableBrightnessKey(int key) => key is >=0x70 and <=0x87 or >=0xD8 and <=0xDA or 0xFF;
    public static HardwareAction? Resolve(int key, bool volume, bool brightness, int dim, int brighten, bool controlAlt)
    {
        if(volume) { if(key==0xAD)return HardwareAction.VolumeMute;if(key==0xAE)return HardwareAction.VolumeDown;if(key==0xAF)return HardwareAction.VolumeUp; }
        if(brightness && !controlAlt)
        {if(dim!=0&&key==dim)return HardwareAction.BrightnessDown;if(brighten!=0&&key==brighten)return HardwareAction.BrightnessUp;}
        if(controlAlt && key==0x28)return HardwareAction.BrightnessDown;
        if(controlAlt && key==0x26)return HardwareAction.BrightnessUp;
        return null;
    }
}
public static class ForegroundPolicy
{
    public static bool ShouldStow(bool enabled,bool ownWindow,bool shellWindow,bool maximized,bool fullscreen,bool anyApplicationVisible=true) => anyApplicationVisible&&enabled&&!ownWindow&&!shellWindow&&(maximized||fullscreen);
}
