using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HeliumVideoTools.Core;

// Each snapshot refers to an immutable directory. Never resolve the two binaries
// from separate reads of active.json while another window may activate a set.
public sealed class MediaToolStore(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    private string Pointer => Path.Combine(Root, "active.json");
    private string Previous => Path.Combine(Root, "previous.json");
    private readonly SemaphoreSlim mutation = new(1, 1);

    public ToolSet? Active() => File.Exists(Pointer) ? Read(Pointer, false) : null;
    private static ToolSet? Read(string path, bool allowBundled)
    {
        try
        {
            if (new FileInfo(path).Length > 4096) throw new IOException("Controle de ferramentas inválido.");
            var set = JsonSerializer.Deserialize<ToolSet>(File.ReadAllText(path));
            if (set is null && allowBundled) return null;
            if (set is null || !Regex.IsMatch(set.Id ?? "", "^[a-f0-9]{32}$") || !Version.TryParse(set.Version, out _) ||
                !Regex.IsMatch(set.FfmpegSha256 ?? "", "^[a-fA-F0-9]{64}$") || !Regex.IsMatch(set.FfprobeSha256 ?? "", "^[a-fA-F0-9]{64}$"))
                throw new IOException("Identidade das ferramentas inválida.");
            return set;
        }
        catch (JsonException ex) { throw new IOException("Controle de ferramentas danificado. Restaure ou reinstale o conjunto em Atualizações.", ex); }
    }
    public string Resolve(ToolSet set, string name)
    {
        if (name is not "ffmpeg" and not "ffprobe" || set.Id is null || !Regex.IsMatch(set.Id, "^[a-f0-9]{32}$")) throw new IOException("Ferramenta inválida.");
        string path = Path.Combine(Root, set.Id, name + ".exe");
        return File.Exists(path) ? path : throw new IOException("Ferramentas atualizadas ausentes. Restaure o conjunto anterior.");
    }
    public async Task ValidateAsync(ToolSet set, CancellationToken token)
    {
        foreach (var (name, expected) in new[] { ("ffmpeg", set.FfmpegSha256), ("ffprobe", set.FfprobeSha256) })
        {
            await using var file = File.OpenRead(Resolve(set, name));
            string actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("O conjunto de ferramentas foi alterado. Restaure o conjunto anterior.");
        }
    }
    public async Task ActivateAsync(ToolSet set, CancellationToken token)
    {
        await mutation.WaitAsync(token);
        try
        {
            await ValidateAsync(set, token);
            ToolSet? old = null;
            try { old = Active(); if (old is not null) await ValidateAsync(old, token); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                old = null;
                // Keep an existing recovery point when repairing a damaged active set.
                if (File.Exists(Previous)) { token.ThrowIfCancellationRequested(); AtomicSave(Pointer, set); return; }
            }
            token.ThrowIfCancellationRequested();
            // null is an explicit recovery point for the bundled tools. Write recovery
            // first: interruption before activation leaves the old active set usable.
            AtomicSave(Previous, old);
            AtomicSave(Pointer, set);
        }
        finally { mutation.Release(); }
    }
    public async Task RestoreAsync(CancellationToken token)
    {
        await mutation.WaitAsync(token);
        try
        {
            if (!File.Exists(Previous)) throw new IOException("Nenhum conjunto anterior disponível.");
            var set = Read(Previous, true);
            if (set is not null) await ValidateAsync(set, token);
            token.ThrowIfCancellationRequested();
            if (set is null) File.Delete(Pointer); else AtomicSave(Pointer, set);
        }
        finally { mutation.Release(); }
    }
    private static void AtomicSave(string path, ToolSet? set)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, set); file.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
