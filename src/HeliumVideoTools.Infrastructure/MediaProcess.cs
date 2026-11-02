using System.Diagnostics;

namespace HeliumVideoTools.Infrastructure;

public static class MediaProcess
{
    public static async Task<string> ExecuteAsync(string binary, IEnumerable<string> args, Action<string>? line, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(binary) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var group=new OwnedProcessGroup();
        using var process = new Process { StartInfo = start };
        process.Start();
        group.Attach(process);
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception) { } });
        var error = process.StandardError.ReadToEndAsync();
        var output = new System.Text.StringBuilder();
        while (await process.StandardOutput.ReadLineAsync() is { } text) { output.AppendLine(text); line?.Invoke(text); }
        await process.WaitForExitAsync();
        var details = await error;
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
        {
            AppLog.Write("ERROR", "FFmpeg", details);
            throw new IOException(details.Contains("No space left", StringComparison.OrdinalIgnoreCase) ? "O disco ficou sem espaço." : "Não foi possível processar o vídeo. Verifique o formato, áudio e permissões da pasta. Os detalhes estão no log.");
        }
        return output.ToString();
    }
}
