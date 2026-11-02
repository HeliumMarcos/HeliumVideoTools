using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using HeliumVideoTools.Core;

internal static class ToolInterruptionTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    internal static async Task WorkerAsync(string root,string archive,string metadata,string stage,string ready)
    {
        var asset=JsonSerializer.Deserialize<ReleaseAsset>(File.ReadAllText(metadata))!;
        void Hold(string actual)
        {
            if(actual!=stage)return;
            File.WriteAllText(ready+".tmp",actual);File.Move(ready+".tmp",ready);
            Thread.Sleep(Timeout.Infinite);
        }
        using var http=new HttpClient(new ArchiveHandler(archive));
        var store=new MediaToolStore(root){Checkpoint=Hold};
        var updater=new MediaToolUpdater(new ReleaseClient(http),store){Checkpoint=Hold};
        await updater.InstallAsync(asset,new TransferProgress(value=>{if(value>0 && value<100)Hold("downloading");}),default);
        throw new Exception("Worker não parou no checkpoint esperado");
    }
    internal static async Task RunAsync(string folder,string archive,ReleaseAsset asset,string ffmpeg,string ffprobe)
    {
        string hash1,hash2;
        await using(var file=File.OpenRead(ffmpeg))hash1=Convert.ToHexString(await SHA256.HashDataAsync(file));
        await using(var file=File.OpenRead(ffprobe))hash2=Convert.ToHexString(await SHA256.HashDataAsync(file));
        foreach(string test in new[]{"downloading","downloaded","extracted-ffmpeg.exe","backup-written","activated","activated-corrupt","activated-pointer","bundled-pointer"})
        {
            string caseFolder=Path.Combine(folder,"tool-crash-"+Guid.NewGuid().ToString("N")),root=Path.Combine(caseFolder,"tools");Directory.CreateDirectory(root);
            var old=new ToolSet(asset.Version,Guid.NewGuid().ToString("N"),hash1,hash2);
            bool bundled=test=="bundled-pointer";
            if(!bundled)
            {
                string oldDirectory=Path.Combine(root,old.Id);Directory.CreateDirectory(oldDirectory);
                File.Copy(ffmpeg,Path.Combine(oldDirectory,"ffmpeg.exe"));File.Copy(ffprobe,Path.Combine(oldDirectory,"ffprobe.exe"));
                File.WriteAllText(Path.Combine(root,"active.json"),JsonSerializer.Serialize(old));
            }
            string foreign=Path.Combine(root,"unregistered.partial");File.WriteAllText(foreign,"preserve");
            string metadata=Path.Combine(caseFolder,"asset.json"),ready=Path.Combine(caseFolder,"ready.txt");File.WriteAllText(metadata,JsonSerializer.Serialize(asset));
            string stage=test.StartsWith("activated-") || bundled?"activated":test;
            var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
            if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(ToolInterruptionTests).Assembly.Location);
            foreach(string arg in new[]{"--tool-update-worker",root,archive,metadata,stage,ready})start.ArgumentList.Add(arg);
            using(var worker=Process.Start(start)??throw new Exception("Worker não iniciou"))
            {
                try
                {
                    var deadline=DateTime.UtcNow.AddSeconds(60);
                    while(!File.Exists(ready) && !worker.HasExited && DateTime.UtcNow<deadline)await Task.Delay(50);
                    Check(File.Exists(ready),"Checkpoint não atingido: "+test+" "+(worker.HasExited?await worker.StandardError.ReadToEndAsync():"timeout"));
                    var store=new MediaToolStore(root);
                    var live=await store.RecoverAsync();Check(live.Cleaned==0,"Limpou transação ainda ativa: "+test);
                    worker.Kill();await worker.WaitForExitAsync();
                    if(test=="activated-corrupt")File.AppendAllText(store.Resolve(store.Active()!,"ffprobe"),"damaged");
                    if(test=="activated-pointer" || bundled)File.WriteAllText(Path.Combine(root,"active.json"),"{");
                    var recovered=await store.RecoverAsync();
                    if(recovered.Cleaned!=1)
                    {
                        string log=Path.Combine(AppLog.Root,"app.jsonl");
                        if(File.Exists(log))foreach(string line in File.ReadLines(log).TakeLast(30))
                        {
                            using var entry=JsonDocument.Parse(line);
                            if(entry.RootElement.TryGetProperty("detail",out var detail) && detail.GetString()?.Contains(root,StringComparison.OrdinalIgnoreCase)==true)Console.Error.WriteLine(line);
                        }
                    }
                    Check(recovered.Cleaned==1,"Não recuperou transação: "+test+" "+JsonSerializer.Serialize(recovered));
                    var active=store.Active();
                    if(bundled)Check(active is null,"Não retornou ao conjunto embutido");
                    else
                    {
                        Check(active is not null,"Perdeu conjunto ativo: "+test);await store.ValidateAsync(active!,default);
                        Check(test=="activated"?active!.Id!=old.Id:active==old,"Conjunto incorreto após interrupção: "+test);
                    }
                    Check(File.ReadAllText(foreign)=="preserve","Arquivo não registrado foi alterado");
                    Check(Directory.GetFiles(root,"*.zip*").Length==0,"Download incompleto permaneceu");
                    Check(Directory.GetFiles(Path.Combine(root,"transactions"),"*.json").Length==0,"Transação já recuperada permaneceu");
                    int sets=Directory.GetDirectories(root).Count(path=>Regex.IsMatch(Path.GetFileName(path),"^[a-f0-9]{32}$"));
                    Check(sets==(bundled?0:test=="activated"?2:1),"Conjunto incompleto ou conjunto válido removido: "+test);
                    Check((await store.RecoverAsync()).Cleaned==0,"Recuperação não foi idempotente");
                }
                finally {if(!worker.HasExited){worker.Kill(true);await worker.WaitForExitAsync();}}
            }
            // Only this explicitly verified isolated fixture is recursively removed.
            Check(Path.GetFullPath(caseFolder).StartsWith(Path.GetFullPath(folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Raiz de limpeza inválida");
            Directory.Delete(caseFolder,true);
            Console.WriteLine("PASS: interrupção real da atualização de ferramentas em "+test+"; conjunto utilizável e dados externos preservados.");
        }
    }
    private sealed class ArchiveHandler(string path):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {token.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StreamContent(File.OpenRead(path))});}
    }
    private sealed class TransferProgress(Action<double> report):IProgress<double>{public void Report(double value)=>report(value);}
}
