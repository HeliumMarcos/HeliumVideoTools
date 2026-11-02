using System.Text.Json;

namespace HeliumVideoTools.Infrastructure;

public static class AppLog
{
    private static readonly object Gate = new();
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeliumVideoTools");
    public static void Write(string level, string message, string? detail = null)
    {
        try { lock (Gate) { Directory.CreateDirectory(Root); var path = Path.Combine(Root, "app.jsonl"); if (File.Exists(path) && new FileInfo(path).Length > 2097152) File.Move(path, path + ".1", true); File.AppendAllText(path, JsonSerializer.Serialize(new { timestamp = DateTimeOffset.Now, level, message, detail }) + Environment.NewLine); } } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }
}
