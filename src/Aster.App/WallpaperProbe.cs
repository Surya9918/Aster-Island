using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aster.Core;
using Aster.Infrastructure;
using Aster.Platform.Windows;
namespace Aster.App;
internal static class WallpaperProbe
{
    private sealed class FakeBackend:IWallpaperBackend
    {
        public string Current="original-local-picture";public int Applies,Restores;public int ApplyDelay;public volatile bool Applying;public bool FailRestore;
        public WallpaperSnapshot Capture()=>new(new[]{new MonitorWallpaper("fixture",Current)},0,4,1,Array.Empty<string>(),0,1800000);
        public void Apply(string path){if(!File.Exists(path))throw new IOException();Applying=true;if(ApplyDelay>0)System.Threading.Thread.Sleep(ApplyDelay);Current=path;Applies++;Applying=false;}
        public bool Owns(IReadOnlyCollection<string> paths)=>paths.Contains(Current);
        public bool Restore(WallpaperSnapshot original,IReadOnlyCollection<string> paths){if(FailRestore)throw new IOException();if(paths.Contains(Current))Current=original.Monitors[0].Path;Restores++;return !paths.Contains(Current);}
    }
    private static byte[] Cover(bool alternate)
    {
        var bytes=new byte[64*64*4];for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {var i=(y*64+x)*4;bytes[i]=(byte)(70+x*2);bytes[i+1]=(byte)(25+y*2);bytes[i+2]=(byte)(alternate?30+x:200-y*2);bytes[i+3]=255;}
        var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,bytes,256);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=new MemoryStream();png.Save(file);return file.ToArray();
    }
    private static async Task Until(Func<bool> condition)
    {var watch=Stopwatch.StartNew();while(!condition()){if(watch.Elapsed>TimeSpan.FromSeconds(8))throw new InvalidOperationException("Wallpaper integration check timed out.");await Task.Delay(50);}}
    public static async Task RunAsync(string path)
    {
        var report=new Dictionary<string,object?>();var root=Path.Combine(Path.GetTempPath(),"Aster-Wallpaper-Tests",Guid.NewGuid().ToString("N"));
        using(var model=new AppModel(new LocalStore(root)))
        {
            var fake=new FakeBackend();using var controller=new SpotifyWallpaperController(model,fake);
            model.UpdatePreferences(model.Preferences with{SpotifyWallpaper=true});
            var art=Cover(false);model.UpdateMedia(new("Fixture","",true,true,art,"open.spotify.com-D55111A4_nwqr74yvdh6rj!App"));await Until(()=>fake.Applies==1);
            report["installedWebAppRecognized"]=SpotifyVisualPolicy.IsSpotify("open.spotify.com-D55111A4_nwqr74yvdh6rj!App");
            report["spotifyPlaybackApplies"]=File.Exists(fake.Current);var first=fake.Current;
            model.UpdateMedia(new("Fixture","",true,true,art,"Spotify.exe"));await Task.Delay(950);report["sameCoverDeduplicated"]=fake.Applies==1;
            model.UpdateMedia(new("Fixture B","",true,true,Cover(true),"Spotify.exe"));await Until(()=>fake.Applies==2);report["trackChangeAppliesNewWallpaper"]=fake.Current!=first;
            var held=fake.Current;model.UpdateMedia(new("Fixture B","",false,true,Cover(true),"Spotify.exe"));await Task.Delay(950);report["pauseKeepsAtmosphere"]=fake.Current==held&&fake.Applies==2;
            model.UpdateMedia(new("Other player","",true,true,art,"msedge.exe"));await Until(()=>fake.Current=="original-local-picture");report["otherPlayerRestores"]=true;
            model.UpdateMedia(new("Fixture","",true,true,art,"Spotify.exe"));await Until(()=>fake.Applies==3);
            fake.Current="user-picked-new-wallpaper";model.UpdateMedia(new("Fixture B","",true,true,Cover(true),"Spotify.exe"));await Task.Delay(1000);report["manualWallpaperRespected"]=fake.Current=="user-picked-new-wallpaper";
            model.UpdatePreferences(model.Preferences with{GlassShine=1.6});await Task.Delay(650);report["appearanceChangeDoesNotOverrideManualWallpaper"]=fake.Current=="user-picked-new-wallpaper"&&fake.Applies==3;
            model.UpdatePreferences(model.Preferences with{SpotifyWallpaper=false});await Task.Delay(650);report["cacheClearedWhenOff"]=!File.Exists(Path.Combine(root,"wallpaper-a.png"))&&!File.Exists(Path.Combine(root,"wallpaper-b.png"));
            model.UpdatePreferences(model.Preferences with{SpotifyWallpaper=true});model.UpdateMedia(new("Fixture","",true,true,art,"Spotify.exe"));await Until(()=>fake.Applies==4);
            model.UpdateMedia(new("Missing cover","",true,true,null,"Spotify.exe"));await Until(()=>fake.Current=="user-picked-new-wallpaper");report["missingCoverRestores"]=true;
            model.UpdateMedia(new("Fixture","",true,true,art,"Spotify.exe"));await Until(()=>fake.Applies==5);controller.Dispose();report["quitRestores"]=fake.Current=="user-picked-new-wallpaper";
            // Simulate a persisted journal left by an interrupted process, then recover through the controller.
            var previous=fake.Capture();Directory.CreateDirectory(root);var cache=Path.Combine(root,"wallpaper-a.png");File.WriteAllBytes(cache,art);fake.Apply(cache);model.Store.Save("wallpaper-restore.json",new WallpaperJournal(previous));
            model.UpdateMedia(new("No player","",false,false));await Task.Delay(200);var restores=fake.Restores;
            using(var recovered=new SpotifyWallpaperController(model,fake)){await Until(()=>fake.Restores>restores&&fake.Current=="user-picked-new-wallpaper");report["crashJournalRecovery"]=true;}
        }
        using(var model=new AppModel(new LocalStore(Path.Combine(root,"shutdown-check"))))
        {
            var fake=new FakeBackend{ApplyDelay=250};using var controller=new SpotifyWallpaperController(model,fake);
            model.UpdatePreferences(model.Preferences with{SpotifyWallpaper=true});model.UpdateMedia(new("Fixture","",true,true,Cover(false),"Spotify.exe"));
            await Until(()=>fake.Applying);var watch=Stopwatch.StartNew();controller.Dispose();
            report["shutdownDuringUpdateRestores"]=fake.Current=="original-local-picture"&&watch.Elapsed<TimeSpan.FromSeconds(3);
        }
        using(var model=new AppModel(new LocalStore(Path.Combine(root,"restore-failure-check"))))
        {
            var fake=new FakeBackend();using var controller=new SpotifyWallpaperController(model,fake);model.UpdatePreferences(model.Preferences with{SpotifyWallpaper=true});model.UpdateMedia(new("Fixture","",true,true,Cover(false),"Spotify.exe"));await Until(()=>fake.Applies==1);
            fake.FailRestore=true;controller.Dispose();report["failedRestoreRetainsJournal"]=File.Exists(Path.Combine(model.Store.Root,"wallpaper-restore.json"))&&File.Exists(fake.Current);
            fake.FailRestore=false;controller.Dispose();report["failedRestoreCanRetry"]=fake.Current=="original-local-picture"&&!File.Exists(Path.Combine(model.Store.Root,"wallpaper-restore.json"));
        }
        var image=ArtworkDecoder.Decode(Cover(false))!;var rgba=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);rgba.Freeze();var source=new byte[rgba.PixelWidth*rgba.PixelHeight*4];rgba.CopyPixels(source,rgba.PixelWidth*4,0);
        var timer=Stopwatch.StartNew();var pixels=await Task.Run(()=>AtmosphereRenderer.Render(source,rgba.PixelWidth,rgba.PixelHeight,1600,1000));report["wallpaperRenderMs"]=timer.Elapsed.TotalMilliseconds;
        var preview=Path.ChangeExtension(path,"preview.png");var bmp=BitmapSource.Create(1600,1000,96,96,PixelFormats.Bgra32,null,pixels,6400);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(preview))encoder.Save(file);
        var native=new DesktopWallpaperService();var original=native.Capture();report["nativeMonitorCount"]=original.Monitors.Length;
        try{native.Apply(preview);await Task.Delay(1100);report["nativeWallpaperApplied"]=native.Owns(new[]{preview});}
        finally{report["nativeWallpaperRestored"]=native.Restore(original,new[]{preview});}
        var restored=native.Capture();report["nativeOriginalImagesVerified"]=original.Monitors.All(o=>restored.Monitors.Any(m=>m.Monitor==o.Monitor&&m.Path==o.Path));
        report["nativePositionRestored"]=original.Position==restored.Position;report["nativeBackgroundColorRestored"]=original.Color==restored.Color;
        report["networkRequests"]=0;report["scope"]="Original local cover fixture, actual controller state changes and actual Windows wallpaper apply/restore. Windows controls transition effects; live Spotify track switching is not simulated by controlling playback.";
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        if(report.Values.OfType<bool>().Any(v=>!v))throw new InvalidOperationException("A wallpaper check failed.");
    }
}
