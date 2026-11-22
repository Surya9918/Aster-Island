using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aster.Core;
namespace Aster.App;
internal static class ShelfProbe
{
 public static async Task RunAsync(OverlayWindow island,AppModel model,string directory)
 {
  Directory.CreateDirectory(directory);
  var file=Path.Combine(model.Store.Root,"Shelf sample.txt");Directory.CreateDirectory(model.Store.Root);await File.WriteAllTextAsync(file,"Original local file must survive removal.");
  model.AddFiles(new[]{file});island.Machine.Expand(IslandPage.Shelf);await Task.Delay(1600);island.UpdateLayout();
  Button? Find(DependencyObject node){if(node is Button b&&b.Content as string=="Remove")return b;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var hit=Find(VisualTreeHelper.GetChild(node,i));if(hit!=null)return hit;}return null;}
  var remove=Find(island)??throw new InvalidOperationException("Remove button missing.");
  if(remove.Command is null||!remove.Command.CanExecute(remove.CommandParameter))throw new InvalidOperationException("Remove binding failed.");
  var bitmap=new RenderTargetBitmap((int)island.ActualWidth,(int)island.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(island);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(directory,"file-shelf.png")))png.Save(stream);
  remove.Command.Execute(remove.CommandParameter);await Task.Delay(1200);
  if(model.Shelf.Any()||model.ShelfCount!=0||!File.Exists(file))throw new InvalidOperationException("Removal failed or original lost.");
  var saved=await File.ReadAllTextAsync(Path.Combine(model.Store.Root,"workspace.json"));
  if(saved.Contains("Shelf sample"))throw new InvalidOperationException("Removal not persisted.");
  await File.WriteAllTextAsync(Path.Combine(directory,"shelf.json"),"{\"removeButtonBound\":true,\"entryRemoved\":true,\"countUpdated\":true,\"originalKept\":true,\"persisted\":true}");
 }
}
