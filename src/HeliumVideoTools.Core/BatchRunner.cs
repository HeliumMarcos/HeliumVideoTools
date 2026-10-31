using System.Diagnostics;
using System.Text.Json;

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

// Independente da interface. Falhas de um arquivo não descartam os demais,
// e cada saída só é registrada depois de validada e publicada pelo motor.
public sealed class BatchRunner(IVideoProcessor processor)
{
    public async Task<BatchResult> RunAsync(IEnumerable<string> sources, VideoOptions options, IProgress<VideoProgress> progress, CancellationToken token)
    {
        var inputs = sources.ToArray();
        if (inputs.Length == 0) throw new IOException("Adicione pelo menos um vídeo.");
        var items = new List<BatchItemResult>(); bool cancelled = false;
        var timer = Stopwatch.StartNew();
        double maximum = 0; var gate = new object();
        void Report(double value, string message)
        {
            lock (gate)
            {
                maximum = Math.Max(maximum, Math.Clamp(double.IsFinite(value) ? value : 0, 0, 100));
                double remaining = maximum > 1 ? timer.Elapsed.TotalSeconds * (100 - maximum) / maximum : 0;
                progress.Report(new(maximum, message + $" • {maximum:0}%" + (remaining > 0 && remaining < 864000 ? $" • restante ≈ {TimeSpan.FromSeconds(remaining):hh\\:mm\\:ss}" : "")));
            }
        }
        for (int i = 0; i < inputs.Length; i++)
        {
            if (token.IsCancellationRequested) { cancelled = true; break; }
            var outputs = new List<string>(); int index = i; bool active = true;
            var fileProgress = new DirectProgress(p =>
            {
                lock (gate)
                    if (active) Report((index + Math.Clamp(double.IsFinite(p.Percent) ? p.Percent : 0, 0, 100) / 100) / inputs.Length * 100,
                        $"{index + 1}/{inputs.Length} • {Path.GetFileName(inputs[index])} • {p.Status}");
            });
            void Published(string path) { if (!outputs.Contains(path, StringComparer.OrdinalIgnoreCase)) outputs.Add(path); }
            try
            {
                var result = await processor.ProcessAsync(inputs[i], options, fileProgress, token, Published);
                foreach (var path in result) Published(path);
                items.Add(new(inputs[i], BatchItemState.Succeeded, outputs.ToArray()));
                AppLog.Write("INFO", "Arquivo concluído", Path.GetFileName(inputs[i]));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                items.Add(new(inputs[i], BatchItemState.Cancelled, outputs.ToArray(), "Processamento cancelado. As saídas já concluídas foram preservadas."));
                cancelled = true; break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException or ArgumentException or OverflowException or System.ComponentModel.Win32Exception)
            {
                var message = ex switch
                {
                    OperationCanceledException => "O arquivo excedeu o tempo limite de análise ou processamento.",
                    UnauthorizedAccessException => "Sem permissão para ler o arquivo ou gravar na pasta de saída.",
                    FileNotFoundException => "O arquivo não foi encontrado.",
                    IOException => ex.Message,
                    _ => "Não foi possível processar este arquivo. Os detalhes estão no log."
                };
                items.Add(new(inputs[i], BatchItemState.Failed, outputs.ToArray(), message));
                AppLog.Write("ERROR", "Falha de arquivo no lote", ex.ToString());
            }
            finally { lock (gate) active = false; }
            Report((i + 1d) / inputs.Length * 100, $"{i + 1}/{inputs.Length} arquivo(s) verificado(s)");
        }
        for (int i = items.Count; i < inputs.Length; i++) items.Add(new(inputs[i], BatchItemState.NotStarted, [], "Não iniciado: lote cancelado."));
        return new(items.ToArray(), cancelled);
    }
    private sealed class DirectProgress(Action<VideoProgress> report) : IProgress<VideoProgress>
    { public void Report(VideoProgress value) => report(value); }
}

public static class BatchReportStore
{
    public static Task<string> SaveAsync(BatchResult result, string directory) => Task.Run(async () =>
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"lote-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        string temporary = path + ".tmp";
        string text = Path.ChangeExtension(path, ".txt"), textTemporary = text + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new { CreatedUtc = DateTimeOffset.UtcNow, Result = result }, new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
            var lines = new List<string> { "Helium Vídeo Tools — Resultado do lote", result.Cancelled ? "Lote cancelado; resultados concluídos preservados." : "Processamento encerrado.", $"Concluídos: {result.Succeeded} | Com erro: {result.Failed} | Não iniciados: {result.NotStarted}", "" };
            foreach (var item in result.Items)
            {
                lines.Add(item.Source);
                lines.Add("Estado: " + (item.State switch { BatchItemState.Succeeded => "Concluído", BatchItemState.Failed => "Erro", BatchItemState.Cancelled => "Cancelado", _ => "Não iniciado" }));
                if (item.Error is not null) lines.Add(item.Error);
                foreach (var output in item.Outputs) lines.Add("Saída confirmada: " + output);
                lines.Add("");
            }
            lines.Add("Pastas com resultados:"); lines.AddRange(result.OutputFolders);
            await File.WriteAllLinesAsync(textTemporary, lines);
            File.Move(temporary, path, false); File.Move(textTemporary,text,false); return path;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); if (File.Exists(textTemporary)) File.Delete(textTemporary); }
    });
}
