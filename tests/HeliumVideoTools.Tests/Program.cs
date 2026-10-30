using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HeliumVideoTools.Core;
using HeliumVideoTools.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--render")
        {
            var app = new App(); app.InitializeComponent();
            var window = new MainWindow { Width = 1280, Height = 900 };
            window.Show(); window.UpdateLayout();
            var visual = (FrameworkElement)window.Content;
            var dpi = VisualTreeHelper.GetDpi(visual);
            var bitmap = new RenderTargetBitmap((int)(visual.ActualWidth * dpi.DpiScaleX), (int)(visual.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(args[1])) png.Save(stream);
            window.Close(); Console.WriteLine("Prévia WPF renderizada: " + args[1]); return 0;
        }
        try { RunAsync().GetAwaiter().GetResult(); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task RunAsync()
    {
        Check(VideoEngine.Bitrate(60, 98, 128) < 13000, "Orçamento deve usar MB decimal com margem.");
        try { VideoEngine.Bitrate(3600, 1, 128); throw new Exception("Alvo impossível aceito"); } catch (IOException) { }
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var ffmpeg = Path.Combine(root, "ffmpeg.exe"); var ffprobe = Path.Combine(root, "ffprobe.exe");
        if (!File.Exists(ffmpeg)) throw new Exception("FFmpeg ausente em " + root);
        var folder = Path.Combine(root, "work", "smoke-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "audiência teste.mp4");
        await VideoEngine.ExecuteAsync(ffmpeg, ["-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=25:duration=8", "-f", "lavfi", "-i", "sine=frequency=440:duration=8", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "12", "-c:a", "aac", "-shortest", source], null, default);
        var engine = new VideoEngine(ffmpeg, ffprobe); var info = await engine.ProbeAsync(source, default);
        Check(info.Duration > 7.5 && info.AudioKbps > 0, "Sondagem JSON perdeu duração/áudio");
        var progress = new InlineProgress();
        var compressed = await engine.ProcessAsync(source, new(Operation.Compress, info.Bytes / 1000000d * .7, TwoPass: true), progress, default);
        Check(new FileInfo(compressed[0]).Length < info.Bytes * .7, "Compactação acima do limite");
        foreach (var codec in new[] { "vp8", "vp9" })
        {
            var result = await engine.ProcessAsync(source, new(Operation.Webm, 299, codec, TwoPass: true), progress, default);
            Check(new FileInfo(result[0]).Length < info.Bytes, "WebM aumentou acima do arquivo original");
            Check((await engine.ProbeAsync(result[0], default)).AudioKbps > 0, "WebM perdeu áudio");
        }
        var split = await engine.ProcessAsync(source, new(Operation.Split, SplitValue: 2), progress, default);
        Check(split.Count == 2 && split.All(File.Exists), "Divisão inválida");
        Check(progress.Maximum >= 99, "Progresso não chegou ao fim");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { await engine.ProcessAsync(source, new(), progress, canceled.Token); throw new Exception("Cancelamento ignorado"); } catch (OperationCanceledException) { }
        using var activeCancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var canceledOutput = Path.Combine(folder, "cancelado.mp4");
        try { await VideoEngine.ExecuteAsync(ffmpeg, ["-y", "-re", "-i", source, "-c", "copy", canceledOutput], null, activeCancel.Token); throw new Exception("Cancelamento de processo ativo ignorado"); } catch (OperationCanceledException) { }
        Check(!Directory.GetFiles(folder, "*.partial.*").Any(), "Temporário permaneceu após operação");
        Console.WriteLine("PASS: sondagem, áudio, compactação 2-pass, VP8/VP9 sem aumento, divisão, progresso, cancelamento prévio/ativo e temporários.");
        Console.WriteLine("Amostras: " + folder);
    }
    private sealed class InlineProgress : IProgress<VideoProgress> { public double Maximum; public void Report(VideoProgress p) => Maximum = Math.Max(Maximum, p.Percent); }
}
