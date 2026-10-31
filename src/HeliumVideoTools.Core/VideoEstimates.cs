namespace HeliumVideoTools.Core;

public static class VideoSizing
{
    public static double TargetMb(MediaInfo info, VideoOptions options)
    {
        double original = info.Bytes / 1000000d;
        return options.Operation == Operation.Webm ? Math.Min(options.TargetMb, original * .98) : options.Preset switch
        { "low" => original * .75, "medium" => original * .55, "high" => original * .35, _ => options.TargetMb };
    }
}
public sealed record OutputEstimate(double? CapMb = null, double? BudgetMb = null, double? ReferencePartMb = null, int? Parts = null, double? PartSeconds = null, string? Warning = null);
public sealed record EstimateEntry(string Source, OutputEstimate? Estimate, string? Error = null);

public static class VideoEstimates
{
    public static OutputEstimate Calculate(MediaInfo info, VideoOptions options)
    {
        if (info.Bytes <= 0 || !double.IsFinite(info.Duration) || info.Duration <= 0) throw new IOException("Metadados insuficientes para estimar.");
        double original = info.Bytes / 1000000d;
        if (options.Operation == Operation.Split)
        {
            double value = options.SplitValue;
            if (!double.IsFinite(value) || value <= 0) throw new IOException("Informe um valor de divisão válido.");
            if (options.SplitMode == SplitMode.Size) return new(CapMb: value);
            double seconds = options.SplitMode == SplitMode.Parts ? info.Duration / value : value * 60;
            if (options.SplitMode == SplitMode.Parts && (value < 2 || value > 10 || value % 1 != 0)) throw new IOException("Escolha de 2 a 10 partes.");
            if (seconds < 1 || Math.Ceiling(info.Duration / seconds) > 10000) throw new IOException("Intervalo de divisão inválido.");
            int count = options.SplitMode == SplitMode.Parts ? (int)value : (int)Math.Ceiling(info.Duration / seconds);
            return new(ReferencePartMb: original * Math.Min(seconds,info.Duration) / info.Duration, Parts: count, PartSeconds: Math.Min(seconds,info.Duration));
        }
        double target = VideoSizing.TargetMb(info,options);
        if (!double.IsFinite(target) || target <= 0) throw new IOException("Informe um tamanho válido.");
        if (options.Operation == Operation.Compress && target >= original)
            return new(CapMb: target, Warning: "O original já está abaixo do alvo; escolha um tamanho menor para compactá-lo.");
        double audio = info.AudioKbps == 0 ? 0 : options.Operation == Operation.Webm ? 128 : options.CopyAudio ? info.AudioKbps : 128;
        int rate = VideoEngine.Bitrate(info.Duration,target,audio);
        return new(CapMb: target, BudgetMb: (rate + audio) * info.Duration / 8000);
    }
}

// Cache apenas em memória, invalidado por tamanho/data do arquivo. Processos
// limitados a dois e executados fora da thread WPF. Nenhum vídeo é alterado.
public sealed class VideoEstimateService(Func<string,CancellationToken,Task<MediaInfo>> probe)
{
    private sealed record Cached(long Bytes,long Modified,MediaInfo Info);
    private readonly Dictionary<string,Cached> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim concurrency = new(2);
    public Task<IReadOnlyList<EstimateEntry>> EstimateAsync(IEnumerable<string> sources, VideoOptions options, CancellationToken token)
    {
        var paths = sources.ToArray();
        return Task.Run<IReadOnlyList<EstimateEntry>>(async () => await Task.WhenAll(paths.Select(async path =>
        {
            await concurrency.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                var before = new FileInfo(path); long size = before.Length, modified = before.LastWriteTimeUtc.Ticks;
                MediaInfo? info;
                lock(cache) info = cache.TryGetValue(path,out var old) && old.Bytes == size && old.Modified == modified ? old.Info : null;
                if(info is null)
                {
                    info = await probe(path,token);
                    var after = new FileInfo(path);
                    if(after.Length != size || after.LastWriteTimeUtc.Ticks != modified) throw new IOException("O arquivo mudou durante a análise. Tente novamente.");
                    lock(cache) { if(cache.Count >= 128) cache.Remove(cache.Keys.First()); cache[path] = new(size,modified,info); }
                }
                try { return new EstimateEntry(path,VideoEstimates.Calculate(info,options)); }
                catch(IOException ex) { return new EstimateEntry(path,new OutputEstimate(Warning:ex.Message)); }
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested) { throw; }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or OperationCanceledException or System.Text.Json.JsonException or FormatException or KeyNotFoundException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            { return new EstimateEntry(path,null,ex is OperationCanceledException ? "A análise excedeu o tempo limite." : ex is IOException ? ex.Message : "Não foi possível analisar este arquivo."); }
            finally { concurrency.Release(); }
        })),token);
    }
}
