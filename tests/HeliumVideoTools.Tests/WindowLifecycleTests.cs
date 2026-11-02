using System.ComponentModel;
using System.IO;
using HeliumVideoTools.Desktop;
using HeliumVideoTools.Infrastructure;

internal static class WindowLifecycleTests
{
    private static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    private static AppRelease Release()=>new(new Version(9,0,0),"teste",new("9.0.0","Setup.exe",new Uri("https://github.com/HeliumMarcos/HeliumVideoTools/releases/download/v9.0.0/Setup.exe"),100,new string('a',64)));

    internal static async Task RunAsync()
    {
        var recovery=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int queries=0,refreshed=0,notified=0,errors=0;
        var release=Release();
        var updates=new StartupUpdateCheck(_=>{queries++;return Task.FromResult<AppRelease?>(release);},_=>errors++);
        var first=updates.RunOnceAsync(recovery.Task,()=>refreshed++,_=>notified++,default);
        await updates.RunOnceAsync(Task.CompletedTask,()=>refreshed++,_=>notified++,default);
        Check(queries==0 && refreshed==0,"Consulta iniciou antes da recuperação ou foi duplicada");
        recovery.SetResult();await first;
        await updates.RunOnceAsync(Task.CompletedTask,()=>refreshed++,_=>notified++,default);
        Check(queries==1 && refreshed==1 && notified==1 && updates.Available==release && errors==0,"Consulta inicial não preservou resultado único");

        foreach(bool duringQuery in new[]{false,true})
        {
            using var lifetime=new CancellationTokenSource();
            var gate=new TaskCompletionSource<AppRelease?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recoveryGate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls=0,callbacks=0;
            var check=new StartupUpdateCheck(_=>{calls++;return gate.Task;},_=>callbacks++);
            var pending=check.RunOnceAsync(duringQuery?Task.CompletedTask:recoveryGate.Task,()=>{},_=>callbacks++,lifetime.Token);
            lifetime.Cancel();gate.SetResult(release);
            await pending.WaitAsync(TimeSpan.FromSeconds(3));
            Check(calls==(duringQuery?1:0) && callbacks==0 && check.Available is null,"Fechamento permitiu resultado tardio ou aguardou recuperação indefinidamente");
        }
        foreach(bool fails in new[]{false,true})
        {
            int notices=0,warnings=0;
            var check=new StartupUpdateCheck(_=>fails?Task.FromException<AppRelease?>(new IOException("offline")):Task.FromResult<AppRelease?>(null),_=>warnings++);
            await check.RunOnceAsync(Task.CompletedTask,()=>{},_=>notices++,default);
            Check(notices==0 && warnings==(fails?1:0),"Sem atualização/falha de rede gerou notificação incorreta");
        }

        int starts=0,cancels=0,saves=0,closes=0;
        var shutdown=new WindowCloseCoordinator();
        var e=new CancelEventArgs();
        await shutdown.HandleAsync(e,true,()=>false,()=>throw new Exception("Prévia não pede confirmação"),()=>starts++,()=>cancels++,()=>{saves++;return Task.CompletedTask;},()=>closes++);
        Check(!e.Cancel && starts+saves+cancels+closes==0,"Prévia ociosa alterou perfil ou bloqueou fechamento");
        e=new();
        await shutdown.HandleAsync(e,false,()=>true,()=>false,()=>starts++,()=>cancels++,()=>{saves++;return Task.CompletedTask;},()=>closes++);
        Check(e.Cancel && !shutdown.Saving && starts+saves+cancels+closes==0,"Recusar cancelamento iniciou encerramento");

        bool busy=true;
        var saveStarted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saved=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstClose=new CancelEventArgs();
        var closing=shutdown.HandleAsync(firstClose,false,()=>busy,()=>true,()=>starts++,()=>cancels++,async ()=>{saves++;saveStarted.SetResult();await saved.Task;},()=>closes++);
        var duplicate=new CancelEventArgs();
        await shutdown.HandleAsync(duplicate,false,()=>busy,()=>throw new Exception("Confirmação duplicada"),()=>starts++,()=>cancels++,()=>throw new Exception("Gravação duplicada"),()=>closes++);
        Check(firstClose.Cancel && duplicate.Cancel && shutdown.Saving && starts==1 && cancels==1 && saves==0 && closes==0,"Encerramento repetido não aguardou processamento");
        busy=false;await saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(saves==1 && closes==0,"Janela fechou antes de salvar preferências");
        saved.SetResult();await closing;
        e=new();await shutdown.HandleAsync(e,false,()=>false,()=>true,()=>starts++,()=>cancels++,()=>Task.CompletedTask,()=>closes++);
        Check(!e.Cancel && closes==1,"Fechamento final foi bloqueado ou solicitado duas vezes");

        foreach(Exception failure in new Exception[]{new IOException("disco"),new UnauthorizedAccessException("acesso")})
        {
            var failed=new WindowCloseCoordinator();int deferred=0;
            await failed.HandleAsync(new(),false,()=>false,()=>true,()=>{},()=>{},()=>Task.FromException(failure),()=>deferred++);
            Check(deferred==1,"Falha de gravação impediu fechamento");
        }
        // Synchronous save still delegates the final close instead of calling it
        // recursively inside WPF's Closing event (covered with real windows too).
        var synchronous=new WindowCloseCoordinator();int posts=0;
        await synchronous.HandleAsync(new(),false,()=>false,()=>true,()=>{},()=>{},()=>Task.CompletedTask,()=>posts++);
        Check(posts==1,"Gravação síncrona não agendou fechamento");
        Console.WriteLine("PASS: consulta após recuperação/uma vez, rede/cancelamento, encerramento recusado/repetido, espera do lote e gravação antes de fechar.");
    }
}
