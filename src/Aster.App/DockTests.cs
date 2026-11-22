using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aster.Core;
using Aster.Infrastructure;
using Aster.Platform.Windows;
namespace Aster.App;
internal static class DockTests
{
    public static async Task CrashHelper(string lease)
    {
        var original=NativeTaskbar.Capture();Directory.CreateDirectory(Path.GetDirectoryName(lease)!);
        File.WriteAllText(lease,JsonSerializer.Serialize(new DockLease(Guid.NewGuid(),Environment.ProcessId,original)));
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--dock-guardian",Environment.ProcessId.ToString(),lease}});
        NativeTaskbar.SetState(original.State|1);for(int i=0;i<1200;i++){NativeTaskbar.Hide();if(i==4)File.WriteAllText(lease+".ready","ready");await Task.Delay(100);}NativeTaskbar.Restore(original);
    }
    public static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);var report=new Dictionary<string,object>();var root=Path.Combine(Path.GetTempPath(),"Aster-Dock-Tests",Guid.NewGuid().ToString("N"));using var model=new AppModel(new LocalStore(root));
        var before=NativeTaskbar.Capture();var originalArea=NativeTaskbar.WorkingArea(IntPtr.Zero);
        async Task Until(Func<bool> condition){var watch=Stopwatch.StartNew();while(!condition()){if(watch.Elapsed>TimeSpan.FromSeconds(8))throw new InvalidOperationException("Dock check timed out.");await Task.Delay(60);}}
        using(var dock=new DockWindow(model,()=>{},_=>{},true))
        {
            dock.Show();await Until(()=>dock.ApplicationButtonCount>0);await Task.Delay(900);Capture(dock,Path.Combine(directory,"01-idle.png"));report["actualAppButtons"]=dock.ApplicationButtonCount;
            report["previewLeavesTaskbarUnchanged"]=NativeTaskbar.Capture()==before;
            var button=Descendants(dock).OfType<Button>().First();button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});await Task.Delay(350);
            report["hoverSpringScale"]=((ScaleTransform)button.RenderTransform).ScaleX>1.05;Capture(dock,Path.Combine(directory,"02-hover.png"));
            button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseLeaveEvent});
            var pixels=new byte[64*64*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=170;pixels[i+1]=100;pixels[i+2]=40;pixels[i+3]=255;}var image=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,pixels,256);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=new MemoryStream();encoder.Save(stream);
            model.UpdateMedia(new("Dock artwork fixture","Original local test image",true,true,stream.ToArray(),"Spotify.exe"));await Task.Delay(550);Capture(dock,Path.Combine(directory,"03-media.png"));report["mediaArtBound"]=Descendants(dock).OfType<Image>().Any(i=>ReferenceEquals(i.Source,model.MediaArtwork));
            foreach(var scale in new[]{.85,1.0,1.2}){model.UpdatePreferences(model.Preferences with{DockScale=scale});dock.Place();await Task.Delay(650);Capture(dock,Path.Combine(directory,$"scale-{scale:0.00}.png"));if(dock.SurfaceBounds.Right>dock.Width||dock.SurfaceBounds.Left<0)throw new InvalidOperationException("Scaled dock exceeds its host.");}
            report["scaleBounds"]=true;dock.Exiting=true;dock.Close();
        }
        using(var dock=new DockWindow(model,()=>{},_=>{}))
        {
            try
            {
                dock.Show();await Task.Delay(1200);report["nativeTaskbarHidden"]=NativeTaskbar.Hidden;report["dockVisible"]=dock.IsVisible;report["desktopSpaceReserved"]=NativeTaskbar.WorkingArea(new System.Windows.Interop.WindowInteropHelper(dock).Handle).Bottom<originalArea.Bottom;
                Capture(dock,Path.Combine(directory,"04-live-native.png"));dock.RevealNative();await Task.Delay(250);report["nativeTrayAccess"]=!NativeTaskbar.Hidden;
                dock.HideNativeNow();await Until(()=>NativeTaskbar.Hidden);report["nativeRehide"]=true;
            }
            finally{dock.Dispose();dock.Exiting=true;dock.Close();}
        }
        await Task.Delay(500);report["normalExitRestores"]=NativeTaskbar.Capture()==before;
        var crash=Path.Combine(root,"guardian-check.json");var child=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--dock-crash-helper",crash}})!;
        try{await Until(()=>File.Exists(crash+".ready"));await Until(()=>NativeTaskbar.Hidden);report["crashHelperHidTaskbar"]=true;child.Kill();await child.WaitForExitAsync();await Until(()=>!File.Exists(crash)&&NativeTaskbar.Capture()==before);report["guardianCrashRestores"]=true;}
        finally{if(!child.HasExited)child.Kill();NativeTaskbar.Restore(before);child.Dispose();}
        report["architecture"]=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();report["scope"]="Actual WPF dock, live app icons, own-window rendered pixels, native taskbar hide/reveal/normal restore and recovery after terminating only the dedicated helper. Start/notification flyouts and physical touch need separate UI verification.";
        await File.WriteAllTextAsync(Path.Combine(directory,"dock-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        if(report.Values.OfType<bool>().Any(v=>!v))throw new InvalidOperationException("A dock check failed.");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var node in Descendants(child))yield return node;}}
    private static void Capture(Window window,string path){window.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file);}
}
