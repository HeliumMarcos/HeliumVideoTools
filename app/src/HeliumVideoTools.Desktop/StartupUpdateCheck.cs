using HeliumVideoTools.Infrastructure;

namespace HeliumVideoTools.Desktop;

// UI-context coordinator: recovery must finish before the single startup query.
internal sealed class StartupUpdateCheck
{
    private readonly Func<CancellationToken,Task<AppRelease?>> check;
    private readonly Action<Exception> unavailable;
    private bool started;
    internal AppRelease? Available {get;private set;}

    internal StartupUpdateCheck(Func<CancellationToken,Task<AppRelease?>>? check=null,Action<Exception>? unavailable=null)
    {
        this.check=check??CheckAsync;
        this.unavailable=unavailable??(ex=>AppLog.Write("INFO","Consulta de atualização indisponível",ex.Message));
    }

    internal async Task RunOnceAsync(Task recovery,Action recovered,Action<AppRelease> notify,CancellationToken token)
    {
        if(started)return;
        started=true;
        try
        {
            await recovery.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            recovered();
            var release=await check(token);
            token.ThrowIfCancellationRequested();
            Available=release;
            if(release is not null)notify(release);
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) {if(!token.IsCancellationRequested)unavailable(ex);}
    }

    private static async Task<AppRelease?> CheckAsync(CancellationToken token)
    {
        using var http=ReleaseClient.CreateHttpClient();
        return await new ReleaseClient(http).CheckAppAsync(typeof(MainWindow).Assembly.GetName().Version!,token);
    }
}
