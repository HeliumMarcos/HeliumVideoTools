using System.ComponentModel;
using System.IO;
using HeliumVideoTools.Infrastructure;

namespace HeliumVideoTools.Desktop;

internal sealed class WindowCloseCoordinator
{
    internal bool Saving {get;private set;}
    private bool ready;

    internal async Task HandleAsync(CancelEventArgs e,bool skipIdle,Func<bool> busy,Func<bool> confirm,
        Action started,Action cancel,Func<Task> save,Action deferClose)
    {
        if(ready || (skipIdle && !busy()))return;
        e.Cancel=true;
        if(Saving)return;
        if(busy() && !confirm())return;
        Saving=true;started();
        AppLog.Write("INFO","Encerramento solicitado; salvando preferências");
        try
        {
            cancel();while(busy())await Task.Delay(50);
            await save();
            AppLog.Write("INFO","Preferências salvas no encerramento");
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {AppLog.Write("WARNING","Não foi possível salvar preferências",ex.Message);}
        // The caller must post Close to the dispatcher, even when save completes
        // synchronously. Re-entering Close inside WPF's Closing event is illegal.
        finally {ready=true;deferClose();}
    }
}
