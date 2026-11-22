using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using Aster.Core;
namespace Aster.App;
internal static class StartupProbe
{
 public static async Task RunAsync(OverlayWindow island,AppModel model,Action open,string directory)
 {
  Directory.CreateDirectory(directory);
  await Task.Delay(1100);
  var launch=model.Preferences.ShowOverlay&&island.IsVisible&&island.Machine.Target==IslandState.Idle&&island.SurfaceBounds.Height>30;
  model.UpdatePreferences(model.Preferences with{ShowOverlay=false});await Task.Delay(800);
  var hide=island.Machine.Target==IslandState.Hidden;
  open();await Task.Delay(1100);
  var reopen=model.Preferences.ShowOverlay&&island.Machine.Target!=IslandState.Hidden;
  var persisted=model.Store.Load("preferences.json",new Preferences{ShowOverlay=false}).ShowOverlay;
  await File.WriteAllTextAsync(Path.Combine(directory,"startup.json"),JsonSerializer.Serialize(new{launch,hide,reopen,persisted,taskbarDisabled=!model.Preferences.TaskbarGlass}));
  if(!launch||!hide||!reopen||!persisted)throw new InvalidOperationException("Startup visibility regression.");
 }
}
