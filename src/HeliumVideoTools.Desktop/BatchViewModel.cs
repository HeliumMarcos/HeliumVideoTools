using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Desktop;
public sealed record InputVideo(string Path)
{
    public string Name
    {
        get
        {
            try { return $"{System.IO.Path.GetFileName(Path)}   •   {new FileInfo(Path).Length / 1000000d:0.0} MB"; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return System.IO.Path.GetFileName(Path) + " • arquivo indisponível"; }
        }
    }
}
public sealed class BatchViewModel : INotifyPropertyChanged
{
    private readonly Func<IVideoProcessor>? processorFactory;
    private readonly Func<CancellationToken,Task> validateTools;
    private readonly string reportDirectory;
    public BatchViewModel() : this(null,MediaToolUpdater.ValidateActiveAsync,System.IO.Path.Combine(AppLog.Root,"reports")) { }
    public BatchViewModel(Func<IVideoProcessor>? processorFactory,Func<CancellationToken,Task> validateTools,string reportDirectory)
    { this.processorFactory=processorFactory; this.validateTools=validateTools; this.reportDirectory=reportDirectory; }
    public ObservableCollection<InputVideo> Files { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool Busy { get; private set; }
    public string? LastOutputFolder { get; private set; }
    public BatchResult? LastResult { get; private set; }
    public string? LastReportPath { get; private set; }
    public string? ReportWarning { get; private set; }
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
    public async Task<BatchResult> RunAsync(VideoOptions options, IProgress<VideoProgress> progress)
    {
        if (Busy) throw new InvalidOperationException("Já existe um processamento em andamento.");
        LastResult = null; LastReportPath = null; LastOutputFolder = null; ReportWarning = null;
        var sources = Files.Select(f => f.Path).ToArray();
        if (sources.Length == 0) throw new IOException("Adicione pelo menos um vídeo.");
        var engine = processorFactory?.Invoke() ?? new VideoEngine(FindTool("ffmpeg") ?? throw new IOException("FFmpeg ausente. Coloque o conjunto de ferramentas na pasta tools."), FindTool("ffprobe") ?? throw new IOException("ffprobe ausente. Coloque o conjunto de ferramentas na pasta tools."));
        Busy = true; LastOutputFolder = null; PropertyChanged?.Invoke(this, new(nameof(Busy)));
        using var cancel = new CancellationTokenSource(); cancellation = cancel;
        try
        {
            await validateTools(cancel.Token);
            LastResult = await new BatchRunner(engine).RunAsync(sources, options, progress, cancel.Token);
            LastOutputFolder = LastResult.OutputFolders.LastOrDefault();
            try
            {
                string report = await BatchReportStore.SaveAsync(LastResult,reportDirectory);
                LastReportPath = System.IO.Path.ChangeExtension(report,".txt");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { ReportWarning = "Não foi possível salvar o relatório. As saídas concluídas foram preservadas."; AppLog.Write("WARNING","Relatório do lote",ex.Message); }
            return LastResult;
        }
        finally { cancellation = null; Busy = false; PropertyChanged?.Invoke(this, new(nameof(Busy))); }
    }
}
