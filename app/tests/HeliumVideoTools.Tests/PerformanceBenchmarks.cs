using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;
using HeliumVideoTools.Desktop;

internal static class PerformanceBenchmarks
{
    internal static void Startup(string destination)
    {
        var managed=Stopwatch.StartNew();
        var app=new App(isolated:true) {ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
        ThemeManager.EnsureInitialized();ThemeManager.SetPreference("dark");
        var window=new MainWindow(preview:true);
        window.ContentRendered+=(_,_)=>
        {
            _=window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>
            {
                using var process=Process.GetCurrentProcess();process.Refresh();
                if(app.Windows.Count!=1)throw new Exception("Benchmark não tem exatamente uma janela isolada");
                var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(window);
                File.WriteAllText(destination,JsonSerializer.Serialize(new
                {
                    ManagedToIdleMs=managed.Elapsed.TotalMilliseconds,
                    ProcessToIdleMs=(DateTime.UtcNow-process.StartTime.ToUniversalTime()).TotalMilliseconds,
                    WorkingSetMb=process.WorkingSet64/1_000_000d,PrivateMb=process.PrivateMemorySize64/1_000_000d,
                    PeakWorkingSetMb=process.PeakWorkingSet64/1_000_000d,
                    WindowCount=app.Windows.Count,DpiX=dpi.PixelsPerInchX,DpiY=dpi.PixelsPerInchY,Theme="dark",
                    Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,Version=typeof(MainWindow).Assembly.GetName().Version!.ToString(3),
                    Scenario="Isolated App and one MainWindow(preview:true): no preferences, automatic network check or startup recovery"
                }));
                window.Close();app.Shutdown();
            }));
        };
        window.Show();app.Run();
    }
    internal static async Task RunAsync(string destination,string ffmpeg,string ffprobe)
    {
        destination=Path.GetFullPath(destination);Directory.CreateDirectory(destination);
        string run=Path.Combine(destination,"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(run);
        var startup=new List<JsonElement>();
        for(int i=0;i<6;i++)
        {
            string result=Path.Combine(run,$"startup-{i+1}.json");
            var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
            if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(PerformanceBenchmarks).Assembly.Location);
            start.ArgumentList.Add("--benchmark-startup");start.ArgumentList.Add(result);
            using var child=Process.Start(start)??throw new Exception("Benchmark não iniciou");
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40));
            try{await child.WaitForExitAsync(timeout.Token);}catch{if(!child.HasExited)child.Kill(true);throw;}
            if(child.ExitCode!=0 || !File.Exists(result))throw new Exception("Falha no benchmark de abertura: "+await child.StandardError.ReadToEndAsync());
            using var doc=JsonDocument.Parse(File.ReadAllText(result));startup.Add(doc.RootElement.Clone());
            Console.WriteLine($"Abertura isolada {i+1}/6: {doc.RootElement.GetProperty("ProcessToIdleMs").GetDouble():0} ms até dispatcher ocioso.");
        }
        string source=Path.Combine(run,"synthetic-720p-45s.mp4");
        await VideoEngine.ExecuteAsync(ffmpeg,["-y","-hide_banner","-f","lavfi","-i","testsrc2=size=1280x720:rate=25:duration=45","-f","lavfi","-i","sine=frequency=440:duration=45","-c:v","libx264","-preset","ultrafast","-crf","12","-c:a","aac","-shortest",source],null,default);
        var engine=new VideoEngine(ffmpeg,ffprobe,new RecoveryJournal(Path.Combine(run,"recovery")));
        var info=await engine.ProbeAsync(source,default);
        string sourceHash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source)));
        var scenarios=new[]{
            ("H264-copy-audio-two-pass",new VideoOptions(Operation.Compress,info.Bytes/1_000_000d*.65,TwoPass:true)),
            ("Split-3-precise",new VideoOptions(Operation.Split,SplitMode:SplitMode.Parts,SplitValue:3)),
            ("VP8-Opus-two-pass",new VideoOptions(Operation.Webm,299,"vp8",TwoPass:true))};
        var media=new List<object>();
        foreach(var (name,options) in scenarios)
        {
            for(int repeat=1;repeat<=2;repeat++)
            {
                Console.WriteLine($"Medindo {name}, execução {repeat}/2…");
                var watch=Stopwatch.StartNew();
                var outputs=await engine.ProcessAsync(source,options,new SilentProgress(),default);
                watch.Stop();
                long bytes=outputs.Sum(path=>new FileInfo(path).Length);
                foreach(string path in outputs)await engine.ProbeAsync(path,default);
                if(options.Operation!=Operation.Split && bytes>VideoSizing.TargetMb(info,options)*1_000_000)throw new Exception("Saída excedeu limite");
                media.Add(new {Scenario=name,Repeat=repeat,Options=options,Seconds=watch.Elapsed.TotalSeconds,RealtimeFactor=info.Duration/watch.Elapsed.TotalSeconds,OutputBytes=bytes,Files=outputs.Count});
                Console.WriteLine($"{name}: {watch.Elapsed.TotalSeconds:0.00}s; {bytes/1_000_000d:0.00} MB; {outputs.Count} arquivo(s).");
            }
        }
        if(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source)))!=sourceHash)throw new Exception("Original do benchmark alterado");
        string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        string report=Path.Combine(run,"benchmark.json");
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new
        {
            RecordedAt=DateTimeOffset.Now,Version=typeof(VideoEngine).Assembly.GetName().Version!.ToString(),
            OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription,Architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,LogicalProcessors=Environment.ProcessorCount,
            Cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
            Ffmpeg=await MediaToolUpdater.InstalledVersionAsync(ffmpeg,default),Ffprobe=await MediaToolUpdater.InstalledVersionAsync(ffprobe,default),FfmpegSha256=Hash(ffmpeg),FfprobeSha256=Hash(ffprobe),
            Source=new {info.Duration,info.Bytes,info.FrameRate,info.AudioKbps,Sha256=sourceHash,Width=1280,Height=720},
            Startup=startup,Media=media,
            Limitations=new[]{"Startup uses a framework-dependent test host and isolated real WPF window, not the installed self-contained application.","First sample is first observed launch, not a cold-cache guarantee. Remaining samples are repeated launches.","Memory describes the test host at idle, not FFmpeg or whole-system memory.","Synthetic 45s 720p media does not represent hearing recordings or GPU encoding. CPU-only encoder presets as recorded.","Media wall time excludes fixture generation and post-run probe verification, but includes engine preflight, encoding, output validation and cleanup.","Two samples per operation provide a baseline, not a statistically robust performance guarantee."}
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Benchmark completo: "+report);
    }
    private sealed class SilentProgress:IProgress<VideoProgress>{public void Report(VideoProgress value){}}
}
