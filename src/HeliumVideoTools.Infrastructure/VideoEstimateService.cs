using HeliumVideoTools.Core;

namespace HeliumVideoTools.Infrastructure;

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
