using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HeliumVideoTools.Desktop;

internal static class AudioSettingsTests
{
    private static void Check(bool value,string text){if(!value)throw new Exception(text);}
    private static IEnumerable<DependencyObject> Walk(DependencyObject obj)
    {yield return obj;foreach(var c in LogicalTreeHelper.GetChildren(obj).OfType<DependencyObject>())foreach(var d in Walk(c))yield return d;}
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);var app=new App(isolated:true){ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();ThemeManager.SetPreference("dark");
        var w=new MainWindow(preview:true){ShowActivated=false};w.Show();var panel=(StackPanel)w.FindName("OptionsPanel");
        T Field<T>(string name) where T:FrameworkElement=>Walk(panel).OfType<T>().Single(el=>System.Windows.Automation.AutomationProperties.GetName(el)==name);
        var copy=Field<CheckBox>("Áudio: Manter áudio original");var rate=Field<ComboBox>("Taxa de áudio em kbps");var channels=Field<ComboBox>("Canais de áudio");
        Check(!rate.IsVisible && w.Editor.Read() is {CopyAudio:true,AudioBitrateKbps:128,AudioChannels:0},"Padrões/ocultação de áudio incorretos");
        copy.IsChecked=false;w.UpdateLayout();Check(rate.IsVisible && channels.IsVisible,"Desmarcar não revelou configuração");
        rate.SelectedIndex=5;channels.SelectedIndex=1;
        Check(w.Editor.Read() is {CopyAudio:false,AudioBitrateKbps:256,AudioChannels:1},"Campos não mapearam taxa/mono");
        copy.IsChecked=true;Check(w.Editor.Read() is {CopyAudio:true,AudioBitrateKbps:128,AudioChannels:0},"Campos ocultos contaminaram cópia");
        copy.IsChecked=false;Check(w.Editor.Read() is {AudioBitrateKbps:256,AudioChannels:1},"Alternar perdeu ajustes da sessão");
        var model=(BatchViewModel)w.DataContext;
        typeof(BatchViewModel).GetProperty(nameof(model.LastResult))!.SetValue(model,new HeliumVideoTools.Core.BatchResult([],false));
        ((Border)w.FindName("ProgressPanel")).Visibility=Visibility.Visible;((Border)w.FindName("ActionBar")).Visibility=Visibility.Collapsed;
        rate.SelectedIndex=4;
        Check(model.LastResult is null && ((Border)w.FindName("ActionBar")).Visibility==Visibility.Visible,"Mudar áudio após resultado não retornou à edição");
        rate.SelectedIndex=2;channels.SelectedIndex=0;
        var root=(Grid)w.Content;root.HorizontalAlignment=HorizontalAlignment.Left;root.VerticalAlignment=VerticalAlignment.Top;
        foreach(string state in new[]{"idle","progress","result"})
        foreach(var size in new[]{(1120,700),(1280,650),(1536,800),(900,650),(675,449)})
        {
            ((Border)w.FindName("ActionBar")).Visibility=state=="idle"?Visibility.Visible:Visibility.Collapsed;
            ((Border)w.FindName("ProgressPanel")).Visibility=state=="idle"?Visibility.Collapsed:Visibility.Visible;
            ((Button)w.FindName("ResetButton")).Visibility=((Button)w.FindName("ReportButton")).Visibility=state=="result"?Visibility.Visible:Visibility.Collapsed;
            ((TextBlock)w.FindName("ProgressStatus")).Text=state=="progress"?"1/1 · audiência.mp4 · 42% · restante ≈ 00:25":"1 concluído · 0 erros";
            ((TextBlock)w.FindName("ResultDetails")).Visibility=state=="result"?Visibility.Visible:Visibility.Collapsed;
            ((TextBlock)w.FindName("ResultDetails")).Text="Processamento concluído. Os arquivos originais foram preservados.";
            w.Editor.Estimate!.Text="Orçamento: ≈ 194,0 MB no total • 2 vídeo(s)";w.Editor.EstimateNote!.Text="Teto aplicado: 98,0 MB por vídeo. O tamanho real pode variar e será conferido.";w.Editor.EstimateNote.Visibility=Visibility.Visible;
            root.Width=size.Item1;root.Height=size.Item2;w.AdaptiveLayout!.Refresh(size.Item1,size.Item2);root.Measure(new Size(size.Item1,size.Item2));root.Arrange(new Rect(0,0,size.Item1,size.Item2));root.UpdateLayout();
            if(size.Item1>=900)
            {
                var scroll=(ScrollViewer)w.FindName("PageScroll");Check(scroll.ComputedVerticalScrollBarVisibility!=Visibility.Visible,$"Áudio expandido exigiu rolagem: {state}/{size}, conteúdo {scroll.ExtentHeight:0}, área {scroll.ViewportHeight:0}");
                foreach(var field in new[]{rate,channels}){var b=field.TransformToAncestor(root).TransformBounds(new Rect(field.RenderSize));Check(b.Right<=size.Item1+1 && b.Bottom<=size.Item2+1,"Campo de áudio cortado");}
            }
            Check(w.Editor.Read() is {AudioBitrateKbps:128,AudioChannels:0},"Reflow alterou áudio");
            if(size==(1120,700)){var bmp=new RenderTargetBitmap(1120,700,96,96,PixelFormats.Pbgra32);bmp.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var f=File.Create(Path.Combine(folder,$"audio-{state}.png"));encoder.Save(f);}
        }
        ((Button)w.FindName("ResetButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(w.Editor.Read() is {CopyAudio:true,AudioBitrateKbps:128,AudioChannels:0},"Reset não protegeu padrões de áudio");w.Close();
        Console.WriteLine("PASS: mostrar/ocultar AAC, padrões, taxa/canais, cópia protegida, redimensionamento e reset.");
    }
}
