using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aster.Core;

namespace Aster.App;

/// <summary>Exercises the actual retained visual tree and records its own pixels only.</summary>
internal static class IslandVisualTests
{
    public static async Task RunAsync(OverlayWindow island,AppModel model,string directory)
    {
        Directory.CreateDirectory(directory);Directory.CreateDirectory(Path.Combine(directory,"transition-frames"));
        var checks=new List<object>();
        async Task State(string name,Action action,int delay=850)
        {
            action();await Task.Delay(delay);Capture(island,Path.Combine(directory,name+".png"));
            checks.Add(new{name,target=island.Machine.Target.ToString(),current=island.Machine.Current.ToString(),width=island.SurfaceBounds.Width,height=island.SurfaceBounds.Height});
            if(island.Machine.Current!=island.Machine.Target)throw new InvalidOperationException("Motion did not settle: "+name);
        }
        // Original local test art exercises the WinRT thumbnail stream and actual retained UI.
        var fixture=new DrawingVisual();using(var dc=fixture.RenderOpen())
        {dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(32,112,224)),null,new Rect(0,0,32,64));dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(240,144,64)),null,new Rect(32,0,32,64));}
        var source=new RenderTargetBitmap(64,64,96,96,PixelFormats.Pbgra32);source.Render(fixture);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(source));using var memory=new MemoryStream();png.Save(memory);var bytes=memory.ToArray();
        using var stream=new Windows.Storage.Streams.InMemoryRandomAccessStream();
        using(var writer=new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(bytes);await writer.StoreAsync();writer.DetachStream();}
        var received=await Aster.Platform.Windows.MediaService.ReadArtworkAsync(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(stream));
        if(received is null||!received.SequenceEqual(bytes))throw new InvalidOperationException("Artwork stream bytes did not roundtrip.");
        if(ArtworkDecoder.Decode(new byte[]{1,2,3}) is not null)throw new InvalidOperationException("Malformed artwork was accepted.");
        if(await Aster.Platform.Windows.MediaService.ReadArtworkAsync(null) is not null)throw new InvalidOperationException("Null artwork was accepted.");
        using var oversized=new Windows.Storage.Streams.InMemoryRandomAccessStream();oversized.Size=4*1024*1024+1;
        if(await Aster.Platform.Windows.MediaService.ReadArtworkAsync(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(oversized)) is not null)throw new InvalidOperationException("Oversized artwork was accepted.");
        model.UpdateMedia(new("Artwork integration fixture","Original local test image",true,true,received));await Task.Delay(200);
        await State("20-artwork-compact",()=>island.Machine.Collapse());
        if(model.MediaArtwork is not BitmapSource decoded||!decoded.IsFrozen||decoded.PixelWidth>256||decoded.PixelHeight>256)throw new InvalidOperationException("Artwork decode bounds/freeze failed.");
        AssertArtworkPixels(Path.Combine(directory,"20-artwork-compact.png"));
        await State("21-artwork-expanded",()=>island.Machine.Expand(IslandPage.Media));
        AssertArtworkPixels(Path.Combine(directory,"21-artwork-expanded.png"));
        // A new track with no/invalid artwork must clear the previous track's cover.
        model.UpdateMedia(new("Track without art","",true,true,new byte[]{1,2,3}));await Task.Delay(200);
        if(model.MediaArtwork is not null)throw new InvalidOperationException("Previous cover survived a track without artwork.");
        await State("22-artwork-cleared",()=>island.Machine.Collapse());
        model.UpdateMedia(new("No media playing","Open a player to get started",false,false));await Task.Delay(200);
        await File.WriteAllTextAsync(Path.Combine(directory,"artwork-report.json"),JsonSerializer.Serialize(new{streamRoundtrip=true,compactPixels=true,expandedPixels=true,invalidFallback=true,nullFallback=true,oversizeRejected=true,staleCoverCleared=true,decodeBound=256,networkRequests=0,scope="Local WinRT stream fixture and real WPF rendering; live YouTube/browser thumbnail availability depends on the player."}));
        var hit=(System.Windows.FrameworkElement)island.FindName("HitPad");
        void HoverInput()=>hit.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=System.Windows.Input.Mouse.MouseEnterEvent});
        island.Machine.Collapse();HoverInput();
        if(island.Machine.Target!=IslandState.Expanded)throw new InvalidOperationException("Compact hover did not open the island.");
        await State("23-hover-open",()=>{});
        island.Machine.ShowSystemHud(SystemHudKind.Volume,42);HoverInput();
        if(island.Machine.Target!=IslandState.SystemHud)throw new InvalidOperationException("Hover interrupted the volume HUD.");
        island.Machine.Collapse();
        await State("01-idle",()=>island.Machine.Collapse());
        await State("02-hover",()=>island.Machine.Hover(true));
        island.Machine.Expand();await Task.Delay(90);Capture(island,Path.Combine(directory,"03-transition.png"));
        await State("04-expanded-music",()=>island.Machine.Expand(IslandPage.Media));
        await State("05-expanded-focus",()=>island.Machine.Select(IslandPage.Focus));
        await State("06-persistent-focus",()=>{model.ToggleFocusCommand.Execute(null);island.Machine.Collapse();});
        await State("07-notification",()=>island.Machine.Notify("Focus is flowing.","Your timer keeps running while you work.",TimeSpan.FromSeconds(2)));
        await Task.Delay(1500);
        await State("08-files",()=>island.Machine.Expand(IslandPage.Shelf));
        model.Note="A little space for a clear thought.\n\nSaved here. Only here.";
        await State("09-note",()=>island.Machine.Select(IslandPage.Notes));
        await State("10-sound",()=>island.Machine.Select(IslandPage.Controls));
        await State("11-simultaneous",()=>{island.Machine.SetActivity(new(ActivityKind.Media,10,"Media arbitration test"),ActivityKind.Media);island.Machine.Collapse();});
        await State("12-hidden",()=>island.Machine.SetHidden(true));
        await State("13-restored",()=>island.Machine.SetHidden(false));
        await State("15-today",()=>island.Machine.Select(IslandPage.Today));
        await State("16-stowed",()=>{island.Machine.SetStowed(true);island.Machine.Collapse();});
        await State("17-volume-hud",()=>island.Machine.ShowSystemHud(SystemHudKind.Volume,42));
        await State("18-brightness-hud",()=>island.Machine.ShowSystemHud(SystemHudKind.Brightness,65));
        await Task.Delay(1000);if(island.Machine.Target!=IslandState.Stowed)throw new InvalidOperationException("HUD did not return to the stowed strip.");
        await State("19-hud-return",()=>island.Machine.Collapse());island.Machine.SetStowed(false);
        // Interrupted retargets and retained content, driven by the same public state API as input.
        for(var i=0;i<12;i++)
        {
            island.Machine.Expand((IslandPage)(i%5));await Task.Delay(90);island.Machine.Collapse();await Task.Delay(90);
        }
        await State("14-after-interruptions",()=>island.Machine.Expand(IslandPage.Focus));
        // Capture real rendered frames for a transition contact sheet/animation artifact.
        island.Machine.Collapse();await Task.Delay(800);island.Machine.Expand(IslandPage.Focus);
        for(var i=0;i<32;i++){await Task.Delay(17);Capture(island,Path.Combine(directory,"transition-frames",$"frame-{i:000}.png"));}
        await Task.Delay(850);
        foreach(var factor in new[]{.85,1.0,1.2})
        {
            model.UpdatePreferences(model.Preferences with{IslandScale=factor});await Task.Delay(900);Capture(island,Path.Combine(directory,$"scale-{factor:0.00}.png"));
        }
        model.UpdatePreferences(model.Preferences with{IslandScale=1});
        model.ToggleFocusCommand.Execute(null);island.Machine.SetActivity(null,ActivityKind.Media);island.Machine.Collapse();await Task.Delay(900);
        var recordings=new List<object>();
        async Task Record(string name,Action action)
        {
            var folder=Path.Combine(directory,"recordings",name);Directory.CreateDirectory(folder);
            var watch=System.Diagnostics.Stopwatch.StartNew();var times=new List<double>();action();
            for(var i=0;i<36;i++){await Task.Delay(12);Capture(island,Path.Combine(folder,$"frame-{i:000}.png"));times.Add(watch.Elapsed.TotalMilliseconds);}
            recordings.Add(new{name,frameTimesMs=times});await Task.Delay(200);
        }
        await Record("idle-to-hover",()=>island.Machine.Hover(true));
        await Record("hover-to-expanded",()=>island.Machine.Expand(IslandPage.Focus));
        await Record("expanded-to-notification",()=>island.Machine.Notify("A moment of clarity.","Your work stays on your device.",TimeSpan.FromSeconds(15)));
        await Record("notification-to-expanded",()=>island.Machine.Expand(IslandPage.Notes));
        await Record("expanded-to-persistent",()=>{model.ToggleFocusCommand.Execute(null);island.Machine.Collapse();});
        await Record("persistent-to-hidden",()=>island.Machine.SetHidden(true));
        await Record("hidden-to-persistent",()=>island.Machine.SetHidden(false));
        await Record("persistent-to-stowed",()=>island.Machine.SetStowed(true));
        await Record("stowed-to-system-hud",()=>island.Machine.ShowSystemHud(SystemHudKind.Volume,42));
        await Record("hud-to-stowed",()=>island.Machine.Collapse());
        island.Machine.SetStowed(false);
        await File.WriteAllTextAsync(Path.Combine(directory,"recordings.json"),JsonSerializer.Serialize(recordings,new JsonSerializerOptions{WriteIndented=true}));
        await File.WriteAllTextAsync(Path.Combine(directory,"capture-frame-timings.json"),JsonSerializer.Serialize(island.FrameSamples));
        model.UpdatePreferences(model.Preferences with{IslandScale=1});await Task.Delay(900);
        island.ClearFrameSamples();
        // Separate timing pass: no PNG encoding, disk writes or screenshots in this interval.
        for(var i=0;i<12;i++){island.Machine.Expand((IslandPage)(i%5));await Task.Delay(440);island.Machine.Collapse();await Task.Delay(440);}
        await Task.Delay(900);
        var frames=island.FrameSamples.ToArray();var intervals=frames.Where(f=>f.IntervalMs>0).Select(f=>f.IntervalMs).OrderBy(v=>v).ToArray();
        double Percentile(double[] sorted,double fraction)=>sorted.Length==0?0:sorted[(int)Math.Clamp(Math.Ceiling(sorted.Length*fraction)-1,0,sorted.Length-1)];
        var report=new
        {
            architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),renderingTier=island.RenderingTier,material="Vector glass; separate native taskbar backdrop option",
            measurement="Separate WPF composition callback pass without screenshots/PNG capture; not GPU presentation FPS.",
            frameCount=frames.Length,medianIntervalMs=Percentile(intervals,.5),p95IntervalMs=Percentile(intervals,.95),p99IntervalMs=Percentile(intervals,.99),
            meanUpdateMs=frames.Length==0?0:frames.Average(f=>f.UpdateMs),maxUpdateMs=frames.Length==0?0:frames.Max(f=>f.UpdateMs),
            intervalsOver20Ms=intervals.Count(v=>v>20),checks,
            unverified=new[]{"Physical touchscreen and precision-trackpad gestures","Real display resolution/refresh changes and multi-monitor hardware","GPU present-time trace","Live media playback controls with an active player","Background desktop blur/refraction is not implemented"}
        };
        await File.WriteAllTextAsync(Path.Combine(directory,"visual-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        await File.WriteAllTextAsync(Path.Combine(directory,"frame-timings.json"),JsonSerializer.Serialize(frames));
    }
    private static void AssertArtworkPixels(string path)
    {
        using var file=File.OpenRead(path);var frame=BitmapFrame.Create(file,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
        var bitmap=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
        var blue=0;var orange=0;for(var i=0;i<pixels.Length;i+=4){if(Math.Abs(pixels[i]-224)<6&&Math.Abs(pixels[i+1]-112)<6&&Math.Abs(pixels[i+2]-32)<6)blue++;if(Math.Abs(pixels[i]-64)<6&&Math.Abs(pixels[i+1]-144)<6&&Math.Abs(pixels[i+2]-240)<6)orange++;}
        if(blue<20||orange<20)throw new InvalidOperationException("Cover artwork was not rendered in "+Path.GetFileName(path));
    }
    private static void Capture(OverlayWindow island,string path)
    {
        island.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(island.ActualWidth),(int)Math.Ceiling(island.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(island);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
}
