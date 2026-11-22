using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Aster.Platform.Windows;

namespace Aster.App;

internal static class PlatformProbe
{
    public static async Task RunAsync(string reportPath)
    {
        var results = new Dictionary<string, object?> { ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(), ["timeUtc"] = DateTimeOffset.UtcNow };
        try { var power = PowerService.Read(); results["batteryApi"] = "passed"; results["batteryPresent"] = power.BatteryPresent; } catch { results["batteryApi"] = "unavailable"; }
        try { _ = AudioService.Read(); results["audioRead"] = "passed"; } catch { results["audioRead"] = "unavailable"; }
        try { results["brightnessRead"] = await Task.Run(BrightnessService.Read) is not null ? "passed" : "provider-unavailable"; } catch { results["brightnessRead"] = "provider-unavailable"; }
        using var media = new MediaService();
        media.Changed+=reading=>{results["spotifyCurrentSession"]=Aster.Core.SpotifyVisualPolicy.IsSpotify(reading.SourceAppId);results["sharedArtworkAvailable"]=reading.Artwork is {Length:>0};};
        try { await media.StartAsync(); results["mediaSessionApi"] = "passed"; } catch { results["mediaSessionApi"] = "unavailable"; }
        try
        {
            var sessions=await Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            results["mediaSources"]=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(sessions.GetSessions(),s=>new{source=s.SourceAppUserModelId,playback=s.GetPlaybackInfo().PlaybackStatus.ToString(),current=s==sessions.GetCurrentSession()}));
        }
        catch{results["mediaSources"]="unavailable";}
        // Only application IDs/playback flags are recorded; never track titles, artists, tabs or device IDs.
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
