using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Aster.Core;
using Aster.Infrastructure;
using Aster.Platform.Windows;
using Microsoft.Win32;

namespace Aster.App;

public sealed record ShelfRow(string Path, string Name, string State);
public sealed record CalendarCell(string Letter,string Day,bool Today);
public sealed partial class AppModel : INotifyPropertyChanged, IDisposable
{
    public LocalStore Store { get; }
    public Diagnostics Log { get; }
    public Preferences Preferences { get; private set; }
    private WorkspaceData data;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer clockTimer, saveTimer;
    private readonly FocusTimer focus = new(new SystemClock());
    public ClipboardHistory ClipboardHistory { get; } = new(new SystemClock());
    public NetworkConsent Consent { get; } = new();
    public NetworkAudit Audit { get; } = new(new SystemClock());
    private readonly WeatherClient weather;
    private readonly MediaService media = new();
    private bool closing;
    private int ticks;private string lastClock="",lastDate="";
    private string status = "Ready. Local features work offline.";
    public string Status { get => status; private set { status = value; Changed(); } }
    public string ClockText => ClockDisplay.Format(DateTime.Now,Preferences.Use12HourClock);
    public string DateText => DateTime.Now.ToString("dddd, d MMMM", CultureInfo.CurrentCulture);
    public string DayNumber=>DateTime.Now.Day.ToString(CultureInfo.CurrentCulture);
    public string DayAndMonth=>DateTime.Now.ToString("ddd\nMMM",CultureInfo.CurrentCulture).ToUpperInvariant();
    public string WeekNumber=>"Week "+ISOWeek.GetWeekOfYear(DateTime.Today);
    public string Weekday=>DateTime.Now.ToString("dddd",CultureInfo.CurrentCulture).ToUpperInvariant();
    public string FullDate=>DateTime.Now.ToString("d MMMM yyyy",CultureInfo.CurrentCulture);
    public IReadOnlyList<CalendarCell> WeekCells=>Enumerable.Range(0,7).Select(i=>DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek+6)%7)+i)).Select(d=>new CalendarCell(d.ToString("ddd",CultureInfo.CurrentCulture)[..1],d.Day.ToString(CultureInfo.CurrentCulture),d==DateTime.Today)).ToArray();
    public string Architecture => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
    private string battery = "Reading battery…";
    public string Battery { get => battery; private set { battery = value; Changed(); } }
    private MediaReading mediaReading = new("No media playing", "Open a player to get started", false, false);
    public System.Windows.Media.ImageSource? MediaArtwork {get;private set;}
    public System.Windows.Media.ImageSource? DisplayedMediaArtwork=>Preferences.ShowArtwork?MediaArtwork:null;
    public string FocusLengthLabel=>$"{Preferences.FocusMinutes} min";
    public string BreakLengthLabel=>$"{Preferences.BreakMinutes} min";
    private long mediaRevision;
    internal async void UpdateMedia(MediaReading reading)
    {
        if(closing)return;
        var revision=System.Threading.Interlocked.Increment(ref mediaRevision);
        var art=await Task.Run(()=>ArtworkDecoder.Decode(reading.Artwork));
        if(closing||dispatcher.HasShutdownStarted)return;
        await dispatcher.InvokeAsync(()=>
        {
            if(closing||revision!=System.Threading.Interlocked.Read(ref mediaRevision))return;
            mediaReading=reading with{Artwork=null};MediaArtwork=art;
            Changed(nameof(MediaSourceAppId));Changed(nameof(MediaArtwork));Changed(nameof(DisplayedMediaArtwork));Changed(nameof(MediaTitle));Changed(nameof(MediaArtist));
            Changed(nameof(PlayLabel));Changed(nameof(MediaAvailable));Changed(nameof(MediaPlaying));
        });
    }
    public string MediaSourceAppId=>mediaReading.SourceAppId;
    public string WallpaperStatus {get;private set;}="Spotify wallpaper off";
    public string WallpaperButtonLabel=>Preferences.SpotifyWallpaper?"Spotify atmosphere · on":"Spotify atmosphere · off";
    public void SetWallpaperStatus(string value){WallpaperStatus=value;Changed(nameof(WallpaperStatus));}
    public event Action? BeforeDeleteData;
    public ICommand ToggleWallpaperCommand {get;}
    public string MediaTitle => mediaReading.Title;
    public string MediaArtist => mediaReading.Artist;
    public string PlayLabel => mediaReading.Playing ? "Pause" : "Play";
    public bool MediaAvailable => mediaReading.Available;
    public bool MediaPlaying => mediaReading.Playing;
    public bool FocusRunning => focus.Running;
    private double focusDuration = 25 * 60;
    public double FocusProgress => Math.Clamp(1 - focus.Remaining.TotalSeconds / focusDuration, 0, 1);
    public int ShelfCount => data.Shelf.Count;
    public string TimerText => $"{(int)focus.Remaining.TotalMinutes:00}:{focus.Remaining.Seconds:00}";
    public string TimerButton => focus.Running ? "Pause focus" : "Start focus";
    public string FocusStatus { get; private set; } = "Make room for one thing.";
    public string Note { get => data.Note; set { data = data with { Note = (value ?? "")[..Math.Min((value ?? "").Length, 100_000)] }; Changed(); QueueSave(); } }
    public string TaskDraft { get; set; } = "";
    public IReadOnlyList<TaskItem> Tasks => data.Tasks.ToArray();
    public IReadOnlyList<LauncherItem> Launchers => data.Launchers.ToArray();
    public double CardWidth => Preferences.Layout == "Compact" ? 300 : 350;
    public IReadOnlyList<ShelfRow> Shelf => data.Shelf.Select(p => new ShelfRow(p, System.IO.Path.GetFileName(p), File.Exists(p) ? "Local file" : "Missing — remove or restore file")).ToArray();
    public IReadOnlyList<ClipboardEntry> Clips => ClipboardHistory.Items;
    public bool ClipboardEnabled { get => ClipboardHistory.Enabled; set { ClipboardHistory.SetEnabled(value); Changed(); Changed(nameof(Clips)); ClipboardPermissionChanged?.Invoke(value); } }
    public bool NetworkEnabled => Consent.Enabled;
    public string NetworkState => Consent.Enabled ? "Weather permitted for this session" : "Offline · network disabled";
    public IReadOnlyList<NetworkAuditEntry> NetworkEntries => Audit.Entries;
    public string Latitude { get; set; } = "";
    public string Longitude { get; set; } = "";
    private string weatherText = "No weather requested. Coordinates are never inferred.";
    public string WeatherText { get => weatherText; private set { weatherText = value; Changed(); } }
    private double volume;
    public double Volume { get => volume; private set { volume = value; Changed(); } }
    public bool AudioAvailable { get; private set; }
    public string AudioState { get; private set; } = "Reading audio device…";
    public string MicState { get; private set; } = "Microphone not queried";
    public int? Brightness { get; private set; }
    public string BrightnessState => Brightness is { } value ? $"Brightness {value}%" : "Brightness control unavailable on this display";
    public bool BrightnessAvailable=>Brightness.HasValue;
    private bool outputMuted;
    public string MuteTile=>outputMuted?"Muted\nTap to unmute":"Sound on\nTap to mute";
    public string WifiState {get;private set;}="Unavailable";
    public string BluetoothState {get;private set;}="Unavailable";
    public string FocusTile=>FocusRunning?"Focus timer\nRunning":"Focus timer\nPaused";
    public string TaskbarStatus {get;private set;}="Windows taskbar unchanged";
    public string TaskbarTile=>Preferences.TaskbarGlass?"Glass on":"Glass off";
    public bool TaskbarTintAvailable{get;private set;}
    public string HardwareStatus {get;private set;}="Device integration starting…";
    public event Action<SystemHudKind,double,bool>? SystemLevelChanged;
    public event Action<bool>? LearnBrightnessRequested;
    public ICommand ToggleWifiCommand {get;}
    public ICommand ToggleBluetoothCommand {get;}
    public ICommand TaskbarCommand {get;}
    public ICommand LearnDimCommand {get;}
    public ICommand LearnBrightCommand {get;}
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<bool>? ClipboardPermissionChanged;
    public event Action? AppearanceChanged;
    public event Action? RequestQuit;
    public event Action<string, string>? IslandNotice;
    public ICommand MediaCommand { get; }
    public ICommand ToggleFocusCommand { get; }
    public ICommand ResetFocusCommand { get; }
    public ICommand AddTaskCommand { get; }
    public ICommand ToggleTaskCommand { get; }
    public ICommand RemoveTaskCommand { get; }
    public ICommand RemoveShelfCommand { get; }
    public ICommand OpenShelfCommand { get; }
    public ICommand AddFilesCommand { get; }
    public ICommand AddLauncherCommand { get; }
    public ICommand LaunchCommand { get; }
    public ICommand RemoveLauncherCommand { get; }
    public ICommand ClearClipsCommand { get; }
    public ICommand CopyClipCommand { get; }
    public ICommand RemoveClipCommand { get; }
    public ICommand MuteCommand { get; }
    public ICommand MicMuteCommand { get; }
    public ICommand PermitWeatherCommand { get; }
    public ICommand RevokeWeatherCommand { get; }
    public ICommand FetchWeatherCommand { get; }
    public ICommand DeleteDataCommand { get; }
    public ICommand QuitCommand { get; }

    public AppModel(LocalStore store)
    {
        Store = store; Log = new(store.Root); dispatcher = Dispatcher.CurrentDispatcher;
        Preferences = store.Load("preferences.json", new Preferences()).Validated();
        data = store.Load("workspace.json", new WorkspaceData()).Validated();focus.Reset(Preferences.FocusMinutes);focusDuration=Preferences.FocusMinutes*60;
        weather = new(Consent, Audit);
        media.Changed += UpdateMedia;
        media.Failed += () => dispatcher.BeginInvoke(() => Error("MEDIA_UNAVAILABLE", "Media information is unavailable. Try another player."));
        Audit.Changed += () => dispatcher.BeginInvoke(() => { if (closing) return; Changed(nameof(NetworkEntries)); });
        clockTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick(), dispatcher);
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        InitializeNotifications();InitializeLive();
        ToggleWallpaperCommand=new ActionCommand(_=>UpdatePreferences(Preferences with{SpotifyWallpaper=!Preferences.SpotifyWallpaper}));
        MediaCommand = new AsyncCommand(async p => { if (!await media.ControlAsync(p as string ?? "toggle")) Status = "This player does not support that control."; }, () => Error("MEDIA_CONTROL_FAILED", "The media control was unavailable."));
        ToggleFocusCommand = new ActionCommand(_ => { if (focus.Running) focus.Pause(); else {if(focus.Remaining<=TimeSpan.Zero){focus.Reset(Preferences.FocusMinutes);focusDuration=Preferences.FocusMinutes*60;}focus.Start();} FocusStatus = focus.Running ? "Focus session in progress" : "Focus paused"; TimerChanged(); });
        ResetFocusCommand = new ActionCommand(p => { var minutes = p as string=="break"?Preferences.BreakMinutes:p as string=="focus"?Preferences.FocusMinutes:int.TryParse(p as string, out var n) ? Math.Clamp(n,1,180) : Preferences.FocusMinutes; focus.Reset(minutes); focusDuration = minutes * 60; FocusStatus = "Make room for one thing."; TimerChanged(); });
        AddTaskCommand = new ActionCommand(_ => { if (string.IsNullOrWhiteSpace(TaskDraft) || data.Tasks.Count >= 100) return; data.Tasks.Add(new(Guid.NewGuid(), TaskDraft.Trim()[..Math.Min(TaskDraft.Trim().Length, 500)], false)); TaskDraft = ""; Changed(nameof(TaskDraft)); Changed(nameof(Tasks)); QueueSave(); });
        ToggleTaskCommand = new ActionCommand(p => { if (p is Guid id) { data = data with { Tasks = data.Tasks.Select(t => t.Id == id ? t with { Done = !t.Done } : t).ToList() }; Changed(nameof(Tasks)); QueueSave(); } });
        RemoveTaskCommand = new ActionCommand(p => { if (p is Guid id) { data.Tasks.RemoveAll(t => t.Id == id); Changed(nameof(Tasks)); QueueSave(); } });
        RemoveShelfCommand = new ActionCommand(p => { if (p is string path) { data.Shelf.Remove(path); Changed(nameof(Shelf)); Changed(nameof(ShelfCount)); QueueSave(); Status = "Removed from island. Original file kept."; } });
        OpenShelfCommand = new ActionCommand(p => { if (p is string path) OpenFile(path); });
        AddFilesCommand = new ActionCommand(_ => { var dialog = new OpenFileDialog { Multiselect = true, Title = "Add local files to your shelf" }; if (dialog.ShowDialog() == true) AddFiles(dialog.FileNames); });
        AddLauncherCommand = new ActionCommand(_ => { var dialog = new OpenFileDialog { Filter = "Windows applications (*.exe)|*.exe", Title = "Choose an application" }; if (dialog.ShowDialog() == true && System.IO.Path.GetExtension(dialog.FileName).Equals(".exe", StringComparison.OrdinalIgnoreCase)) { if (data.Launchers.Count >= 30) return; data.Launchers.Add(new(System.IO.Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName)); Changed(nameof(Launchers)); QueueSave(); } });
        LaunchCommand = new ActionCommand(p => { if (p is LauncherItem item && System.IO.Path.GetExtension(item.Path).Equals(".exe", StringComparison.OrdinalIgnoreCase)) OpenFile(item.Path); });
        RemoveLauncherCommand = new ActionCommand(p => { if (p is LauncherItem item) { data.Launchers.Remove(item); Changed(nameof(Launchers)); QueueSave(); } });
        ClearClipsCommand = new ActionCommand(_ => { ClipboardHistory.Clear(); Changed(nameof(Clips)); });
        CopyClipCommand = new ActionCommand(p => { if (p is ClipboardEntry clip) { try { Clipboard.SetText(clip.Text); Status = "Copied to the Windows clipboard."; } catch { Error("CLIPBOARD_BUSY", "Clipboard is busy. Try again."); } } });
        RemoveClipCommand = new ActionCommand(p => { if (p is ClipboardEntry clip) { ClipboardHistory.Remove(clip.Id); Changed(nameof(Clips)); } });
        MuteCommand = new ActionCommand(_ => { try { AudioService.ToggleMute(); RefreshAudio(); } catch { Error("AUDIO_FAILED", "No accessible output device."); } });
        MicMuteCommand = new ActionCommand(_ => { try { AudioService.ToggleMute(true); MicState = AudioService.Read(true).Muted ? "Default communications microphone muted" : "Default communications microphone unmuted"; Changed(nameof(MicState)); } catch { Error("MIC_FAILED", "No accessible default communications microphone."); } });
        PermitWeatherCommand = new ActionCommand(_ => PermitWeather());
        RevokeWeatherCommand = new ActionCommand(_ => { Consent.Revoke(); NetworkChanged(); WeatherText = "Network permission revoked."; });
        FetchWeatherCommand = new AsyncCommand(_ => FetchWeatherAsync(), () => Error("WEATHER_FAILED", "Weather could not be retrieved. Check permission, coordinates and connection."));
        DeleteDataCommand = new ActionCommand(_ => DeleteData());
        QuitCommand = new ActionCommand(_ => RequestQuit?.Invoke());
        ToggleWifiCommand=new AsyncCommand(async _=>{WifiState=await RadioService.ToggleAsync(false);Changed(nameof(WifiState));},()=>Error("WIFI_DENIED","Windows did not allow Wi-Fi control. No radio setting was changed by Aster."));
        ToggleBluetoothCommand=new AsyncCommand(async _=>{BluetoothState=await RadioService.ToggleAsync(true);Changed(nameof(BluetoothState));},()=>Error("BLUETOOTH_DENIED","Windows did not allow Bluetooth control."));
        TaskbarCommand=new ActionCommand(_=>UpdatePreferences(Preferences with{TaskbarGlass=!Preferences.TaskbarGlass}));
        LearnDimCommand=new ActionCommand(_=>LearnBrightnessRequested?.Invoke(false));LearnBrightCommand=new ActionCommand(_=>LearnBrightnessRequested?.Invoke(true));
    }
    public async Task StartAsync()
    {
        await StartHeadphones();
        RefreshPower(); RefreshAudio();
        try { Brightness = await Task.Run(BrightnessService.Read); Changed(nameof(Brightness)); Changed(nameof(BrightnessState));Changed(nameof(BrightnessAvailable)); } catch { Log.Record("BRIGHTNESS_UNAVAILABLE"); }
        try{WifiState=await RadioService.ReadAsync(false);BluetoothState=await RadioService.ReadAsync(true);Changed(nameof(WifiState));Changed(nameof(BluetoothState));}catch{Log.Record("RADIOS_UNAVAILABLE");}
        try { await media.StartAsync(); } catch { Error("MEDIA_START_FAILED", "Media integration is unavailable. Other local features still work."); }
    }
    private void Tick()
    {
        if (closing) return;
        TickNotifications();TickHeadphones();
        var wasRunning = focus.Running;
        if (focus.CompleteIfDue()) { FocusStatus = "Focus complete. Take a breath."; Status = "Your focus session is complete."; PublishNotice("A little pause.", "Your focus session is complete. Take a breath."); }
        if (wasRunning) TimerChanged();
        var clockNow=ClockText;if(lastClock!=clockNow){lastClock=clockNow;Changed(nameof(ClockText));}
        var dateNow=DateText;if(lastDate!=dateNow){lastDate=dateNow;Changed(nameof(DateText));Changed(nameof(DayNumber));Changed(nameof(DayAndMonth));Changed(nameof(WeekNumber));Changed(nameof(Weekday));Changed(nameof(FullDate));Changed(nameof(WeekCells));}
        if (++ticks % 15 == 0) { RefreshPower(); if (ClipboardEnabled) Changed(nameof(Clips)); }
    }
    private void TimerChanged() { Changed(nameof(TimerText)); Changed(nameof(TimerButton)); Changed(nameof(FocusStatus)); Changed(nameof(FocusRunning)); Changed(nameof(FocusProgress));Changed(nameof(FocusTile)); }
    private void RefreshPower() { try { var p = PowerService.Read(); Battery = p.BatteryPresent ? $"{(p.Percent is { } n ? n + "%" : "Unknown")} · {(p.PluggedIn ? "plugged in" : "battery")}" : "No battery"; } catch { Battery = "Battery unavailable"; Log.Record("POWER_UNAVAILABLE"); } }
    public void RefreshAudio()
    {
        try { var audio = AudioService.Read(); Volume = Math.Round(audio.Volume * 100);outputMuted=audio.Muted;Changed(nameof(MuteTile)); AudioState = audio.Muted ? "Output muted" : "System output volume"; AudioAvailable = true; }
        catch { AudioAvailable = false; AudioState = "No accessible output device"; Log.Record("AUDIO_UNAVAILABLE"); }
        Changed(nameof(AudioState)); Changed(nameof(AudioAvailable));
    }
    public void SetVolume(double value) { try { AudioService.SetVolume((float)(value / 100)); Volume = value; } catch { Error("VOLUME_FAILED", "Volume could not be changed."); } }
    public void ReceiveAudio(AudioReading value,bool forceHud=false)
    {
        var percent=Math.Round(value.Volume*100);var changed=AudioAvailable&&(Math.Abs(Volume-percent)>.1||outputMuted!=value.Muted);AudioAvailable=true;Changed(nameof(AudioAvailable));
        Volume=percent;outputMuted=value.Muted;AudioState=value.Muted?"Output muted":"System output volume";Changed(nameof(AudioState));Changed(nameof(MuteTile));
        if(changed||forceHud)SystemLevelChanged?.Invoke(SystemHudKind.Volume,percent,value.Muted);
    }
    public void ReceiveBrightness(int value,bool forceHud=false)
    {
        var changed=Brightness.HasValue&&Brightness!=value;Brightness=Math.Clamp(value,0,100);Changed(nameof(Brightness));Changed(nameof(BrightnessState));Changed(nameof(BrightnessAvailable));
        if(changed||forceHud)SystemLevelChanged?.Invoke(SystemHudKind.Brightness,value,false);
    }
    public void SetTaskbarStatus(string value,bool tint=false){TaskbarStatus=value;TaskbarTintAvailable=tint;Changed(nameof(TaskbarStatus));Changed(nameof(TaskbarTintAvailable));}
    public void SetHardwareStatus(string value){HardwareStatus=value;Changed(nameof(HardwareStatus));}
    public async Task SetBrightnessAsync(int value)
    { try { await Task.Run(() => BrightnessService.Set(value)); ReceiveBrightness(value); } catch { Error("BRIGHTNESS_FAILED", "This display did not accept the brightness change."); } }
    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Take(100))
        {
            if (data.Shelf.Count >= 100) break;
            if (System.IO.Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal) && File.Exists(path) && !data.Shelf.Contains(path, StringComparer.OrdinalIgnoreCase)) data.Shelf.Add(path);
        }
        Changed(nameof(Shelf)); Changed(nameof(ShelfCount)); QueueSave(); Status = "Shelf stores local references. Original files stay in place.";
        PublishNotice("Within reach.", $"{data.Shelf.Count} local file reference{(data.Shelf.Count == 1 ? "" : "s")} on your shelf.");
    }
    private void OpenFile(string path)
    {
        try { if (!System.IO.Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || !File.Exists(path)) throw new FileNotFoundException(); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { Error("OPEN_FAILED", "The local file or application could not be opened."); }
    }
    public void CaptureClipboard()
    {
        if (!ClipboardEnabled || closing) return;
        try
        {
            if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return;
            if (Clipboard.ContainsText(TextDataFormat.UnicodeText)) ClipboardHistory.Add(Clipboard.GetText(TextDataFormat.UnicodeText));
            Changed(nameof(Clips));
        }
        catch { Log.Record("CLIPBOARD_BUSY"); }
    }
    private void PermitWeather()
    {
        if (MessageBox.Show("Allow weather for this session?\n\nOnly when you press Fetch weather, Aster will send your entered latitude and longitude to api.open-meteo.com. The provider also sees your public IP address. There is no automatic refresh. Coordinates are not saved.\n\nYou can revoke this permission at any time.", "Aster · weather permission", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Consent.Grant(); NetworkChanged();
    }
    private async Task FetchWeatherAsync()
    {
        if (!double.TryParse(Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) || !double.TryParse(Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) { Status = "Enter latitude and longitude using a decimal point."; return; }
        WeatherText = "Requesting weather…";
        try { var result = await weather.FetchAsync(lat, lon); if (!closing) WeatherText = $"{result.TemperatureC:0.#} °C · wind {result.WindKph:0.#} km/h · WMO code {result.Code}"; }
        catch (OperationCanceledException) { if (!closing) WeatherText = "Weather request cancelled or timed out."; }
        catch { if (!closing) { WeatherText = "Weather unavailable. Nothing is cached."; throw; } }
    }
    private void NetworkChanged() { Changed(nameof(NetworkEnabled)); Changed(nameof(NetworkState)); }
    public void EnsureIslandShown()
    {
        if(!Preferences.ShowOverlay)UpdatePreferences(Preferences with{ShowOverlay=true});
    }
    public void UpdatePreferences(Preferences preferences)
    {
        Preferences = preferences.Validated();Changed(nameof(ClockText));Changed(nameof(DisplayedMediaArtwork));Changed(nameof(FocusLengthLabel));Changed(nameof(BreakLengthLabel)); Changed(nameof(Preferences)); Changed(nameof(CardWidth));Changed(nameof(TaskbarTile));Changed(nameof(WallpaperButtonLabel));Changed(nameof(NightModeLabel)); AppearanceChanged?.Invoke();
        try { Store.Save("preferences.json", Preferences); Status = "Appearance saved."; } catch { Error("SETTINGS_SAVE_FAILED", "Preferences could not be saved. Check folder access."); }
    }
    private void QueueSave() { if (closing) return; saveTimer.Stop(); saveTimer.Start(); }
    private void Save()
    {
        if (closing) return;
        try { data = data.Validated(); Store.Save("workspace.json", data); } catch { Error("DATA_SAVE_FAILED", "Local data could not be saved. Check folder access."); }
    }
    private void DeleteData()
    {
        if (MessageBox.Show("Delete all Aster preferences, notes, tasks, saved references and logs, then quit?\n\nOriginal files, applications and the Windows clipboard will remain. This cannot be undone.", "Delete Aster data", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        saveTimer.Stop(); ClipboardEnabled = false; Consent.Revoke();
        try { BeforeDeleteData?.Invoke();Store.DeleteAll(); closing = true; Audit.Clear(); data = new(); RequestQuit?.Invoke(); }
        catch { Error("DELETE_FAILED", "Aster data could not be fully deleted. Close other instances and check folder access."); }
    }
    public void Error(string code, string message) { if (closing) return; Log.Record(code); Status = message; }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public void Dispose()
    {
        StopHeadphones();StopNotifications(); if (!closing) Save(); closing = true; clockTimer.Stop(); saveTimer.Stop(); ClipboardHistory.SetEnabled(false); Consent.Revoke(); media.Dispose(); weather.Dispose(); Consent.Dispose();
    }
}
