using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Automation;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;

internal static class InstalledPerformanceBenchmarks
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Entry
    {public uint Size,Usage,Pid;public nuint Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Exe;}
    [DllImport("kernel32.dll",SetLastError=true)]private static extern nint CreateToolhelp32Snapshot(uint flags,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern bool Process32First(nint snapshot,ref Entry entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern bool Process32Next(nint snapshot,ref Entry entry);
    [DllImport("kernel32.dll")]private static extern bool CloseHandle(nint handle);
    private sealed record MemorySample(double AppWorkingMb,double AppPrivateMb,double TreeWorkingMb,double TreePrivateMb,int Processes);
    private static Dictionary<int,int> Parents()
    {
        var result=new Dictionary<int,int>();var handle=CreateToolhelp32Snapshot(2,0);
        if(handle== -1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try{var entry=new Entry{Size=(uint)Marshal.SizeOf<Entry>()};if(Process32First(handle,ref entry))do{result[(int)entry.Pid]=(int)entry.Parent;}while(Process32Next(handle,ref entry));}
        finally{CloseHandle(handle);}return result;
    }
    private static MemorySample Sample(Process app)
    {
        var parents=Parents();var included=new HashSet<int>{app.Id};bool changed;
        do{changed=false;foreach(var (id,parent) in parents)if(included.Contains(parent))changed|=included.Add(id);}while(changed);
        double working=0,priv=0,appWorking=0,appPrivate=0;int count=0;
        foreach(int id in included)
            try{using var process=Process.GetProcessById(id);process.Refresh();double w=process.WorkingSet64/1_000_000d,p=process.PrivateMemorySize64/1_000_000d;working+=w;priv+=p;count++;if(id==app.Id){appWorking=w;appPrivate=p;}}
            catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
        return new(appWorking,appPrivate,working,priv,count);
    }
    internal static async Task VerifySamplerAsync()
    {
        using var self=Process.GetCurrentProcess();var initial=Sample(self);
        if(initial.Processes<1 || initial.AppWorkingMb<=0 || initial.TreeWorkingMb<initial.AppWorkingMb || initial.TreePrivateMb<initial.AppPrivateMb)throw new Exception("Amostragem não contém o próprio processo");
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true};
        if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(InstalledPerformanceBenchmarks).Assembly.Location);
        start.ArgumentList.Add("--benchmark-memory-child");using var child=Process.Start(start)??throw new Exception("Filho de teste não iniciou");
        try
        {
            if(await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))!="ready")throw new Exception("Filho de teste não ficou pronto");
            var together=Sample(self);
            if(together.Processes<2 || together.TreeWorkingMb<=together.AppWorkingMb || together.TreePrivateMb<=together.AppPrivateMb)throw new Exception("Memória simultânea não incluiu o filho próprio");
        }
        finally{if(!child.HasExited){child.Kill(true);await child.WaitForExitAsync();}}
        Console.WriteLine("PASS: amostragem inclui o host e filho próprio, totais simultâneos coerentes; somente filho de teste encerrado.");
    }
    private static AutomationElement? Id(AutomationElement root,string id)=>root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id));
    private static AutomationElement Named(AutomationElement root,string name)=>root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,name))??throw new Exception("Controle de teste ausente: "+name);
    private static void Invoke(AutomationElement element)=>((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    private static async Task Wait(Func<bool> condition,int seconds=30)
    {
        var watch=Stopwatch.StartNew();
        while(watch.Elapsed.TotalSeconds<seconds)
        {try{if(condition())return;}catch(Exception ex) when(ex is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException){}await Task.Delay(100);}
        throw new TimeoutException("Tempo limite na medição instalada");
    }
    private static async Task Stop(Process app)
    {
        if(app.HasExited)return;
        app.Refresh();
        if(app.MainWindowHandle!=0)
        {
            var root=AutomationElement.FromHandle(app.MainWindowHandle);
            var dialog=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ClassNameProperty,"#32770"));
            if(dialog is not null && dialog.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty,"2"),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button))) is {} cancel)Invoke(cancel);
            var action=Id(root,"RunButton");var progress=Id(root,"ProgressAction");
            if(action?.Current.IsEnabled==false && progress?.Current.Name=="Cancelar tudo" && progress.Current.IsEnabled)
            {Invoke(progress);await Wait(()=>action.Current.IsEnabled,30);}
        }
        app.CloseMainWindow();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try{await app.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){throw new Exception("App de teste não fechou normalmente; preferências não serão sobrescritas.");}
    }
    private static async Task AddFile(AutomationElement window,Process app,string source)
    {
        var invocation=Task.Run(()=>Invoke(Named(window,"Adicionar vídeos")));
        // The native picker varies with the shell/provider. Select this exact
        // fixture in the dialog; selection time is outside processing timing.
        Console.WriteLine("Selecione somente esta amostra no diálogo: "+source);
        await Wait(()=>Id(window,"FileCount")?.Current.Name=="1 arquivo(s)",180);
        await invocation.WaitAsync(TimeSpan.FromSeconds(10));
        var list=Id(window,"FilesList")!;
        var selected=list.FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem));
        if(selected.Count!=1 || !selected[0].Current.Name.StartsWith(Path.GetFileName(source),StringComparison.OrdinalIgnoreCase))throw new Exception("Arquivo selecionado não é a amostra sintética esperada");
        string help=selected[0].Current.HelpText;
        if(!string.IsNullOrWhiteSpace(help) && !string.Equals(help,source,StringComparison.OrdinalIgnoreCase))throw new Exception("Caminho selecionado não é o da amostra de teste");
    }
    private static void CheckBox(AutomationElement window,string name,bool value)
    {var toggle=(TogglePattern)Named(window,name).GetCurrentPattern(TogglePattern.Pattern);if((toggle.Current.ToggleState==ToggleState.On)!=value)toggle.Toggle();}
    private static void Choose(AutomationElement window,Process app,string name,string value)
    {
        var combo=window.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.NameProperty,name),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ComboBox)))??throw new Exception("Seletor ausente: "+name);var expand=(ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern);expand.Expand();
        try
        {
            AutomationElement? item=combo.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.NameProperty,value),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem)));
            if(item is null)
                foreach(AutomationElement top in AutomationElement.RootElement.FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ProcessIdProperty,app.Id)))
                {item=top.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.NameProperty,value),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem)));if(item is not null)break;}
            if(item is null)throw new Exception("Opção de teste ausente: "+value);
            ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        }
        finally{expand.Collapse();}
    }

    internal static async Task RunAsync(string executable,string destination,string? fixture=null,bool startupOnly=false)
    {
        executable=Path.GetFullPath(executable);destination=Path.GetFullPath(destination);
        if(!File.Exists(executable))throw new FileNotFoundException("Instalação ausente");
        bool Matching(Process p){try{return string.Equals(p.MainModule?.FileName,executable,StringComparison.OrdinalIgnoreCase);}catch{return false;}}
        if(Process.GetProcessesByName("HeliumVideoTools").Any(Matching))throw new Exception("Feche o aplicativo antes do benchmark; a sessão existente será preservada.");
        string folder=Path.Combine(destination,"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string tools=Path.Combine(Path.GetDirectoryName(executable)!,"tools"),ffmpeg=Path.Combine(tools,"ffmpeg.exe"),ffprobe=Path.Combine(tools,"ffprobe.exe");
        if(!File.Exists(ffmpeg) || !File.Exists(ffprobe))throw new IOException("Ferramentas instaladas ausentes");
        string preferences=Path.Combine(AppLog.Root,"preferences.json");byte[]? originalPreferences=File.Exists(preferences)?await File.ReadAllBytesAsync(preferences):null;
        if(originalPreferences is not null)await File.WriteAllBytesAsync(Path.Combine(folder,"preferences.private-backup"),originalPreferences);
        var startup=new List<object>();var operations=new List<object>();Process? app=null;AutomationElement? window=null;
        try
        {
            for(int iteration=1;iteration<=6;iteration++)
            {
                var watch=Stopwatch.StartNew();app=Process.Start(new ProcessStartInfo(executable){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden})??throw new Exception("Instalação não iniciou");
                await Wait(()=>
                {
                    app.Refresh();if(app.HasExited)throw new IOException("App instalado encerrou durante abertura");
                    if(app.MainWindowHandle==0)return false;window=AutomationElement.FromHandle(app.MainWindowHandle);
                    return Id(window,"RunButton")?.Current.IsEnabled==true && Id(window,"Heading")?.Current.Name=="Compactar vídeos";
                });
                double milliseconds=watch.Elapsed.TotalMilliseconds;await Task.Delay(500);var memory=Sample(app);
                startup.Add(new{Iteration=iteration,ActionReadyMs=milliseconds,IdleMemoryAfter500Ms=memory,WindowsUptimeSeconds=Environment.TickCount64/1000d});
                await File.WriteAllTextAsync(Path.Combine(folder,"startup.json"),JsonSerializer.Serialize(startup,new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine($"Instalado, abertura {iteration}/6: ação disponível {milliseconds:0} ms; residente {memory.AppWorkingMb:0.0} MB.");
                if(iteration<6){await Stop(app);app.Dispose();app=null;}
            }
            if(startupOnly)
            {
                Console.WriteLine("Aberturas registradas em "+Path.Combine(folder,"startup.json")+"; primeira amostra não garante cache frio.");return;
            }
            string source=Path.Combine(folder,"synthetic-720p-180s.mp4");
            Console.WriteLine("Preparando somente mídia sintética: 180 s, 1280×720, H.264/AAC…");
            if(fixture is not null)File.Copy(Path.GetFullPath(fixture),source,false);
            else await VideoEngine.ExecuteAsync(ffmpeg,["-y","-hide_banner","-f","lavfi","-i","testsrc2=size=1280x720:rate=25:duration=180","-f","lavfi","-i","sine=frequency=440:duration=180","-c:v","libx264","-preset","ultrafast","-crf","12","-c:a","aac","-shortest",source],null,default);
            var probe=new VideoEngine(ffmpeg,ffprobe,new RecoveryJournal(Path.Combine(folder,"probe-journal")));
            var info=await probe.ProbeAsync(source,default);string hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source)));
            if(info.Bytes<=98_000_000)throw new Exception("Amostra não exige compactação ao teto de 98 MB");
            await AddFile(window!,app!,source);
            foreach(string scenario in new[]{"Compress","Split","Webm"})
            {
                if(scenario!="Compress")Invoke(Id(window!,scenario=="Split"?"SplitNav":"WebmNav")!);
                if(scenario=="Split"){Choose(window!,app!,"Dividir por","Número de partes");Choose(window!,app!,"Quantidade","3 partes");}
                else CheckBox(window!,"Precisão: Modo preciso em 2 etapas",true);
                string label=scenario=="Split"?"Dividir":scenario=="Webm"?"Converter":"Compactar";
                var before=Directory.GetFiles(folder).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var samples=new List<MemorySample>();var watch=Stopwatch.StartNew();bool sawBusy=false;
                var action=Id(window!,"RunButton")!;Invoke(action);
                await Wait(()=>{sawBusy=!action.Current.IsEnabled;return sawBusy;},10);
                Console.WriteLine("Medindo interface instalada: "+label+"…");
                while(watch.Elapsed.TotalMinutes<12)
                {
                    samples.Add(Sample(app!));
                    if(action.Current.IsEnabled)break;
                    await Task.Delay(150);
                }
                watch.Stop();if(!action.Current.IsEnabled)throw new TimeoutException("Processamento da amostra excedeu 12 min");
                string counts=Id(window!,"ProgressStatus")!.Current.Name;
                if(!counts.Contains("1 concluído") || counts.Contains("com erro"))throw new Exception("Operação da instalação não concluiu: "+counts);
                var outputs=Directory.GetFiles(folder).Where(p=>!before.Contains(p) && Path.GetExtension(p) is ".mp4" or ".webm").ToArray();
                if(outputs.Length!=(scenario=="Split"?3:1))throw new Exception("Quantidade inesperada de saídas");
                var outputInfo=new List<object>();double duration=0;
                foreach(var output in outputs){var media=await probe.ProbeAsync(output,default);duration+=media.Duration;if(media.AudioKbps<=0)throw new Exception("Saída perdeu áudio");outputInfo.Add(new{Bytes=media.Bytes,Duration=media.Duration,AudioKbps=media.AudioKbps});}
                long bytes=outputs.Sum(p=>new FileInfo(p).Length);
                double? cap=scenario=="Compress"?98:scenario=="Webm"?Math.Min(299,info.Bytes/1_000_000d*.98):null;
                if(cap.HasValue && bytes>cap.Value*1_000_000)throw new Exception("Saída acima do teto aplicado");
                if(Math.Abs(duration-info.Duration)>1)throw new Exception("Duração total divergente");
                operations.Add(new{Scenario=scenario,Seconds=watch.Elapsed.TotalSeconds,SampleCount=samples.Count,PeakConcurrentTreeWorkingMb=samples.Max(s=>s.TreeWorkingMb),PeakConcurrentTreePrivateMb=samples.Max(s=>s.TreePrivateMb),PeakAppWorkingMb=samples.Max(s=>s.AppWorkingMb),MaximumProcessCount=samples.Max(s=>s.Processes),OutputBytes=bytes,CapMb=cap,Outputs=outputInfo,TwoPass=scenario!="Split",CopyOriginalAudio=scenario=="Compress"});
                Console.WriteLine($"{label}: {watch.Elapsed.TotalSeconds:0.00}s; pico residente simultâneo {samples.Max(s=>s.TreeWorkingMb):0.0} MB; {bytes/1_000_000d:0.00} MB de saída.");
            }
            if(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source)))!=hash)throw new Exception("Fonte sintética alterada");
            string report=Path.Combine(folder,"benchmark.json");
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{RecordedAt=DateTimeOffset.Now,ProductVersion=FileVersionInfo.GetVersionInfo(executable).ProductVersion,InstalledExeSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(executable))),InstalledRuntime=JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.ChangeExtension(executable,".runtimeconfig.json"))),OS=RuntimeInformation.OSDescription,LogicalProcessors=Environment.ProcessorCount,Cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),Ffmpeg=await MediaToolUpdater.InstalledVersionAsync(ffmpeg,default),Ffprobe=await MediaToolUpdater.InstalledVersionAsync(ffprobe,default),FfmpegSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(ffmpeg))),FfprobeSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(ffprobe))),Source=new{info.Duration,info.Bytes,info.FrameRate,info.AudioKbps,Sha256=hash,Width=1280,Height=720},Startup=startup,Operations=operations,Limitations=new[]{"First observed launch is not cold-cache controlled. Normal startup includes preferences, recovery and network check.","Action-ready time is external UIA polling, not precise ContentRendered timing. Memory sampled 500 ms later.","Processing has one sample per operation, includes UI/report overhead, excludes fixture generation and post-run probing.","Tree memory is simultaneous working/private bytes sampled around 150 ms plus UIA/query overhead. Shared pages may be counted twice; short peaks/processes may be missed.","User/system load, power, caches and temperature are not controlled. Synthetic 180s 720p CPU media does not represent all hearings."}},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("Relatório: "+report);
        }
        finally
        {
            if(app is not null){await Stop(app);app.Dispose();}
            if(Process.GetProcessesByName("HeliumVideoTools").Any(Matching))throw new Exception("Outra sessão abriu; preferências não serão sobrescritas.");
            if(originalPreferences is not null)
            {
                string temp=preferences+".benchmark-"+Guid.NewGuid().ToString("N")+".tmp";
                await File.WriteAllBytesAsync(temp,originalPreferences);File.Move(temp,preferences,true);
                var restoredPreferences=await File.ReadAllBytesAsync(preferences);
                if(!originalPreferences.SequenceEqual(restoredPreferences))throw new Exception("Preferências não restauradas");
            }
        }
    }
}
