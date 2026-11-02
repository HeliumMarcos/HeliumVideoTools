using System.IO;
using HeliumVideoTools.Core;
using HeliumVideoTools.Desktop;

internal static class EstimateCoordinatorTests
{
    private static void Check(bool condition,string message)
    {if(!condition)throw new Exception(message);}
    private static TaskCompletionSource<IReadOnlyList<EstimateEntry>> Gate()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static IReadOnlyList<EstimateEntry> Entries(string path,double cap)=>[new(path,new(CapMb:cap,BudgetMb:cap*.94))];

    internal static async Task RunAsync()
    {
        // The fake transport deliberately ignores cancellation, as a process or
        // external callback may finish after a newer request has replaced it.
        foreach(bool oldFails in new[]{false,true})
        {
            var old=Gate();var latest=Gate();var oldStarted=new TaskCompletionSource<CancellationToken>();
            var seen=new List<EstimatePresentation>();var errors=new List<Exception>();
            var coordinator=new EstimateCoordinator((paths,_,token)=>
            {
                if(paths[0]=="old"){oldStarted.SetResult(token);return old.Task;}
                return latest.Task;
            },_=>Task.CompletedTask);
            var first=coordinator.RequestAsync(["old"],()=>new(Operation.Compress,10),()=>{},seen.Add,errors.Add,default);
            var oldToken=await oldStarted.Task;
            var second=coordinator.RequestAsync(["latest"],()=>new(Operation.Webm,20),()=>{},seen.Add,errors.Add,default);
            Check(oldToken.IsCancellationRequested,"A análise substituída continuou sem cancelamento");
            if(oldFails)old.SetException(new IOException("resposta antiga"));else old.SetResult(Entries("old",10));
            await first;
            Check(seen.Count==0 && errors.Count==0,"Resposta antiga substituiu estado da análise atual");
            latest.SetResult(Entries("latest",20));await second;
            Check(seen.Count==1 && seen[0].Details.Contains("latest") && errors.Count==0,"A estimativa mais recente não foi apresentada");
        }

        int probes=0,completed=0,failures=0;
        var waiting=new TaskCompletionSource();
        var debounce=new EstimateCoordinator((_,_,_)=>{probes++;return Task.FromResult(Entries("unused",98));},async token=>
        {waiting.SetResult();await Task.Delay(Timeout.Infinite,token);});
        var pending=debounce.RequestAsync(["video"],()=>new(Operation.Compress),()=>{},_=>completed++,_=>failures++,default);
        await waiting.Task;debounce.Cancel();await pending;
        Check(probes==0 && completed==0 && failures==0,"Cancelamento durante a espera iniciou análise ou mostrou erro");

        foreach(bool close in new[]{false,true})
        {
            var analysis=Gate();using var lifetime=new CancellationTokenSource();CancellationToken used=default;
            var coordinator=new EstimateCoordinator((_,_,token)=>{used=token;return analysis.Task;},_=>Task.CompletedTask);
            var task=coordinator.RequestAsync(["video"],()=>new(Operation.Compress),()=>{},_=>completed++,_=>failures++,lifetime.Token);
            if(close)lifetime.Cancel();else coordinator.Cancel();
            analysis.SetResult(Entries("video",98));await task;
            Check(used.IsCancellationRequested && completed==0 && failures==0,"Limpar/navegar/fechar permitiu publicação atrasada");
        }

        var failure=new IOException("Informe um tamanho válido.");Exception? observed=null;bool analyzing=false;
        var validation=new EstimateCoordinator((_,_,_)=>throw new Exception("Não deve sondar opção inválida"),_=>Task.CompletedTask);
        await validation.RequestAsync(["video"],()=>throw failure,()=>analyzing=true,_=>completed++,ex=>observed=ex,default);
        Check(ReferenceEquals(observed,failure) && !analyzing,"Validação perdeu a mensagem ou iniciou análise");

        var broken=new EstimateCoordinator((_,_,_)=>Task.FromException<IReadOnlyList<EstimateEntry>>(failure),_=>Task.CompletedTask);
        observed=null;
        await broken.RequestAsync(["video"],()=>new(Operation.Compress),()=>analyzing=true,_=>completed++,ex=>observed=ex,default);
        Check(analyzing && ReferenceEquals(observed,failure),"Falha da análise atual não chegou à apresentação");
        using var closed=new CancellationTokenSource();closed.Cancel();analyzing=false;observed=null;
        await broken.RequestAsync(["video"],()=>new(Operation.Compress),()=>analyzing=true,_=>completed++,ex=>observed=ex,closed.Token);
        Check(!analyzing && observed is null,"Janela encerrada iniciou nova análise");
        Console.WriteLine("PASS: estimativas recentes prevalecem; respostas/erros atrasados, debounce, navegação/limpeza, fechamento e validação protegidos.");
    }
}
