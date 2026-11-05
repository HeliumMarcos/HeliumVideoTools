using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;

namespace HeliumVideoTools.Desktop;

// Called on the UI context. Requests own their cancellation source until their
// continuation finishes; replacing a request never disposes a token still in use.
internal sealed class EstimateCoordinator
{
    private CancellationTokenSource? current;
    private VideoEstimateService? service;
    private string? probe;
    private readonly Func<string[],VideoOptions,CancellationToken,Task<IReadOnlyList<EstimateEntry>>> analyze;
    private readonly Func<CancellationToken,Task> debounce;

    internal EstimateCoordinator(
        Func<string[],VideoOptions,CancellationToken,Task<IReadOnlyList<EstimateEntry>>>? analyze=null,
        Func<CancellationToken,Task>? debounce=null)
    {
        this.analyze=analyze??AnalyzeAsync;
        this.debounce=debounce??(token=>Task.Delay(250,token));
    }

    internal void Cancel()
    {
        var previous=current;
        current=null;
        previous?.Cancel();
    }

    internal async Task RequestAsync(string[] sources,Func<VideoOptions> readOptions,
        Action analyzing,Action<EstimatePresentation> completed,Action<Exception> failed,
        CancellationToken lifetime)
    {
        Cancel();
        var request=CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        current=request;
        try
        {
            request.Token.ThrowIfCancellationRequested();
            var options=readOptions();
            analyzing();
            await debounce(request.Token);
            var result=await analyze(sources,options,request.Token);
            request.Token.ThrowIfCancellationRequested();
            if(ReferenceEquals(current,request))completed(EstimatePresentation.From(result,options));
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { if(ReferenceEquals(current,request) && !request.IsCancellationRequested)failed(ex); }
        finally
        {
            if(ReferenceEquals(current,request))current=null;
            request.Dispose();
        }
    }

    private Task<IReadOnlyList<EstimateEntry>> AnalyzeAsync(string[] sources,VideoOptions options,CancellationToken token)
    {
        var tools=BatchViewModel.CaptureTools();
        if(service is null || probe!=tools.Ffprobe)
        {
            var engine=new VideoEngine(tools.Ffmpeg,tools.Ffprobe);
            service=new VideoEstimateService(async (path,cancellation)=>
            {
                if(tools.Set is not null)await MediaToolUpdater.DefaultStore.ValidateAsync(tools.Set,cancellation);
                return await engine.ProbeAsync(path,cancellation);
            });
            probe=tools.Ffprobe;
        }
        return service.EstimateAsync(sources,options,token);
    }
}
