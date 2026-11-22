using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Aster.App;
public sealed class IslandIcon:FrameworkElement
{
    public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(IslandIcon),new FrameworkPropertyMetadata("today",FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty=DependencyProperty.Register(nameof(Stroke),typeof(Brush),typeof(IslandIcon),new FrameworkPropertyMetadata(Brushes.White,FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind{get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
    public Brush Stroke{get=>(Brush)GetValue(StrokeProperty);set=>SetValue(StrokeProperty,value);}
    private static readonly Dictionary<string,string> paths=new()
    {
        ["today"]="M3,3 L9,3 L9,9 L3,9 Z M15,3 L21,3 L21,9 L15,9 Z M3,15 L9,15 L9,21 L3,21 Z M15,15 L21,15 L21,21 L15,21 Z",
        ["music"]="M9,18 L9,5 L20,3 L20,16 M9,18 A3,3 0 1 1 3,18 A3,3 0 1 1 9,18 M20,16 A3,3 0 1 1 14,16 A3,3 0 1 1 20,16",
        ["focus"]="M12,4 A8,8 0 1 1 11.99,4 M9,1 L15,1 M12,8 L12,12 L16,14",
        ["files"]="M2,6 L9,6 L11,9 L22,9 L22,21 L2,21 Z",
        ["notes"]="M6,2 L18,2 L21,5 L21,22 L3,22 L3,2 Z M7,8 L17,8 M7,12 L17,12 M7,16 L14,16",
        ["controls"]="M2,5 L22,5 M2,12 L22,12 M2,19 L22,19 M7,2 L7,8 M16,9 L16,15 M10,16 L10,22",
        ["wifi"]="M2,8 Q12,-1 22,8 M5,12 Q12,5 19,12 M8,16 Q12,12 16,16 M11,20 L13,20",
        ["bluetooth"]="M12,1 L12,23 L19,17 L5,7 M5,17 L19,7 L12,1",
        ["volume"]="M3,9 L7,9 L12,4 L12,20 L7,15 L3,15 Z M16,8 Q20,12 16,16 M19,5 Q25,12 19,19",
        ["sun"]="M12,7 A5,5 0 1 1 11.99,7 M12,1 L12,4 M12,20 L12,23 M1,12 L4,12 M20,12 L23,12 M4,4 L6,6 M18,18 L20,20 M4,20 L6,18 M18,6 L20,4",
        ["battery"]="M2,6 L20,6 L20,18 L2,18 Z M23,9 L23,15 M5,9 L15,9 L15,15 L5,15 Z",
        ["taskbar"]="M2,4 L22,4 L22,20 L2,20 Z M2,16 L22,16 M8,18 L10,18 M13,18 L15,18",
        ["settings"]="M10,2 L14,2 L15,5 L18,7 L21,7 L23,11 L21,14 L19,17 L19,20 L15,22 L12,20 L9,20 L6,22 L2,19 L3,16 L3,12 L1,9 L4,6 L7,6 Z M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12"
    };
    protected override void OnRender(DrawingContext dc)
    {
        if(!paths.TryGetValue(Kind,out var path))return;
        var pen=new Pen(Stroke,1.65){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        dc.PushTransform(new ScaleTransform(ActualWidth/24,ActualHeight/24));dc.DrawGeometry(null,pen,Geometry.Parse(path));dc.Pop();
    }
}
