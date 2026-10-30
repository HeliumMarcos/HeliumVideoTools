using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Desktop;
public sealed record InputVideo(string Path)
{
    public string Name => $"{System.IO.Path.GetFileName(Path)}   •   {new FileInfo(Path).Length / 1000000d:0.0} MB";
}
public sealed class BatchViewModel : INotifyPropertyChanged
{
    public ObservableCollection<InputVideo> Files { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool Busy { get; private set; }
    public string? LastOutputFolder { get; private set; }
    private CancellationTokenSource? cancellation;
    public void Add(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(p => File.Exists(p) && VideoEngine.Supported(p)))
            if (!Files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))) Files.Add(new(System.IO.Path.GetFullPath(path)));
    }
    public static string? FindTool(string name)
    {
        if (MediaToolUpdater.ResolveActive(name) is { } active) return active;
        foreach (var directory in new[] { AppContext.BaseDirectory, System.IO.Path.Combine(AppContext.BaseDirectory, "tools"), Environment.CurrentDirectory })
        { var path = System.IO.Path.Combine(directory, name + ".exe"); if (File.Exists(path)) return path; }
        return null;
    }
    public void Cancel() => cancellation?.Cancel();
    public async Task<int> RunAsync(VideoOptions options, IProgress<VideoProgress> progress)
    {
        if (Busy) return 0;
        var sources = Files.Select(f => f.Path).ToArray();
        if (sources.Length == 0) throw new IOException("Adicione pelo menos um vídeo.");
        var engine = new VideoEngine(FindTool("ffmpeg") ?? throw new IOException("FFmpeg ausente. Coloque o conjunto de ferramentas na pasta tools."), FindTool("ffprobe") ?? throw new IOException("ffprobe ausente. Coloque o conjunto de ferramentas na pasta tools."));
        Busy = true; LastOutputFolder = null; PropertyChanged?.Invoke(this, new(nameof(Busy)));
        using var cancel = new CancellationTokenSource(); cancellation = cancel;
        int completed = 0;
        var timer = Stopwatch.StartNew();
        try
        {
            await MediaToolUpdater.ValidateActiveAsync(cancel.Token);
            for (int i = 0; i < sources.Length; i++)
            {
                int index = i;
                var fileProgress = new Progress<VideoProgress>(p =>
                {
                    double percent = (index + p.Percent / 100) / sources.Length * 100;
                    double remaining = percent > 1 ? timer.Elapsed.TotalSeconds * (100 - percent) / percent : 0;
                    progress.Report(new(percent, $"{index + 1}/{sources.Length} • {System.IO.Path.GetFileName(sources[index])} • {p.Status} • {percent:0}%" + (remaining > 0 ? $" • restante ≈ {TimeSpan.FromSeconds(remaining):hh\\:mm\\:ss}" : "")));
                });
                var outputs = await engine.ProcessAsync(sources[i], options, fileProgress, cancel.Token);
                LastOutputFolder = System.IO.Path.GetDirectoryName(outputs.Last()); completed++;
                AppLog.Write("INFO", "Arquivo concluído", System.IO.Path.GetFileName(sources[i]));
            }
            return completed;
        }
        finally { cancellation = null; Busy = false; PropertyChanged?.Invoke(this, new(nameof(Busy))); }
    }
}
