using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HeliumVideoTools.Core;

public sealed record ReleaseAsset(string Version, string Name, Uri Url, long Size, string Sha256);
public sealed record AppRelease(Version Version, string Notes, ReleaseAsset Installer);

public sealed class ReleaseClient(HttpClient client)
{
    public static HttpClient CreateHttpClient() => new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(20) };
    private static bool Allowed(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Host is "api.github.com" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
    private async Task<HttpResponseMessage> OpenAsync(Uri uri, bool json, CancellationToken token)
    {
        for (int redirects = 0; redirects < 5; redirects++)
        {
            if (!Allowed(uri)) throw new IOException("Origem de atualização não reconhecida.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("HeliumVideoTools/" + typeof(ReleaseClient).Assembly.GetName().Version!.ToString(3));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(json ? "application/vnd.github+json" : "application/octet-stream"));
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect or HttpStatusCode.SeeOther)
            {
                var location = response.Headers.Location; response.Dispose();
                uri = location is not null ? new Uri(uri, location) : throw new IOException("Redirecionamento sem destino.");
                continue;
            }
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                throw new IOException("Nenhuma release estável acessível. Ainda pode não haver uma versão estável publicada, ou o GitHub pode estar limitando as consultas. Tente novamente mais tarde.");
            }
            try { response.EnsureSuccessStatusCode(); return response; } catch { response.Dispose(); throw; }
        }
        throw new IOException("Redirecionamentos demais na atualização.");
    }
    public async Task<byte[]> ReadAsync(Uri uri, long limit, bool json, CancellationToken token)
    {
        using var response = await OpenAsync(uri, json, token);
        if (response.Content.Headers.ContentLength > limit) throw new IOException("Resposta de atualização grande demais.");
        await using var source = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream(); var buffer = new byte[81920]; int count;
        while ((count = await source.ReadAsync(buffer, token)) > 0) { if (memory.Length + count > limit) throw new IOException("Resposta de atualização grande demais."); await memory.WriteAsync(buffer.AsMemory(0, count), token); }
        return memory.ToArray();
    }
    public static ReleaseAsset ParseAsset(JsonElement root, string repository, string name)
    {
        string version = root.GetProperty("tag_name").GetString()!;
        var asset = root.GetProperty("assets").EnumerateArray().Single(a => a.GetProperty("name").GetString() == name);
        long id = asset.GetProperty("id").GetInt64();
        var url = new Uri($"https://api.github.com/repos/{repository}/releases/assets/{id}");
        if (id <= 0 || asset.GetProperty("url").GetString() != url.AbsoluteUri || asset.GetProperty("browser_download_url").GetString() != $"https://github.com/{repository}/releases/download/{version}/{name}") throw new IOException("Arquivo não pertence à release esperada.");
        var digest = asset.GetProperty("digest").GetString() ?? "";
        if (!Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$")) throw new IOException("Release sem SHA-256 válido.");
        long size = asset.GetProperty("size").GetInt64();
        if (size <= 0 || size > 2_000_000_000) throw new IOException("Tamanho de pacote inválido.");
        return new(version.TrimStart('v'), name, url, size, digest[7..].ToLowerInvariant());
    }
    public async Task DownloadAsync(ReleaseAsset asset, string destination, IProgress<double>? progress, CancellationToken token)
    {
        if (asset.Size <= 0 || asset.Size > 2_000_000_000 || !Regex.IsMatch(asset.Sha256, "^[a-fA-F0-9]{64}$")) throw new IOException("Pacote inválido.");
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        try
        {
            using var response = await OpenAsync(asset.Url, false, token);
            if (response.Content.Headers.ContentLength is { } announced && announced != asset.Size) throw new IOException("Tamanho anunciado divergente.");
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920]; long size = 0; int count;
                while ((count = await source.ReadAsync(buffer, token)) > 0)
                {
                    size += count; if (size > asset.Size) throw new IOException("Arquivo maior que o esperado.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token); hash.AppendData(buffer, 0, count); progress?.Report(size * 100d / asset.Size);
                }
                if (size != asset.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Download incompleto ou SHA-256 divergente. Nada foi instalado.");
            }
            token.ThrowIfCancellationRequested(); File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public async Task<AppRelease?> CheckAppAsync(Version installed, CancellationToken token)
    {
        const string repo = "HeliumMarcos/HeliumVideoTools";
        using var doc = JsonDocument.Parse(await ReadAsync(new($"https://api.github.com/repos/{repo}/releases/latest"), 4_000_000, true, token));
        var root = doc.RootElement; var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean() || !Regex.IsMatch(tag, "^v[0-9]+\\.[0-9]+\\.[0-9]+$")) throw new IOException("Release estável não reconhecida.");
        var version = Version.Parse(tag[1..]); if (version <= installed) return null;
        var setup = ParseAsset(root, repo, $"HeliumVideoTools-{version.ToString(3)}-win-x64-Setup.exe");
        var sums = ParseAsset(root, repo, "SHA256SUMS.txt");
        var data = await ReadAsync(sums.Url, 1_000_000, false, token);
        if (data.Length != sums.Size || !Convert.ToHexString(SHA256.HashData(data)).Equals(sums.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Manifesto de integridade inválido.");
        var match = Regex.Match(System.Text.Encoding.UTF8.GetString(data), @"(?m)^([a-fA-F0-9]{64})\s+\*?" + Regex.Escape(setup.Name) + @"\r?$");
        if (!match.Success || !match.Groups[1].Value.Equals(setup.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Hashes da release divergentes.");
        return new(version, root.TryGetProperty("body", out var notes) ? notes.GetString() ?? "" : "", setup);
    }
    public static FileStream VerifyInstaller(string path, ReleaseAsset asset)
    {
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (!asset.Name.EndsWith("-Setup.exe", StringComparison.Ordinal) || file.Length != asset.Size || file.ReadByte() != 'M' || file.ReadByte() != 'Z') throw new IOException("Instalador inválido.");
            file.Position = 0; if (!Convert.ToHexString(SHA256.HashData(file)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Instalador alterado depois do download.");
            file.Position = 0; return file;
        }
        catch { file.Dispose(); throw; }
    }
}
