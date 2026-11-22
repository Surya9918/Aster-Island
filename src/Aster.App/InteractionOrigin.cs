using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace Aster.App;
internal static class InteractionOrigin
{
 public static bool IsButton(object source)
 {
  DependencyObject? node=source as DependencyObject;
  while(node is not null){if(node is ButtonBase)return true;if(node is ListBoxItem)return false;node=node is Visual?VisualTreeHelper.GetParent(node)??LogicalTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}
  return false;
 }
}
