using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using HeliumVideoTools.Core;

internal static class ReleaseTests
{
    public static async Task RunAsync(string folder)
    {
        if (MediaToolUpdater.ParseInstalledVersion("ffmpeg version 9.0.2-essentials_build-www.gyan.dev Copyright (c) 2000-2026") != new Version(9,0,2)) throw new Exception("Versão FFmpeg completa não foi resumida corretamente");
        if (MediaToolUpdater.ParseInstalledVersion("ffprobe version 9.1 Copyright") != new Version(9,1)) throw new Exception("Versão ffprobe sem patch não reconhecida");
        if (MediaToolUpdater.ParseInstalledVersion(null) is not null || MediaToolUpdater.ParseInstalledVersion("ausente") is not null) throw new Exception("Versão ausente inventada");
        byte[] payload = [77, 90, 1, 2, 3, 4];
        string hash = Convert.ToHexString(SHA256.HashData(payload));
        var asset = new ReleaseAsset("3.1.0", "HeliumVideoTools-3.1.0-win-x64-Setup.exe", new("https://api.github.com/repos/HeliumMarcos/HeliumVideoTools/releases/assets/1"), payload.Length, hash);
        using var client = new HttpClient(new Stub(request =>
        {
            if (request.Headers.Authorization is not null) throw new Exception("Atualização pública enviou credencial");
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
        }));
        var releases = new ReleaseClient(client);
        string output = Path.Combine(folder, "setup.exe");
        await releases.DownloadAsync(asset, output, null, default);
        using (ReleaseClient.VerifyInstaller(output, asset)) { }
        var invalid = asset with { Sha256 = new string('0', 64) };
        try { await releases.DownloadAsync(invalid, Path.Combine(folder, "invalid.exe"), null, default); throw new Exception("Download com hash inválido aceito"); } catch (IOException) { }
        if (File.Exists(Path.Combine(folder, "invalid.exe")) || Directory.GetFiles(folder, "*.partial").Length != 0) throw new Exception("Download inválido foi publicado ou deixou temporário");
        using var redirect = new HttpClient(new Stub(_ => new(HttpStatusCode.Redirect) { Headers = { Location = new("https://outro.exemplo/setup.exe") } }));
        try { await new ReleaseClient(redirect).DownloadAsync(asset, Path.Combine(folder, "redirect.exe"), null, default); throw new Exception("Origem externa aceita"); } catch (IOException) { }
        using var current = new HttpClient(new Stub(_ => new(HttpStatusCode.OK) { Content = new StringContent("{\"tag_name\":\"v3.0.0\",\"draft\":false,\"prerelease\":false}") }));
        if (await new ReleaseClient(current).CheckAppAsync(new(3, 0, 0), default) is not null) throw new Exception("Versão igual apareceu como atualização");
        using var unauthorized = new HttpClient(new Stub(_ => new(HttpStatusCode.Forbidden)));
        try { await new ReleaseClient(unauthorized).CheckAppAsync(new(3, 0, 0), default); throw new Exception("HTTP 403 tratado como atualizado"); } catch (IOException) { }
        Console.WriteLine("PASS: atualização pública sem credencial, integridade, instalador verificado, origem, versão igual e HTTP 403.");
    }
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(response(request));
    }
}
