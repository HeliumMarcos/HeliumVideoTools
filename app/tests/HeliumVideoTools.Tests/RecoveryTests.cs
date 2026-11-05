using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;

internal static class RecoveryTests
{
    private sealed record Signal(int Pid,string Video,string PassDirectory);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    internal static async Task WorkerAsync(string folder,string ffmpeg)
    {
        var journal=new RecoveryJournal(Path.Combine(folder,"journals"));
        using var video=journal.TrackFile(folder,".mp4");
        using var pass=journal.TrackPassDirectory(folder);Directory.CreateDirectory(pass.Path);File.WriteAllText(Path.Combine(pass.Path,"pass-0.log"),"pass stats");
        using var group=new OwnedProcessGroup();using var child=StartFfmpeg(ffmpeg,video.Path);group.Attach(child);
        var deadline=DateTime.UtcNow.AddSeconds(10);
        while((!File.Exists(video.Path) || new FileInfo(video.Path).Length==0) && !child.HasExited && DateTime.UtcNow<deadline)await Task.Delay(50);
        Check(File.Exists(video.Path) && new FileInfo(video.Path).Length>0 && !child.HasExited,"FFmpeg não iniciou a gravação do MP4 de teste");
        string signal=Path.Combine(folder,"ready.json");
        File.WriteAllText(signal+".tmp",JsonSerializer.Serialize(new Signal(child.Id,video.Path,pass.Path)));File.Move(signal+".tmp",signal);
        await child.WaitForExitAsync();
    }
    private static Process StartFfmpeg(string ffmpeg,string? output=null)
    {
        var start=new ProcessStartInfo(ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        foreach(string arg in new[]{"-y","-hide_banner","-loglevel","error","-re","-f","lavfi","-i","testsrc2=size=160x90:rate=25","-t","120"})start.ArgumentList.Add(arg);
        foreach(string arg in output is null?new[]{"-f","null","NUL"}:new[]{"-c:v","libx264","-preset","ultrafast",output})start.ArgumentList.Add(arg);
        return Process.Start(start)??throw new Exception("FFmpeg de teste não iniciou");
    }
    internal static async Task RunAsync(string parent,string ffmpeg)
    {
        string folder=Path.Combine(parent,"recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string original=Path.Combine(folder,"original.mp4"),published=Path.Combine(folder,"concluido.mp4"),foreign=Path.Combine(folder,$".hvt-{Guid.NewGuid():N}.partial.mp4");
        foreach(string path in new[]{original,published,foreign})File.WriteAllText(path,"preserve "+Path.GetFileName(path));
        var hashes=new[]{original,published,foreign}.ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(RecoveryTests).Assembly.Location);
        foreach(string arg in new[]{"--recovery-worker",folder,ffmpeg})start.ArgumentList.Add(arg);
        using var unrelated=StartFfmpeg(ffmpeg);
        using var worker=Process.Start(start)??throw new Exception("Worker não iniciou");
        Process? owned=null;
        try
        {
            string signalPath=Path.Combine(folder,"ready.json");var deadline=DateTime.UtcNow.AddSeconds(20);
            while(!File.Exists(signalPath) && !worker.HasExited && DateTime.UtcNow<deadline)await Task.Delay(50);
            Check(File.Exists(signalPath),"Worker não sinalizou: "+(worker.HasExited?await worker.StandardError.ReadToEndAsync():"timeout"));
            var signal=JsonSerializer.Deserialize<Signal>(File.ReadAllText(signalPath))!;owned=Process.GetProcessById(signal.Pid);
            var journal=new RecoveryJournal(Path.Combine(folder,"journals"));
            var active=await journal.RecoverAsync();Check(active.Cleaned==0 && File.Exists(signal.Video),"Recuperação removeu tarefa ativa");
            // Kill only the host, NOT its process tree: the job must stop its own FFmpeg.
            worker.Kill();await worker.WaitForExitAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));await owned.WaitForExitAsync(timeout.Token);
            Check(!unrelated.HasExited,"FFmpeg independente foi encerrado");
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();
                try{await journal.RecoverAsync(cancelled.Token);throw new Exception("Recuperação cancelada executou");}catch(OperationCanceledException){}
                Check(File.Exists(signal.Video),"Recuperação cancelada apagou temporário");
            }
            var result=await journal.RecoverAsync();
            Check(result.Cleaned==2 && !File.Exists(signal.Video) && !Directory.Exists(signal.PassDirectory),"Interrupção não foi recuperada");
            Check((await journal.RecoverAsync()).Cleaned==0,"Recuperação não foi idempotente");
            foreach(var (path,hash) in hashes)Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))==hash,"Original/resultado/arquivo não registrado foi alterado");
            // Unknown contents are preserved, even in a registered pass directory.
            string unexpected;
            using(var lease=journal.TrackPassDirectory(folder))
            {unexpected=lease.Path;Directory.CreateDirectory(unexpected);File.WriteAllText(Path.Combine(unexpected,"nao-apagar.txt"),"user data");}
            Check((await journal.RecoverAsync()).Deferred>0 && File.Exists(Path.Combine(unexpected,"nao-apagar.txt")),"Conteúdo desconhecido foi apagado");
            string bad=Path.Combine(folder,"journals",Guid.NewGuid().ToString("N")+".json");File.WriteAllText(bad,"{");
            Check((await journal.RecoverAsync()).Invalid==1,"JSON danificado não foi isolado");
            using(var lease=journal.TrackFile(folder,".webm"))
            {File.WriteAllText(lease.Path,"partial");string output=Path.Combine(folder,"publicado.webm");File.Move(lease.Path,output);}
            Check(File.Exists(Path.Combine(folder,"publicado.webm")),"Dispose removeu resultado publicado");
            Console.WriteLine("PASS: encerramento real do host sem matar árvore, FFmpeg próprio encerrado/independente preservado, recuperação bloqueada durante tarefa ativa, temporários pós-crash removidos e arquivos concluídos/originais/desconhecidos intactos.");
        }
        finally
        {
            if(!worker.HasExited){worker.Kill(true);await worker.WaitForExitAsync();}
            if(owned is not null){if(!owned.HasExited){owned.Kill(true);await owned.WaitForExitAsync();}owned.Dispose();}
            if(!unrelated.HasExited){unrelated.Kill(true);await unrelated.WaitForExitAsync();}
        }
    }
}
