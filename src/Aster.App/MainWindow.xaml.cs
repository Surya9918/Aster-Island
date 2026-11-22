using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aster.Core;

namespace Aster.App;

public partial class MainWindow : Window
{
    private readonly AppModel model;
    private Point dragStart;
    public bool Exiting { get; set; }
    public MainWindow(AppModel model)
    {
        this.model = model; InitializeComponent(); DataContext = model;
        FontBox.ItemsSource = new[] { "Segoe UI", "Segoe UI Variable", "Arial", "Calibri", "Consolas", "Tahoma", "Verdana" };
        MonitorBox.ItemsSource = System.Windows.Forms.Screen.AllScreens.Select((s, i) => $"Display {i + 1} · {s.Bounds.Width} × {s.Bounds.Height}").ToArray();
        LoadAppearance();
        Closing += OnClosing;
        model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(AppModel.Preferences))SpotifyWallpaperCheck.IsChecked=model.Preferences.SpotifyWallpaper;};
    }
    private void OnClosing(object? sender, CancelEventArgs e) { if (Exiting) return; e.Cancel = true; Hide(); }
    private void HideWindow(object sender, RoutedEventArgs e) => Hide();
    private void ApplyVolume(object sender, RoutedEventArgs e) => model.SetVolume(VolumeSlider.Value);
    private void RefreshAudio(object sender, RoutedEventArgs e) => model.RefreshAudio();
    private async void ApplyBrightness(object sender, RoutedEventArgs e) => await model.SetBrightnessAsync((int)BrightnessSlider.Value);
    private void FilesDropped(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths) { model.AddFiles(paths); e.Handled = true; } }
    private void FilesDragOver(object sender, DragEventArgs e)
    { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Link | DragDropEffects.Copy; e.Handled = true; } }
    private void ShelfMouseDown(object sender, MouseButtonEventArgs e) => dragStart = e.GetPosition(this);
    private void ShelfMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox list || list.SelectedItem is not ShelfRow row) return;
        var now = e.GetPosition(this);
        if (Math.Abs(now.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(now.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        if (!System.IO.File.Exists(row.Path)) return;
        DragDrop.DoDragDrop(list, new DataObject(DataFormats.FileDrop, new[] { row.Path }), DragDropEffects.Copy | DragDropEffects.Link);
    }
    private void LoadAppearance()
    {
        var p = model.Preferences;
        ThemeBox.SelectedIndex = p.Theme == "Light" ? 1 : 0; AccentBox.Text = p.Accent;
        BackgroundBox.Text = p.BackgroundColor; CardBox.Text = p.CardColor; TextColorBox.Text = p.TextColor;
        FontBox.Text = p.Font; ScaleSlider.Value = p.TextScale; RadiusSlider.Value = p.Radius; WidthSlider.Value = p.OverlayWidth;
        OpacitySlider.Value = p.Opacity; OffsetSlider.Value = p.TopOffset; MonitorBox.SelectedIndex = Math.Min(p.Monitor, MonitorBox.Items.Count - 1);
        LayoutBox.SelectedIndex = p.Layout == "Compact" ? 1 : 0; OverlayCheck.IsChecked = p.ShowOverlay; MediaCheck.IsChecked = p.ShowMedia; BatteryCheck.IsChecked = p.ShowBattery; MotionCheck.IsChecked = p.ReducedMotion;
        GlassSlider.Value=p.GlassOpacity;IslandScaleSlider.Value=p.IslandScale;
        StiffnessBox.Text=p.SpringStiffness.ToString(System.Globalization.CultureInfo.InvariantCulture);DampingBox.Text=p.SpringDamping.ToString(System.Globalization.CultureInfo.InvariantCulture);MassBox.Text=p.SpringMass.ToString(System.Globalization.CultureInfo.InvariantCulture);
        AutoStowCheck.IsChecked=p.AutoStowMaximized;VolumeKeysCheck.IsChecked=p.ReplaceVolumeKeys;BrightnessKeysCheck.IsChecked=p.ReplaceBrightnessKeys;
        DockBrowserCheck.IsChecked=p.DockBrowserShortcut;CustomDockCheck.IsChecked=p.CustomDock;DockScaleSlider.Value=p.DockScale;SpotifyWallpaperCheck.IsChecked=p.SpotifyWallpaper;TaskbarGlassCheck.IsChecked=p.TaskbarGlass;TaskbarTintSlider.Value=p.TaskbarTintOpacity;ShineSlider.Value=p.GlassShine;
    }
    private void SaveAppearance(object sender, RoutedEventArgs e)
    {
        if(!double.TryParse(StiffnessBox.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var stiffness)||!double.TryParse(DampingBox.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var damping)||!double.TryParse(MassBox.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var mass))
        {model.Error("SPRING_INVALID","Enter spring values using decimal points.");return;}
        var colors = new[] { AccentBox.Text, BackgroundBox.Text, CardBox.Text, TextColorBox.Text };
        if (colors.Where((c, i) => i == 0 || !string.IsNullOrWhiteSpace(c)).Any(c => !System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$")))
        { model.Error("COLOR_INVALID", "Enter colors as #RRGGBB; overrides may be empty."); return; }
        model.UpdatePreferences(model.Preferences with
        {
            Theme = ThemeBox.SelectedIndex == 1 ? "Light" : "Dark", Accent = AccentBox.Text,
            BackgroundColor = BackgroundBox.Text, CardColor = CardBox.Text, TextColor = TextColorBox.Text,
            Font = FontBox.Text, TextScale = ScaleSlider.Value, Radius = RadiusSlider.Value, OverlayWidth = WidthSlider.Value,
            Opacity = OpacitySlider.Value, TopOffset = OffsetSlider.Value, Monitor = Math.Max(0, MonitorBox.SelectedIndex),
            Layout = LayoutBox.SelectedIndex == 1 ? "Compact" : "Balanced", ShowOverlay = OverlayCheck.IsChecked == true,
            ShowMedia = MediaCheck.IsChecked == true, ShowBattery = BatteryCheck.IsChecked == true, ReducedMotion = MotionCheck.IsChecked == true
            ,GlassOpacity=GlassSlider.Value,IslandScale=IslandScaleSlider.Value,SpringStiffness=stiffness,SpringDamping=damping,SpringMass=mass
            ,AutoStowMaximized=AutoStowCheck.IsChecked==true,ReplaceVolumeKeys=VolumeKeysCheck.IsChecked==true,ReplaceBrightnessKeys=BrightnessKeysCheck.IsChecked==true
            ,DockBrowserShortcut=DockBrowserCheck.IsChecked==true,CustomDock=CustomDockCheck.IsChecked==true,DockScale=DockScaleSlider.Value,SpotifyWallpaper=SpotifyWallpaperCheck.IsChecked==true,TaskbarGlass=TaskbarGlassCheck.IsChecked==true,TaskbarTintOpacity=(int)TaskbarTintSlider.Value,GlassShine=ShineSlider.Value
        });
    }
    private void ResetAppearance(object sender, RoutedEventArgs e) { model.UpdatePreferences(new Preferences()); LoadAppearance(); }
}
