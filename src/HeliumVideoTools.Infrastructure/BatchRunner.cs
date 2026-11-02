using System.Diagnostics;
using System.Text.Json;

using HeliumVideoTools.Core;

namespace HeliumVideoTools.Infrastructure;

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

