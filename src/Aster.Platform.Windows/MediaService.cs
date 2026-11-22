using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Aster.Platform.Windows;

public sealed record MediaReading(string Title, string Artist, bool Playing, bool Available, byte[]? Artwork=null,string SourceAppId="");
public sealed class MediaService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private readonly SemaphoreSlim refresh = new(1, 1);
    private bool disposed;
    private long requested;
    public event Action<MediaReading>? Changed;
    public event Action? Failed;
    public async Task StartAsync()
    {
        var value = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(8));
        if (disposed) return;
        manager = value; manager.CurrentSessionChanged += SessionChanged;
        Attach(); await RefreshAsync();
    }
    private void SessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    { Attach(); _ = RefreshAsync(); }
    private void Attach()
    {
        if (disposed) return;
        if (session is not null) { session.MediaPropertiesChanged -= PropertiesChanged; session.PlaybackInfoChanged -= PlaybackChanged; }
        session = manager?.GetCurrentSession();
        if (session is not null) { session.MediaPropertiesChanged += PropertiesChanged; session.PlaybackInfoChanged += PlaybackChanged; }
    }
    private void PropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => _ = RefreshAsync();
    private void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => _ = RefreshAsync();
    public async Task RefreshAsync()
    {
        Interlocked.Increment(ref requested);
        if(!await refresh.WaitAsync(0))return;
        var observed=Volatile.Read(ref requested);
        try
        {
            if (disposed) return;
            var current = session;
            if (current is null) { Changed?.Invoke(new("No media playing", "Open a player to get started", false, false)); return; }
            var props = await current.TryGetMediaPropertiesAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (disposed || session != current) return;
            var artwork=await ReadArtworkAsync(props.Thumbnail);
            if(disposed||session!=current)return;
            Changed?.Invoke(new(props.Title ?? "Untitled", props.Artist ?? "", current.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, true, artwork,current.SourceAppUserModelId??""));
        }
        catch { if (!disposed) Failed?.Invoke(); }
        finally { refresh.Release();if(!disposed&&Volatile.Read(ref requested)!=observed)_=RefreshAsync(); }
    }
    // Consume the thumbnail stream already supplied by Windows; never resolve a web URL.
    public static async Task<byte[]?> ReadArtworkAsync(IRandomAccessStreamReference? thumbnail)
    {
        if(thumbnail is null)return null;
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var stream=await thumbnail.OpenReadAsync().AsTask(timeout.Token);
            const uint limit=4*1024*1024;
            if(stream.Size==0||stream.Size>limit)return null;
            using var reader=new DataReader(stream.GetInputStreamAt(0));
            var count=(uint)stream.Size;var loaded=await reader.LoadAsync(count).AsTask(timeout.Token);
            if(loaded!=count)return null;
            var bytes=new byte[count];reader.ReadBytes(bytes);return bytes;
        }
        catch{return null;} // Missing/invalid artwork must not break media controls.
    }
    public async Task<bool> ControlAsync(string command)
    {
        var current = session;
        if (current is null) return false;
        return command switch
        {
            "previous" => await current.TrySkipPreviousAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)),
            "next" => await current.TrySkipNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)),
            "toggle" => await current.TryTogglePlayPauseAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)),
            _ => false
        };
    }
    public void Dispose()
    {
        disposed = true;
        if (manager is not null) manager.CurrentSessionChanged -= SessionChanged;
        if (session is not null) { session.MediaPropertiesChanged -= PropertiesChanged; session.PlaybackInfoChanged -= PlaybackChanged; }
        session = null; manager = null;
    }
}
