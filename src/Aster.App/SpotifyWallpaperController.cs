using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aster.Core;
using Aster.Platform.Windows;
namespace Aster.App;

internal sealed record WallpaperJournal(WallpaperSnapshot Original);
/// <summary>Latest-cover wins, bounded disk cache, ownership-aware restoration and crash recovery.</summary>
internal sealed class SpotifyWallpaperController:IDisposable
{
    private readonly AppModel model;private readonly IWallpaperBackend backend;private readonly Dispatcher dispatcher;
    private readonly SemaphoreSlim gate=new(1);private readonly DispatcherTimer debounce;
    private readonly string[] cache;private WallpaperJournal? journal;private CancellationTokenSource? render;
    private bool? wasEnabled;private volatile bool disposed;private bool suspended,recovery=true;private string lastKey="";private int slot;
    public SpotifyWallpaperController(AppModel model,IWallpaperBackend? backend=null)
    {
        this.model=model;this.backend=backend??new DesktopWallpaperService();dispatcher=Dispatcher.CurrentDispatcher;
        cache=new[]{Path.Combine(model.Store.Root,"wallpaper-a.png"),Path.Combine(model.Store.Root,"wallpaper-b.png")};
        debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(450)};debounce.Tick+=(_,_)=>{debounce.Stop();Refresh();};
        model.PropertyChanged+=Changed;model.AppearanceChanged+=PreferencesChanged;model.BeforeDeleteData+=DeleteData;
        journal=model.Store.Load<WallpaperJournal?>("wallpaper-restore.json",null);
        // Recovery is serialized with rendering; no timer/polling and no artwork persists beyond this bounded cache.
        PreferencesChanged();
    }
    private void Changed(object? sender,PropertyChangedEventArgs args)
    {if(args.PropertyName is nameof(AppModel.MediaArtwork) or nameof(AppModel.MediaPlaying) or nameof(AppModel.MediaSourceAppId))Schedule();}
    private void PreferencesChanged(){var enabled=model.Preferences.SpotifyWallpaper;if(wasEnabled==enabled)return;wasEnabled=enabled;suspended=false;lastKey="";Schedule();}
    private void Schedule(){if(disposed)return;render?.Cancel();debounce.Stop();debounce.Start();}
    private void Status(string text)=>dispatcher.BeginInvoke(()=>{if(!disposed)model.SetWallpaperStatus(text);});
    private async void Refresh()
    {
        if(disposed)return;render?.Cancel();var cancel=new CancellationTokenSource();render=cancel;
        var enabled=model.Preferences.SpotifyWallpaper;var spotify=SpotifyVisualPolicy.IsSpotify(model.MediaSourceAppId);
        var art=model.MediaArtwork as BitmapSource;var playing=model.MediaPlaying;
        try
        {
            await gate.WaitAsync(cancel.Token).ConfigureAwait(false);
            try
            {
                if(disposed)return;
                await Task.Run(()=>
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    if(recovery){RestoreLocked();recovery=false;}
                    if(!enabled||!spotify||art is null)
                    {
                        RestoreLocked();Status(!enabled?"Spotify wallpaper off":!spotify?"Waiting for Spotify · start a song in its app":"This track has no shared cover art");return;
                    }
                    if(suspended){Status("Wallpaper changed outside Aster · toggle atmosphere to resume");return;}
                    if(!playing){Status(journal is null?"Waiting for Spotify playback…":"Paused · keeping this atmosphere");return;}
                    if(journal is not null&&!backend.Owns(cache)){suspended=true;RestoreLocked();Status("Wallpaper changed outside Aster · toggle atmosphere to resume");return;}
                    var converted=new FormatConvertedBitmap(art,PixelFormats.Bgra32,null,0);converted.Freeze();
                    var source=new byte[converted.PixelWidth*converted.PixelHeight*4];converted.CopyPixels(source,converted.PixelWidth*4,0);
                    var key=Convert.ToHexString(SHA256.HashData(source));if(key==lastKey){Status("Spotify atmosphere · following your music");return;}
                    var pixels=AtmosphereRenderer.Render(source,converted.PixelWidth,converted.PixelHeight,1600,1000,cancel.Token);
                    cancel.Token.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(model.Store.Root);
                    if((File.GetAttributes(model.Store.Root)&FileAttributes.ReparsePoint)!=0)throw new IOException();
                    var path=cache[slot++%cache.Length];var temp=path+".tmp";
                    var bitmap=BitmapSource.Create(1600,1000,96,96,PixelFormats.Bgra32,null,pixels,1600*4);bitmap.Freeze();
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    if(File.Exists(temp))File.Delete(temp);
                    using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))encoder.Save(file);
                    File.Move(temp,path,true);cancel.Token.ThrowIfCancellationRequested();
                    journal??=new(backend.Capture());model.Store.Save("wallpaper-restore.json",journal);
                    backend.Apply(path);lastKey=key;Status("Spotify atmosphere · following your music");
                },cancel.Token).ConfigureAwait(false);
            }
            finally{gate.Release();}
        }
        catch(OperationCanceledException){}
        catch{Status("Wallpaper unavailable · your restoration data is retained; toggle off to retry");}
        finally{if(!dispatcher.HasShutdownStarted)_ = dispatcher.BeginInvoke(()=>{if(ReferenceEquals(render,cancel))render=null;cancel.Dispose();});else cancel.Dispose();}
    }
    private void RestoreLocked()
    {
        if(journal is not null&&!backend.Restore(journal.Original,cache))throw new IOException("Wallpaper restoration failed.");
        journal=null;lastKey="";
        if(!Directory.Exists(model.Store.Root))return;
        foreach(var name in new[]{"wallpaper-restore.json","wallpaper-restore.json.bak","wallpaper-restore.json.tmp","wallpaper-a.png","wallpaper-b.png","wallpaper-a.png.tmp","wallpaper-b.png.tmp"})
            File.Delete(Path.Combine(model.Store.Root,name));
    }
    private void DeleteData()=>StopAndRestore();
    private void StopAndRestore()
    {
        if(disposed)return;debounce.Stop();render?.Cancel();
        if(!gate.Wait(TimeSpan.FromSeconds(8)))throw new TimeoutException();
        try{RestoreLocked();disposed=true;model.PropertyChanged-=Changed;model.AppearanceChanged-=PreferencesChanged;model.BeforeDeleteData-=DeleteData;}
        finally{gate.Release();}
    }
    public void Dispose(){try{StopAndRestore();}catch{/* Restoration journal survives for a later retry. */}}
}
