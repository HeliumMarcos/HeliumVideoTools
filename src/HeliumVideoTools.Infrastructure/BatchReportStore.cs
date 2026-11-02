using System.Text.Json;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Infrastructure;

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
