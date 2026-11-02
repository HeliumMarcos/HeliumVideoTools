using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;
using HeliumVideoTools.Desktop;

internal static class RecoveredSummaryTests
{
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);var app=new App(isolated:true){ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
        var window=new MainWindow(preview:true){ShowActivated=false};window.Show();window.UpdateLayout();
        var result=new BatchResult([new("a.mp4",BatchItemState.Succeeded,[Path.GetFullPath(Path.Combine(folder,"done.mp4"))]),new("b.mp4",BatchItemState.Cancelled,[],"Processamento interrompido inesperadamente."),new("c.mp4",BatchItemState.NotStarted,[])],true);
        window.RestoreRecoveredBatch(new(result,Path.GetFullPath(Path.Combine(folder,"report.txt")),DateTimeOffset.UtcNow));window.UpdateLayout();
        if(((Border)window.FindName("ProgressPanel")).Visibility!=Visibility.Visible || ((Button)window.FindName("ReportButton")).Visibility!=Visibility.Visible || !((Button)window.FindName("ProgressAction")).IsEnabled)throw new Exception("Resumo recuperado não disponibilizou relatório/pasta");
        var model=(BatchViewModel)window.DataContext;
        if(model.LastResult!=result || !((TextBlock)window.FindName("ProgressStatus")).Text.Contains("1 concluído"))throw new Exception("Estado recuperado não chegou ao resumo");
        window.RestoreRecoveredBatch(new(new([],true),"outro.txt",DateTimeOffset.UtcNow));if(model.LastResult!=result)throw new Exception("Resultado atual substituído por recuperação tardia");
        var root=(FrameworkElement)window.Content;
        foreach(double scale in new[]{1d,1.25,1.5,2})
        {
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*scale),(int)Math.Ceiling(root.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(root);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(folder,$"summary-{scale:0.##}.png"));encoder.Save(file);
        }
        window.Close();Console.WriteLine("PASS: resumo recuperado, relatório/pasta, recuperação tardia ignorada e render em 100/125/150/200% (não teste físico de monitor).");
    }
}
