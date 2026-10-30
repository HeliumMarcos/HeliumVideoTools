using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HeliumVideoTools.Desktop;

/// <summary>Ícones vetoriais locais, sem dependência de fontes ou rede.</summary>
public sealed class Symbol : Control
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(Symbol), new FrameworkPropertyMetadata("video", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    private static readonly Dictionary<string, Geometry> Icons = new Dictionary<string, string>
    {
        ["video"] = "M4,5 L16,5 Q18,5 18,7 L18,17 Q18,19 16,19 L4,19 Q2,19 2,17 L2,7 Q2,5 4,5 M18,10 L23,7 L23,17 L18,14 M8,9 L13,12 L8,15 Z",
        ["split"] = "M5,3 L5,8 Q5,12 10,12 L17,12 M5,21 L5,16 Q5,12 10,12 M14,8 L18,12 L14,16 M3,3 L7,3 M3,21 L7,21",
        ["compress"] = "M5,3 L19,3 Q21,3 21,5 L21,19 Q21,21 19,21 L5,21 Q3,21 3,19 L3,5 Q3,3 5,3 M12,5 L12,9 M9,7 L12,10 L15,7 M12,19 L12,15 M9,17 L12,14 L15,17",
        ["convert"] = "M3,8 L19,8 M15,4 L19,8 L15,12 M21,16 L5,16 M9,12 L5,16 L9,20",
        ["add"] = "M12,5 L12,19 M5,12 L19,12",
        ["remove"] = "M5,12 L19,12",
        ["clear"] = "M4,6 L20,6 M9,6 L9,3 L15,3 L15,6 M6,6 L7,21 L17,21 L18,6 M10,10 L10,17 M14,10 L14,17",
        ["folder"] = "M2,7 L2,19 Q2,21 4,21 L20,21 Q22,21 22,19 L22,9 Q22,7 20,7 L12,7 L9,4 L4,4 Q2,4 2,7",
        ["upload"] = "M12,16 L12,3 M7,8 L12,3 L17,8 M4,15 L4,20 Q4,21 5,21 L19,21 Q20,21 20,20 L20,15",
        ["size"] = "M4,4 L20,4 L20,20 L4,20 Z M8,8 L16,8 M8,12 L14,12 M8,16 L12,16",
        ["preset"] = "M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M8,3 L8,9 M16,9 L16,15 M10,15 L10,21",
        ["codec"] = "M5,4 L19,4 L19,20 L5,20 Z M5,8 L19,8 M5,16 L19,16 M9,4 L9,8 M15,4 L15,8 M9,16 L9,20 M15,16 L15,20",
        ["audio"] = "M3,9 L7,9 L12,5 L12,19 L7,15 L3,15 Z M16,8 Q21,12 16,16 M19,5 Q26,12 19,19",
        ["check"] = "M5,12 L10,17 L20,6",
        ["shield"] = "M12,2 L21,6 L20,14 Q19,19 12,22 Q5,19 4,14 L3,6 Z M8,12 L11,15 L17,9",
        ["estimate"] = "M3,20 L3,14 M9,20 L9,10 M15,20 L15,6 M21,20 L21,2",
        ["parts"] = "M3,3 L10,3 L10,10 L3,10 Z M14,3 L21,3 L21,10 L14,10 Z M3,14 L10,14 L10,21 L3,21 Z M14,14 L21,14 L21,21 L14,21 Z",
        ["clock"] = "M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 M12,6 L12,12 L16,14",
        ["cancel"] = "M6,6 L18,18 M18,6 L6,18",
        ["arrow"] = "M4,12 L20,12 M14,6 L20,12 L14,18"
    }.ToDictionary(pair => pair.Key, pair => Geometry.Parse(pair.Value));
    public Symbol() { Width = Height = 20; Focusable = false; IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext dc)
    {
        if (!Icons.TryGetValue(Kind, out var geometry)) return;
        var pen = new Pen(Foreground, 1.65) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        dc.PushTransform(new ScaleTransform(ActualWidth / 26, ActualHeight / 26));
        dc.PushTransform(new TranslateTransform(1, 1)); dc.DrawGeometry(null, pen, geometry); dc.Pop(); dc.Pop();
    }
}
