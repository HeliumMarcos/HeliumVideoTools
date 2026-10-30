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
        if (args.Length == 3 && args[0] == "--verify-public-release")
        {
            try { VerifyPublicReleaseAsync(args[1], args[2]).GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.Length >= 2 && args[0] == "--render-updates")
        {
            var app = new App(); app.InitializeComponent();
            var window = new UpdatesWindow(preview: true); window.Show(); window.UpdateLayout();
            if (args.Length > 2)
            {
                typeof(UpdatesWindow).GetMethod("SetTransfer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, [true]);
                var bar = (System.Windows.Controls.ProgressBar)window.FindName("DownloadProgress");
                bar.Value = 42; bar.IsIndeterminate = args[2] == "verify";
                ((System.Windows.Controls.TextBlock)window.FindName("TransferStatus")).Text = bar.IsIndeterminate ? "Verificando a integridade do instalador…" : "Baixando aplicativo • 42%";
                window.UpdateLayout();
            }
            window.Dispatcher.Invoke(() => window.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ContextIdle);
            var scroll = (System.Windows.Controls.ScrollViewer)window.FindName("UpdatesScroll");
            scroll.InvalidateMeasure(); window.UpdateLayout();
            Console.WriteLine($"Layout Atualizações: janela={window.ActualHeight:0}, conteúdo={scroll.ExtentHeight:0}, área={scroll.ViewportHeight:0}, rolagem={scroll.ComputedVerticalScrollBarVisibility}");
            Check(scroll.ComputedVerticalScrollBarVisibility != Visibility.Visible, "Atualizações exigiu rolagem no tamanho padrão");
            if (OperatingSystem.IsWindowsVersionAtLeast(10,0,22000) && !SystemParameters.HighContrast)
            {
                var theme = typeof(MainWindow).Assembly.GetType("HeliumVideoTools.Desktop.NativeWindowTheme")!;
                bool accepted = (bool)theme.GetMethod("Apply", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, [window])!;
                Check(accepted, "Windows rejeitou a aplicação das cores da barra nativa");
            }
            var visual = (FrameworkElement)window.Content; var dpi = VisualTreeHelper.GetDpi(visual);
            var bitmap = new RenderTargetBitmap((int)(visual.ActualWidth * dpi.DpiScaleX), (int)(visual.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(args[1])) encoder.Save(file);
            window.Close(); return 0;
        }
        if (args.Length >= 2 && args[0] == "--render")
        {
            var app = new App(); app.InitializeComponent();
            var window = new MainWindow { Width = args.Length > 3 ? int.Parse(args[3]) : 1280, Height = args.Length > 4 ? int.Parse(args[4]) : 900 };
            if (args.Length > 2 && args[2] != "Compress")
            {
                var nav = (System.Windows.Controls.Button)window.FindName(args[2] == "Split" ? "SplitNav" : "WebmNav");
                nav.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            }
            window.Show();
            if (args.Length > 5 && args[5] == "progress")
            {
                ((FrameworkElement)window.FindName("ProgressPanel")).Visibility = Visibility.Visible;
                ((System.Windows.Controls.TextBlock)window.FindName("ProgressStatus")).Text = "Exemplo de progresso • 1/3 arquivos • 42%";
                ((System.Windows.Controls.ProgressBar)window.FindName("OverallBar")).Value = 42;
            }
            window.UpdateLayout();
            var visual = (FrameworkElement)window.Content;
            var run = (FrameworkElement)window.FindName("RunButton");
            var bounds = run.TransformToAncestor(visual).TransformBounds(new Rect(run.RenderSize));
            Check(bounds.Top >= 0 && bounds.Bottom <= visual.ActualHeight && bounds.Right <= visual.ActualWidth, "Ação principal fora da área visível");
            var scroll = (System.Windows.Controls.ScrollViewer)window.FindName("PageScroll");
            scroll.ScrollToEnd(); window.UpdateLayout();
            var afterScroll = run.TransformToAncestor(visual).TransformBounds(new Rect(run.RenderSize));
            Check(bounds == afterScroll, "Ação principal se move ao rolar o formulário");
            scroll.ScrollToTop(); window.UpdateLayout();
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
    private static async Task VerifyPublicReleaseAsync(string tag, string destination)
    {
        Check(System.Text.RegularExpressions.Regex.IsMatch(tag, @"^v[0-9]+\.[0-9]+\.[0-9]+(-preview\.[0-9]+)?$"), "Tag inválida");
        using var http = ReleaseClient.CreateHttpClient(); var client = new ReleaseClient(http);
        using var metadata = System.Text.Json.JsonDocument.Parse(await client.ReadAsync(new($"https://api.github.com/repos/HeliumMarcos/HeliumVideoTools/releases/tags/{tag}"), 4_000_000, true, default));
        var name = $"HeliumVideoTools-{tag[1..].Split('-')[0]}-win-x64-Setup.exe";
        var asset = ReleaseClient.ParseAsset(metadata.RootElement, "HeliumMarcos/HeliumVideoTools", name);
        string path = Path.Combine(Path.GetFullPath(destination), name);
        await client.DownloadAsync(asset, path, null, default);
        using var verified = ReleaseClient.VerifyInstaller(path, asset);
        if (!tag.Contains("preview"))
        {
            var update = await client.CheckAppAsync(new Version(3,0,0,0), default);
            Check(update is not null && update.Version == Version.Parse(tag[1..]) && update.Installer.Sha256 == asset.Sha256, "A versão anterior não detectou a release oficial esperada");
            Check(await client.CheckAppAsync(Version.Parse(tag[1..]), default) is null, "A versão atual recebeu atualização para si mesma");
            Console.WriteLine("PASS: canal oficial detecta atualização desde 3.0.0 e não oferece atualização para a versão atual.");
        }
        Console.WriteLine($"PASS: download público anônimo real, tamanho {asset.Size}, SHA-256 e instalador MZ conferidos: {path}");
    }
    private static async Task RunAsync()
    {
        Check(VideoEngine.Bitrate(60, 98, 128) < 13000, "Orçamento deve usar MB decimal com margem.");
        try { VideoEngine.Bitrate(3600, 1, 128); throw new Exception("Alvo impossível aceito"); } catch (IOException) { }
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var ffmpeg = Path.Combine(root, "ffmpeg.exe"); var ffprobe = Path.Combine(root, "ffprobe.exe");
        if (!File.Exists(ffmpeg)) throw new Exception("FFmpeg ausente em " + root);
        var folder = Path.Combine(root, "work", "smoke-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        await ReleaseTests.RunAsync(folder);
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
        var sized = await engine.ProcessAsync(source, new(Operation.Split, SplitMode: SplitMode.Size, SplitValue: .15), progress, default);
        Check(sized.Count >= 2 && sized.All(p => new FileInfo(p).Length <= 150000), "Parte acima do limite definido");
        double sumDuration = 0;
        foreach (var path in sized) sumDuration += (await engine.ProbeAsync(path, default)).Duration;
        Check(Math.Abs(sumDuration - info.Duration) < .3, "Duração total dos trechos diverge do original");
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
