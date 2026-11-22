using System.Runtime.InteropServices;

namespace Aster.Platform.Windows;

public sealed record PowerReading(int? Percent, bool PluggedIn, bool BatteryPresent);
public static class PowerService
{
    public static PowerReading Read()
    {
        if (!GetSystemPowerStatus(out var status)) throw new System.ComponentModel.Win32Exception();
        return new(status.Percent == 255 ? null : status.Percent, status.Ac == 1, status.Flags != 128);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus { public byte Ac, Flags, Percent, Reserved; public uint LifeSeconds, FullLifeSeconds; }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
