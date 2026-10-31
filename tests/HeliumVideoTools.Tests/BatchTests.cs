using HeliumVideoTools.Core;
using HeliumVideoTools.Desktop;
using System.IO;
using System.Text.Json;

internal static class BatchTests
{
    public static async Task RunAsync(VideoEngine engine, string source, string folder)
    {
        var originalHash=System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(source));
        var progress = new Recorder();
        IProgress<VideoProgress>? late = null;
        var fake = new Fake((path, p, published, token) =>
        {
            late?.Report(new(99, "evento antigo")); late = p;
            p.Report(new(70, "etapa")); p.Report(new(10, "repetição"));
            if (path == "defeito") { published(Path.Combine(folder,"parcial.mp4")); throw new IOException("Falha simulada"); }
            if (path == "timeout") throw new OperationCanceledException("Sondagem demorou");
            published(Path.Combine(folder,path+".mp4"));
            return Task.FromResult<IReadOnlyList<string>>([Path.Combine(folder,path+".mp4")]);
        });
        var result = await new BatchRunner(fake).RunAsync(["bom", "defeito", "timeout", "outro"], new(), progress, default);
        Check(result.Succeeded == 2 && result.Failed == 2 && !result.Cancelled, "Erro individual interrompeu lote ou timeout virou cancelamento");
        Check(result.Items[1].Outputs.Count == 1 && result.Outputs.Count == 3, "Saída parcial publicada foi perdida");
        int before = progress.Values.Count; late!.Report(new(2,"evento antigo"));
        Check(progress.Values.Count == before, "Callback atrasado alterou progresso encerrado");
        Check(progress.Values.Zip(progress.Values.Skip(1)).All(p => p.Second >= p.First), "Progresso geral regrediu");
        using var cancel = new CancellationTokenSource();
        var cancelFake = new Fake((path,p,published,token) =>
        {
            published(Path.Combine(folder,path+".mp4"));
            if (path == "segundo") { cancel.Cancel(); token.ThrowIfCancellationRequested(); }
            return Task.FromResult<IReadOnlyList<string>>([Path.Combine(folder,path+".mp4")]);
        });
        var canceled = await new BatchRunner(cancelFake).RunAsync(["primeiro","segundo","terceiro"],new(),new Recorder(),cancel.Token);
        Check(canceled.Cancelled && canceled.Succeeded == 1 && canceled.NotStarted == 1 && canceled.Items[1].State == BatchItemState.Cancelled && canceled.Outputs.Count == 2,"Cancelamento perdeu resultados ou iniciou arquivo seguinte");
        string report = await BatchReportStore.SaveAsync(canceled,Path.Combine(folder,"relatórios"));
        using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(report))) Check(doc.RootElement.GetProperty("Result").GetProperty("Items").GetArrayLength() == 3,"Relatório não preservou todos os itens");
        string corrupt = Path.Combine(folder,"corrompido.mp4"); await File.WriteAllTextAsync(corrupt,"arquivo inválido para teste");
        var real = await new BatchRunner(engine).RunAsync([source,corrupt,source],new(Operation.Split,SplitValue:2),new Recorder(),default);
        Check(real.Succeeded == 2 && real.Failed == 1 && real.Outputs.Count == 4 && real.Outputs.All(File.Exists),"Lote FFmpeg real não continuou após mídia corrompida");
        using var realCancellation = new CancellationTokenSource();
        var cancelAfterPart = new Fake((path,p,published,token)=>engine.ProcessAsync(path,new(Operation.Split,SplitValue:2),p,token,output=>{published(output);realCancellation.Cancel();}));
        var partial = await new BatchRunner(cancelAfterPart).RunAsync([source,source],new(),new Recorder(),realCancellation.Token);
        Check(partial.Cancelled && partial.NotStarted == 1 && partial.Outputs.Count == 1 && File.Exists(partial.Outputs[0]),"Cancelamento FFmpeg real perdeu a parte já publicada");
        string another=Path.Combine(folder,"outra pasta"); Directory.CreateDirectory(another);
        string copy=Path.Combine(another,"audiência seguinte.mp4"); File.Copy(source,copy);
        var model=new BatchViewModel(()=>engine,_=>Task.CompletedTask,Path.Combine(folder,"relatórios-modelo"));
        model.Add([source,corrupt,copy]);
        var uiResult=await model.RunAsync(new(Operation.Split,SplitValue:2),new Recorder());
        Check(!model.Busy && uiResult.Succeeded==2 && uiResult.Failed==1 && uiResult.OutputFolders.Count==2 && model.LastResult==uiResult && File.Exists(model.LastReportPath),"ViewModel não integrou lote, pastas distintas, estado final e relatório");
        string human=await File.ReadAllTextAsync(model.LastReportPath!);
        Check(human.Contains("corrompido.mp4") && human.Contains("Saída confirmada:"),"Relatório humano omitiu arquivo com erro ou saídas");
        var finalHash=System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(source));
        Check(originalHash.SequenceEqual(finalHash),"Processamento alterou o arquivo original");
        Console.WriteLine("PASS: lotes reais válido/inválido/válido, falhas isoladas, timeout, cancelamento, saídas parciais, relatório e progresso monotônico sem callbacks atrasados.");
    }
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    private sealed class Recorder : IProgress<VideoProgress> { public List<double> Values {get;}=[]; public void Report(VideoProgress p)=>Values.Add(p.Percent); }
    private sealed class Fake(Func<string,IProgress<VideoProgress>,Action<string>,CancellationToken,Task<IReadOnlyList<string>>> run) : IVideoProcessor
    {
        public Task<IReadOnlyList<string>> ProcessAsync(string source,VideoOptions options,IProgress<VideoProgress> progress,CancellationToken token,Action<string>? outputPublished=null)=>run(source,progress,outputPublished ?? (_=>{}),token);
    }
}
