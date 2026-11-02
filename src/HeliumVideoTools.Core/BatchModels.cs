namespace HeliumVideoTools.Core;

public interface IVideoProcessor
{
    Task<IReadOnlyList<string>> ProcessAsync(string source, VideoOptions options, IProgress<VideoProgress> progress, CancellationToken token, Action<string>? outputPublished = null);
}
public enum BatchItemState { Succeeded, Failed, Cancelled, NotStarted }
public sealed record BatchItemResult(string Source, BatchItemState State, IReadOnlyList<string> Outputs, string? Error = null);
public sealed record BatchResult(IReadOnlyList<BatchItemResult> Items, bool Cancelled)
{
    public int Succeeded => Items.Count(i => i.State == BatchItemState.Succeeded);
    public int Failed => Items.Count(i => i.State == BatchItemState.Failed);
    public int NotStarted => Items.Count(i => i.State == BatchItemState.NotStarted);
    public IReadOnlyList<string> Outputs => Items.SelectMany(i => i.Outputs).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyList<string> OutputFolders => Outputs.Select(p => Path.GetDirectoryName(Path.GetFullPath(p))!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}

