using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aster.Platform.Windows;

namespace Aster.App;
internal static class TaskbarProbe
{
    public static async Task RunAsync(string path)
    {
        using var material=new TaskbarMaterialService();
        var accepted=material.Apply(110);var status=material.Status;var count=material.StyledCount;
        await Task.Delay(800);material.Restore();
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{accepted,taskbars=count,status,restored=material.RestoreSucceeded,scope="API acceptance/restoration only; not visual certification of Explorer XAML"},new JsonSerializerOptions{WriteIndented=true}));
    }
}
