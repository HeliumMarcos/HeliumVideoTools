using System.Diagnostics;
using System.IO;
using System.Text.Json;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;

internal static class BatchRecoveryTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    internal static async Task Worker(string folder)
    {
        var store=new BatchRecoveryStore(Path.Combine(folder,"state"),Path.Combine(folder,"reports"));
        using var lease=store.Begin([Path.Combine(folder,"a.mp4"),Path.Combine(folder,"b.mp4"),Path.Combine(folder,"c.mp4")]);
        await new BatchRunner(new WorkerProcessor(folder)).RunAsync([Path.Combine(folder,"a.mp4"),Path.Combine(folder,"b.mp4"),Path.Combine(folder,"c.mp4")],new(),new Progress<VideoProgress>(),default,lease);
    }
    internal static async Task RunAsync(string parent)
    {
        string folder=Path.Combine(Path.GetFullPath(parent),"batch-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var store=new BatchRecoveryStore(Path.Combine(folder,"state"),Path.Combine(folder,"reports"));
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(BatchRecoveryTests).Assembly.Location);
        foreach(var arg in new[]{"--batch-recovery-worker",folder})start.ArgumentList.Add(arg);
        using(var worker=Process.Start(start)!)
        {
            var error=worker.StandardError.ReadToEndAsync();
            try
            {
                var deadline=DateTime.UtcNow.AddSeconds(15);
                while(!File.Exists(Path.Combine(folder,"ready")) && !worker.HasExited && DateTime.UtcNow<deadline)await Task.Delay(50);
                Check(!worker.HasExited && File.Exists(Path.Combine(folder,"ready")),"Worker não iniciou: "+(worker.HasExited?await error:"timeout"));
                Check((await store.RecoverAsync()).Count==0,"Lote ativo tratado como abandonado");
                worker.Kill(true);await worker.WaitForExitAsync();
            }
            finally{if(!worker.HasExited){worker.Kill(true);await worker.WaitForExitAsync();}}
        }
        IReadOnlyList<RecoveredBatch> recovered=[];
        for(int attempt=0;attempt<100 && recovered.Count==0;attempt++){recovered=await store.RecoverAsync();if(recovered.Count==0)await Task.Delay(50);}
        Check(recovered.Count==1,"Lote interrompido não recuperado");var batch=recovered[0];
        Check(batch.Result.Succeeded==1 && batch.Result.Cancelled && batch.Result.NotStarted==1 && batch.Result.Outputs.Count==2,"Estados/saídas parciais incorretos");
        Check(batch.Result.Items[1].State==BatchItemState.Cancelled && batch.Result.Items[1].Error!.Contains("interrompido"),"Interrupção não identificada");
        Check(File.Exists(batch.ReportPath) && File.ReadAllText(batch.ReportPath).Contains("part.mp4"),"Relatório de recuperação ausente");
        Check((await store.RecoverAsync()).Count==0,"Recuperação repetida duplicou relatório");
        Check(Directory.GetFiles(Path.Combine(folder,"reports"),"*.txt").Length==1,"Mais de um relatório para o mesmo lote");
        using(var lease=store.Begin([Path.Combine(folder,"missing-input.mp4")]))
        {lease.Start(0);lease.Published(0,Path.Combine(folder,"completed.mp4"));File.WriteAllText(Path.Combine(folder,"completed.mp4"),"changed output");}
        var changed=(await store.RecoverAsync()).Single();Check(changed.Result.Outputs.Count==0,"Saída alterada apresentada como confirmada");
        using(var lease=store.Begin([Path.Combine(folder,"source.mp4")])){lease.Start(0);lease.Finish(0,new(Path.Combine(folder,"source.mp4"),BatchItemState.Failed,[],"erro registrado"));lease.Complete();}
        Check((await store.RecoverAsync()).Count==0,"Lote normalmente encerrado recuperado de novo");
        string corrupt=Path.Combine(folder,"state",new string('a',32)+".json");File.WriteAllText(corrupt,"{invalid");
        Check((await store.RecoverAsync()).Count==0 && File.Exists(corrupt),"Metadado inválido não foi preservado");
        Check(File.ReadAllText(Path.Combine(folder,"part.mp4"))=="part complete","Recuperação alterou saída concluída");
        string blocked=Path.Combine(folder,"blocked-reports");File.WriteAllText(blocked,"preserve");
        var blockedStore=new BatchRecoveryStore(Path.Combine(folder,"blocked-state"),blocked);
        using(var lease=blockedStore.Begin([Path.Combine(folder,"source.mp4")])){lease.Start(0);}
        Check((await blockedStore.RecoverAsync()).Count==0 && Directory.GetFiles(Path.Combine(folder,"blocked-state"),"*.json").Length==1,"Falha de relatório perdeu registro");
        File.Move(blocked,blocked+".original");Directory.CreateDirectory(blocked);
        Check((await blockedStore.RecoverAsync()).Count==1 && File.ReadAllText(blocked+".original")=="preserve","Recuperação não repetiu relatório ou alterou arquivo externo");
        Console.WriteLine("PASS: morte real do worker, lease ativo, estados/saídas parciais, relatório idempotente, saída alterada, finalização normal e metadado inválido preservado.");
    }
    private sealed class WorkerProcessor(string folder):IVideoProcessor
    {
        public async Task<IReadOnlyList<string>> ProcessAsync(string source,VideoOptions options,IProgress<VideoProgress> progress,CancellationToken token,Action<string>? outputPublished=null)
        {
            string output=Path.Combine(folder,Path.GetFileName(source)=="a.mp4"?"completed.mp4":"part.mp4");
            File.WriteAllText(output,Path.GetFileName(source)=="a.mp4"?"published":"part complete");outputPublished?.Invoke(output);
            if(Path.GetFileName(source)=="a.mp4")return [output];
            File.WriteAllText(Path.Combine(folder,"ready"),"ready");await Task.Delay(Timeout.Infinite,token);return [];
        }
    }
}
