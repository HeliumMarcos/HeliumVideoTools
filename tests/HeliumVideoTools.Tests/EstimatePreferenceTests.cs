using System.IO;
using System.Text.Json;
using HeliumVideoTools.Core;
using HeliumVideoTools.Desktop;

internal static class EstimatePreferenceTests
{
    public static async Task RunAsync(string folder)
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        var source=new MediaInfo(300,180000000,128);
        var compress=VideoEstimates.Calculate(source,new());
        Check(compress.CapMb==98 && Math.Abs(compress.BudgetMb!.Value-92.1)<.01,"Orçamento de compactação divergiu do motor");
        var webm=VideoEstimates.Calculate(new(60,40000000,128),new(Operation.Webm,299,"vp8"));
        Check(webm.CapMb==39.2 && webm.BudgetMb<40,"WebM deixou de limitar o orçamento ao original");
        foreach(var (preset,factor) in new[]{("low",.75),("medium",.55),("high",.35)}) Check(VideoSizing.TargetMb(source,new(Preset:preset))==180*factor,"Preset divergiu da codificação");
        var split=VideoEstimates.Calculate(new(7200,540000000,128),new(Operation.Split,SplitMode:SplitMode.Parts,SplitValue:3));
        Check(split.ReferencePartMb==180 && split.Parts==3 && split.PartSeconds==2400 && split.CapMb is null,"Divisão confundiu referência com limite");
        var timed=VideoEstimates.Calculate(new(7200,540000000,128),new(Operation.Split,SplitMode:SplitMode.Duration,SplitValue:5));
        Check(timed.Parts==24 && timed.ReferencePartMb==22.5,"Estimativa por duração incorreta");
        var sized=VideoEstimates.Calculate(source,new(Operation.Split,SplitMode:SplitMode.Size,SplitValue:98));
        Check(sized.CapMb==98 && sized.Parts is null,"Estimativa inventou quantidade de partes antes de recodificar");
        Check(VideoEstimates.Calculate(new(60,40000000,128),new()).Warning is not null,"Alvo maior que original não foi sinalizado");
        var text=EstimatePresentation.From([new("vídeo.mp4",webm)],new(Operation.Webm,299,"vp8"));
        Check(text.Summary.Contains("36,8 MB") && text.Details.Contains("39,2 MB"),"Apresentação não separou orçamento de teto");
        var small=EstimatePresentation.From([new("pequeno.mp4",VideoEstimates.Calculate(new(1,400000,0),new(TargetMb:.08)))],new(TargetMb:.08));
        Check(small.Explanation.Contains("80 KB"),"Apresentação arredondou incorretamente limites em KB");

        string inputs=Path.Combine(folder,"estimativas"); Directory.CreateDirectory(inputs);
        string a=Path.Combine(inputs,"a.mp4"),b=Path.Combine(inputs,"b.mp4"),c=Path.Combine(inputs,"c.mp4"),d=Path.Combine(inputs,"d.mp4");
        foreach(var path in new[]{a,b,c,d})await File.WriteAllTextAsync(path,"vídeo fictício para teste de cache");
        int calls=0,active=0,maximum=0;
        var gate=new object();
        var service=new VideoEstimateService(async (path,token)=>
        { Interlocked.Increment(ref calls); int n=Interlocked.Increment(ref active); lock(gate)maximum=Math.Max(maximum,n); try { await Task.Delay(30,token); return new(60,new FileInfo(path).Length,0); } finally { Interlocked.Decrement(ref active); } });
        await service.EstimateAsync([a,b,c,d],new(),default); await service.EstimateAsync([a,b,c,d],new(TargetMb:50),default);
        Check(calls==4 && maximum<=2,"Cache ou limite de sondagens falhou");
        await File.AppendAllTextAsync(a,"alterado"); await service.EstimateAsync([a,b,c,d],new(),default);
        Check(calls==5,"Cache ignorou alteração do arquivo");
        var timeoutService=new VideoEstimateService((path,_)=>path==a?Task.FromException<MediaInfo>(new OperationCanceledException()):Task.FromResult(new MediaInfo(60,40000000,128)));
        var mixed=await timeoutService.EstimateAsync([a,b],new(),default);
        Check(mixed[0].Error is not null && mixed[1].Estimate is not null,"Timeout de uma estimativa interrompeu as demais");
        using(var canceled=new CancellationTokenSource()) { canceled.Cancel(); try { await service.EstimateAsync([a],new(),canceled.Token); throw new Exception("Estimativa ignorou cancelamento"); } catch(OperationCanceledException){} }

        string preferences=Path.Combine(folder,"preferências"); int imports=0;
        var store=new UserPreferenceStore(preferences,()=>{imports++;return new Dictionary<string,string>{{"split/parts","4"},{"split/minutes","8"},{"compress/precise","false"},{"webm/precise","true"},{"compress/target_mb","12"}};});
        var prefs=store.Load(); Check(prefs.SplitParts==4 && prefs.SplitMinutes==8 && !prefs.CompressionTwoPass && prefs.LegacyImported,"Importação de preferências úteis falhou");
        Check(StartupDefaults.For(Operation.Compress,prefs) is { TargetMb:98,CopyAudio:true,Preset:"custom",Codec:"h264" },"Preferências alteraram padrões protegidos da compactação");
        Check(StartupDefaults.For(Operation.Webm,prefs) is { TargetMb:299,Codec:"vp8" },"Preferências alteraram o padrão VP8");
        await store.SaveAsync(prefs with { LastDirectory=inputs,Width=1400,Height=950 });
        var loaded=store.Load(); Check(imports==1 && loaded.Width==1400 && loaded.LastDirectory==inputs,"Preferências não persistiram ou foram reimportadas");
        await File.WriteAllTextAsync(Path.Combine(preferences,"preferences.json"),"JSON inválido");
        var fallback=store.Load(); Check(fallback.Width==1280 && fallback.CompressionTwoPass,"Preferências corrompidas impediram os padrões");
        await store.SaveAsync(fallback); Check(Directory.GetFiles(preferences,"*.invalid-*").Length==1,"Configuração inválida não foi preservada");
        using(var json=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(preferences,"preferences.json")))) Check(!json.RootElement.TryGetProperty("TargetMb",out _),"Alvo protegido foi persistido indevidamente");
        Check(Directory.GetFiles(preferences,"*.tmp").Length==0,"Preferências deixaram temporários");
        Console.WriteLine("PASS: orçamentos/presets, referências por parte, WebM menor, cache, cancelamento e preferências com importação e recuperação de JSON inválido.");
    }
}
