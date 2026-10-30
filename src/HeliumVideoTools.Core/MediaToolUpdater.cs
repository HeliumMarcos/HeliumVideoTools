using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HeliumVideoTools.Core;

public sealed record ToolSet(string Version, string Id, string FfmpegSha256, string FfprobeSha256);

public sealed class MediaToolUpdater(ReleaseClient releases)
{
    private static string Root => Path.Combine(AppLog.Root, "tools");
    private static string Pointer => Path.Combine(Root, "active.json");
    public static ToolSet? Active()
    {
        if (!File.Exists(Pointer)) return null;
        var active = JsonSerializer.Deserialize<ToolSet>(File.ReadAllText(Pointer)) ?? throw new IOException("Conjunto de ferramentas inválido.");
        if (!Regex.IsMatch(active.Id, "^[a-f0-9]{32}$") || !Version.TryParse(active.Version, out _)) throw new IOException("Identidade das ferramentas inválida.");
        return active;
    }
    public static string? ResolveActive(string name)
    {
        if (name is not "ffmpeg" and not "ffprobe") throw new ArgumentException("Ferramenta inválida");
        var active = Active();
        if (active is null) return null;
        string path = Path.Combine(Root, active.Id, name + ".exe");
        return File.Exists(path) ? path : throw new IOException("Ferramentas atualizadas ausentes. Restaure o conjunto anterior.");
    }
    public static async Task ValidateActiveAsync(CancellationToken token)
    {
        if (Active() is { } set) await ValidateAsync(set, token);
    }
    private static async Task ValidateAsync(ToolSet set, CancellationToken token)
    {
        if (!Regex.IsMatch(set.Id, "^[a-f0-9]{32}$")) throw new IOException("Identidade das ferramentas inválida.");
        foreach (var (name, expected) in new[] { ("ffmpeg", set.FfmpegSha256), ("ffprobe", set.FfprobeSha256) })
        {
            await using var file = File.OpenRead(Path.Combine(Root, set.Id, name + ".exe"));
            string actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("O conjunto de ferramentas foi alterado. Restaure o conjunto anterior.");
        }
    }
    public async Task<ReleaseAsset> CheckAsync(CancellationToken token)
    {
        using var doc = JsonDocument.Parse(await releases.ReadAsync(new("https://api.github.com/repos/GyanD/codexffmpeg/releases/latest"), 4_000_000, true, token));
        var root = doc.RootElement;
        var version = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(version, "^[0-9]+\\.[0-9]+(\\.[0-9]+)?$") || root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) throw new IOException("Pacote FFmpeg estável não reconhecido.");
        return ReleaseClient.ParseAsset(root, "GyanD/codexffmpeg", $"ffmpeg-{version}-essentials_build.zip");
    }
    public static async Task<string> InstalledVersionAsync(string binary, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        string output = await VideoEngine.ExecuteAsync(binary, ["-version"], null, timeout.Token);
        return output.Split('\n')[0].Trim();
    }
    public static Version? ParseInstalledVersion(string? details)
    {
        var match = Regex.Match(details ?? "", @"\bversion\s+([0-9]+\.[0-9]+(?:\.[0-9]+)?)\b", RegexOptions.CultureInvariant);
        return match.Success && Version.TryParse(match.Groups[1].Value, out var version) ? version : null;
    }
    public async Task InstallAsync(ReleaseAsset asset, IProgress<double>? progress, CancellationToken token)
    {
        if (asset.Url.Host != "api.github.com" || !Regex.IsMatch(asset.Url.AbsolutePath, "^/repos/GyanD/codexffmpeg/releases/assets/[0-9]+$") || !Regex.IsMatch(asset.Version, "^[0-9]+\\.[0-9]+(\\.[0-9]+)?$") || asset.Name != $"ffmpeg-{asset.Version}-essentials_build.zip") throw new IOException("Pacote de ferramentas não reconhecido.");
        Directory.CreateDirectory(Root);
        string id = Guid.NewGuid().ToString("N"), directory = Path.Combine(Root, id);
        string archive = Path.Combine(Root, id + ".zip");
        try
        {
            await releases.DownloadAsync(asset, archive, progress, token);
            Directory.CreateDirectory(directory);
            using (var zip = ZipFile.OpenRead(archive))
            {
                foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe", "LICENSE" })
                {
                    var entries = zip.Entries.Where(e => e.Name == name && !e.FullName.Contains("..")).ToArray();
                    if (entries.Length != 1 || entries[0].Length is <= 0 or > 500_000_000) throw new IOException("Conteúdo do pacote de ferramentas inválido.");
                    await using var input = entries[0].Open();
                    await using var output = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                    await input.CopyToAsync(output, token);
                }
            }
            foreach (var name in new[] { "ffmpeg", "ffprobe" })
            {
                var version = await InstalledVersionAsync(Path.Combine(directory, name + ".exe"), token);
                if (!version.StartsWith($"{name} version {asset.Version}", StringComparison.Ordinal)) throw new IOException("As versões de FFmpeg/ffprobe não correspondem ao pacote.");
            }
            var codecs = await VideoEngine.ExecuteAsync(Path.Combine(directory, "ffmpeg.exe"), ["-hide_banner", "-encoders"], null, token);
            foreach (var codec in new[] { "libx264", "libx265", "libvpx", "libvpx-vp9", "libopus" })
                if (!Regex.IsMatch(codecs, @"\b" + codec + @"\b")) throw new IOException("O pacote não contém todos os codificadores necessários.");
            var set = new ToolSet(asset.Version, id, await HashAsync(Path.Combine(directory, "ffmpeg.exe"), token), await HashAsync(Path.Combine(directory, "ffprobe.exe"), token));
            await ValidateAsync(set, token);
            if (Active() is { } previous) AtomicSave(Path.Combine(Root, "previous.json"), previous);
            token.ThrowIfCancellationRequested(); AtomicSave(Pointer, set);
        }
        finally { if (File.Exists(archive)) File.Delete(archive); }
        // Diretórios são imutáveis e retidos: uma operação já iniciada pode usar o conjunto antigo.
        // Só a troca atômica do ponteiro ativa o novo conjunto; falhas deixam o anterior ativo.
    }
    public static async Task RestoreAsync(CancellationToken token)
    {
        string previous = Path.Combine(Root, "previous.json");
        if (!File.Exists(previous)) throw new IOException("Nenhum conjunto anterior disponível.");
        var set = JsonSerializer.Deserialize<ToolSet>(File.ReadAllText(previous)) ?? throw new IOException("Backup inválido.");
        await ValidateAsync(set, token); AtomicSave(Pointer, set);
    }
    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var file = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(file, token));
    }
    private static void AtomicSave(string path, ToolSet set)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(set)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
