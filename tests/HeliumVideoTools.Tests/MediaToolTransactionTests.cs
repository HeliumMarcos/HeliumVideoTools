using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;

internal static class MediaToolTransactionTests
{
    private static void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
    private static async Task Reject(Func<Task> action)
    {try {await action();}catch(Exception ex) when(ex is IOException or OperationCanceledException){return;}throw new Exception("Operação inválida foi aceita");}

    public static async Task RunAsync(string folder,string ffmpeg,string ffprobe)
    {
        string root=Path.Combine(folder,"tool-profile"),zipPath=Path.Combine(folder,"tools-fixture.zip");
        string version=MediaToolUpdater.ParseInstalledVersion(await MediaToolUpdater.InstalledVersionAsync(ffmpeg,default))!.ToString();
        using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(ffmpeg,"package/bin/ffmpeg.exe",CompressionLevel.Fastest);
            zip.CreateEntryFromFile(ffprobe,"package/bin/ffprobe.exe",CompressionLevel.Fastest);
            using var writer=new StreamWriter(zip.CreateEntry("package/LICENSE").Open());writer.Write("Isolated test fixture; binaries retain their repository license.");
        }
        string hash;
        await using(var file=File.OpenRead(zipPath))hash=Convert.ToHexString(await SHA256.HashDataAsync(file));
        var asset=new ReleaseAsset(version,$"ffmpeg-{version}-essentials_build.zip",new("https://api.github.com/repos/GyanD/codexffmpeg/releases/assets/1"),new FileInfo(zipPath).Length,hash);
        using var http=new HttpClient(new ArchiveHandler(zipPath));
        var store=new MediaToolStore(root);var updater=new MediaToolUpdater(new ReleaseClient(http),store);
        Check(store.Active() is null,"Perfil isolado não começou vazio");
        await updater.InstallAsync(asset,null,default);
        var first=store.Active()!;await store.ValidateAsync(first,default);
        Check(first.Version==version,"Conjunto não ativado");
        await store.RestoreAsync(default);
        Check(store.Active() is null,"Primeira restauração não retornou às ferramentas embutidas");
        await updater.InstallAsync(asset,null,default);var second=store.Active()!;
        await updater.InstallAsync(asset,null,default);var third=store.Active()!;
        Check(second.Id!=third.Id,"Diretório de ferramentas foi reutilizado");
        await store.ValidateAsync(second,default); // a captured running job remains usable
        await store.RestoreAsync(default);Check(store.Active()==second,"Restauração não voltou ao conjunto anterior");
        string pointer=Path.Combine(root,"active.json"),previous=Path.Combine(root,"previous.json");
        string saved=File.ReadAllText(pointer),recovery=File.ReadAllText(previous);
        await Reject(()=>updater.InstallAsync(asset with {Sha256=new string('0',64)},null,default));
        Check(File.ReadAllText(pointer)==saved,"Hash inválido alterou o ativo");
        await Reject(()=>updater.InstallAsync(asset with {Version=version+"0",Name=$"ffmpeg-{version}0-essentials_build.zip"},null,default));
        Check(File.ReadAllText(pointer)==saved,"Versão divergente alterou o ativo");
        using(var cancellation=new CancellationTokenSource())
        {
            await Reject(()=>updater.InstallAsync(asset,new CancelAtDownloadEnd(cancellation),cancellation.Token));
            Check(cancellation.IsCancellationRequested,"Teste não chegou ao ponto de interrupção");
        }
        Check(File.ReadAllText(pointer)==saved && File.ReadAllText(previous)==recovery,"Cancelamento alterou os ponteiros");
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();
            await Reject(()=>store.ActivateAsync(third,cancellation.Token));
            await Reject(()=>store.RestoreAsync(cancellation.Token));
        }
        Check(File.ReadAllText(pointer)==saved && File.ReadAllText(previous)==recovery,"Ativação/restauração cancelada alterou ponteiros");
        string incomplete=Path.Combine(folder,"tools-incomplete.zip");
        using(var zip=ZipFile.Open(incomplete,ZipArchiveMode.Create))
        { using var writer=new StreamWriter(zip.CreateEntry("package/LICENSE").Open());writer.Write("Missing binaries"); }
        string incompleteHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(incomplete)));
        using(var badHttp=new HttpClient(new ArchiveHandler(incomplete)))
            await Reject(()=>new MediaToolUpdater(new ReleaseClient(badHttp),store).InstallAsync(asset with {Size=new FileInfo(incomplete).Length,Sha256=incompleteHash},null,default));
        Check(File.ReadAllText(pointer)==saved,"Pacote incompleto alterou o ativo");
        // Simulate an interrupted activation and an untrusted metadata file.
        File.WriteAllText(Path.Combine(root,"active.json.interrupted.tmp"),"{");
        Check(store.Active()==second,"Temporário interrompido substituiu o ativo");
        File.WriteAllText(pointer,"{");
        await Reject(()=>Task.Run(()=>store.Active()));
        await updater.InstallAsync(asset,null,default);
        Check(store.Active() is not null && File.ReadAllText(previous)==recovery,"Reparo perdeu o ponto de restauração");
        await store.RestoreAsync(default);Check(store.Active()==second,"Reparo impediu restauração");
        // Never activate a damaged recovery target.
        File.WriteAllText(previous,JsonSerializer.Serialize(first));
        File.AppendAllText(store.Resolve(first,"ffprobe"),"tampered");
        await Reject(()=>store.RestoreAsync(default));
        Check(store.Active()==second,"Backup inválido substituiu o ativo");
        File.WriteAllText(pointer,JsonSerializer.Serialize(second with {Id="../escape"}));
        await Reject(()=>Task.Run(()=>store.Active()));
        File.WriteAllText(pointer,saved);
        Check(Directory.GetFiles(root,"*.zip").Length==0 && Directory.GetFiles(root,"*.partial").Length==0,"Download deixou temporários");
        Check(Directory.GetDirectories(root).Count(path=>Path.GetFileName(path)!="transactions")==4,"Falha ou cancelamento deixou conjunto incompleto");
        await ToolInterruptionTests.RunAsync(folder,zipPath,asset,ffmpeg,ffprobe);
        Directory.Delete(root,true);File.Delete(zipPath);File.Delete(incomplete);
        Console.WriteLine("PASS: transação FFmpeg/ffprobe isolada, executáveis reais, hash/versão, cancelamento, snapshot antigo, restauração embutida/anterior, controle corrompido e backup adulterado.");
    }
    private sealed class ArchiveHandler(string path):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(request.Headers.Authorization is not null)throw new Exception("Ferramentas enviaram credencial");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StreamContent(File.OpenRead(path))});
        }
    }
    private sealed class CancelAtDownloadEnd(CancellationTokenSource cancellation):IProgress<double>
    {public void Report(double value){if(value>=100)cancellation.Cancel();}}
}
