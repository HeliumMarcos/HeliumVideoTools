using System.Text.Json;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Infrastructure;

public static class BatchReportStore
{
    public static Task<string> SaveAsync(BatchResult result, string directory,string? recoveryName=null,DateTimeOffset? createdUtc=null) => Task.Run(async () =>
    {
        Directory.CreateDirectory(directory);
        if(recoveryName is not null && !System.Text.RegularExpressions.Regex.IsMatch(recoveryName,"^recuperado-[a-f0-9]{32}$"))throw new IOException("Nome de relatório inválido.");
        RecoveryJournal.CheckParents(directory);
        string path = Path.Combine(directory, recoveryName is null?$"lote-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json":recoveryName+".json");
        string temporary = path + "."+Guid.NewGuid().ToString("N")+".tmp";
        string text = Path.ChangeExtension(path, ".txt"), textTemporary = text + "."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new { CreatedUtc = createdUtc??DateTimeOffset.UtcNow, Result = result }, new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
            var lines = new List<string> { "Helium Vídeo Tools — Resultado do lote", recoveryName is not null?"Histórico recuperado após interrupção inesperada. Codificação não retomada.":result.Cancelled ? "Lote cancelado; resultados concluídos preservados." : "Processamento encerrado.", $"Concluídos: {result.Succeeded} | Com erro: {result.Failed} | Não iniciados: {result.NotStarted}", "" };
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
            File.Move(temporary, path, recoveryName is not null); File.Move(textTemporary,text,recoveryName is not null); return path;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); if (File.Exists(textTemporary)) File.Delete(textTemporary); }
    });
}
