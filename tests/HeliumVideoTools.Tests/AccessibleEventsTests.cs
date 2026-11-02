using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using HeliumVideoTools.Core;
using HeliumVideoTools.Desktop;

internal static class AccessibleEventsTests
{
    // Separate MTA client prevents UI Automation requests from blocking the
    // provider's STA dispatcher and exercises the real cross-process transport.
    internal static int Listen(string handle,string folder)
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var window=AutomationElement.FromHandle((nint)long.Parse(handle));
                var status=window.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"ProgressStatus"))??throw new Exception("Região de progresso não encontrada no cliente UIA");
                var events=new List<string>();var gate=new object();
                Automation.AddAutomationEventHandler(AutomationElementIdentifiers.LiveRegionChangedEvent,window,TreeScope.Subtree,(sender,_)=>
                {
                    lock(gate)
                    {
                        events.Add(((AutomationElement)sender).Current.Name);
                        string path=Path.Combine(folder,"events.json");
                        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(events));File.Move(path+".tmp",path,true);
                    }
                });
                try
                {
                    File.WriteAllText(Path.Combine(folder,"ready"),"ready");
                    var deadline=DateTime.UtcNow.AddSeconds(60);
                    while(!File.Exists(Path.Combine(folder,"stop")) && DateTime.UtcNow<deadline)Thread.Sleep(25);
                }
                finally{Automation.RemoveAllEventHandlers();}
            }
            catch(Exception ex){failure=ex;}
        });
        thread.SetApartmentState(ApartmentState.MTA);thread.Start();thread.Join();
        if(failure is null)return 0;Console.Error.WriteLine(failure);return 1;
    }

    internal static void Run(string parent)
    {
        AnnouncementTests.Run();
        string folder=Path.Combine(Path.GetFullPath(parent),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var app=new App(isolated:true){ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
        var window=new MainWindow(preview:true){ShowActivated=false};
        ((Border)window.FindName("ProgressPanel")).Visibility=Visibility.Visible;
        var status=(TextBlock)window.FindName("ProgressStatus");status.Text="Preparado para teste";
        window.Show();window.UpdateLayout();
        if(AutomationProperties.GetLiveSetting(status)!=AutomationLiveSetting.Polite)throw new Exception("Região não usa anúncio educado");
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(AccessibleEventsTests).Assembly.Location);
        foreach(string arg in new[]{"--uia-listener",new WindowInteropHelper(window).Handle.ToInt64().ToString(),folder})start.ArgumentList.Add(arg);
        using var listener=Process.Start(start)??throw new Exception("Cliente UIA não iniciou");
        var error=listener.StandardError.ReadToEndAsync();
        try
        {
            PumpUntil(()=>File.Exists(Path.Combine(folder,"ready")) || listener.HasExited,30);
            if(listener.HasExited)throw new Exception("Cliente UIA encerrou: "+error.GetAwaiter().GetResult());
            PumpFor(100);
            long clock=0;var announcer=new ProgressAnnouncer(status,new AnnouncementGate(()=>clock));
            status.Text="Teste de progresso 10%";announcer.Notify();
            PumpUntil(()=>Events(folder).Length>=1,10);
            if(Events(folder)[0]!="Teste de progresso 10%")throw new Exception("Cliente não recebeu o primeiro texto");
            for(int i=0;i<100;i++){status.Text="Rajada "+i;announcer.Notify();}
            PumpFor(300);
            if(Events(folder).Length!=1)throw new Exception("Rajada de progresso gerou eventos excessivos");
            clock=5000;status.Text="Teste de progresso 50%";announcer.Notify();
            PumpUntil(()=>Events(folder).Length>=2,10);
            if(Events(folder)[1]!="Teste de progresso 50%")throw new Exception("Anúncio posterior não chegou");
            var result=new BatchResult([new("teste.mp4",BatchItemState.Succeeded,[])],false);
            typeof(MainWindow).GetMethod("ShowBatchResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[result]);
            PumpUntil(()=>Events(folder).Length>=3,10);
            if(!Events(folder)[2].Contains("1 concluído"))throw new Exception("Resumo real da janela não anunciou conclusão");
            var failed=new BatchResult([new("teste.mp4",BatchItemState.Failed,[],"Erro de teste")],false);
            typeof(MainWindow).GetMethod("ShowBatchResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[failed]);
            PumpUntil(()=>Events(folder).Length>=4,10);
            if(!Events(folder)[3].Contains("1 com erro"))throw new Exception("Erro final não foi anunciado");
            var cancelled=new BatchResult([new("teste.mp4",BatchItemState.Cancelled,[])],true);
            typeof(MainWindow).GetMethod("ShowBatchResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[cancelled]);
            PumpUntil(()=>Events(folder).Length>=5,10);
            if(!Events(folder)[4].Contains("1 cancelado"))throw new Exception("Cancelamento final não foi anunciado");
            typeof(MainWindow).GetMethod("ShowBatchResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[cancelled]);PumpFor(150);
            if(Events(folder).Length!=5)throw new Exception("Resultado repetido foi anunciado duas vezes");
            ((Border)window.FindName("ProgressPanel")).Visibility=Visibility.Collapsed;window.UpdateLayout();
            window.Editor.Target.Text="";
            ((Button)window.FindName("RunButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpUntil(()=>Events(folder).Length>=6,10);
            if(!Events(folder)[5].Contains("Digite um tamanho inteiro"))throw new Exception("Validação com painel oculto não foi anunciada");
            File.WriteAllText(Path.Combine(folder,"stop"),"stop");
            PumpUntil(()=>listener.HasExited,10);
            if(listener.ExitCode!=0)throw new Exception(error.GetAwaiter().GetResult());
            Console.WriteLine("PASS: cliente UIA separado recebeu progresso, conclusão/erro/cancelamento e validação com painel oculto; rajada e resumo repetido suprimidos.");
        }
        finally
        {
            File.WriteAllText(Path.Combine(folder,"stop"),"stop");
            if(!listener.HasExited && !listener.WaitForExit(2000))listener.Kill(entireProcessTree:true);
            window.Close();
        }
    }
    private static string[] Events(string folder)
    {
        try{return JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(folder,"events.json")))??[];}
        catch(IOException){return [];}
    }
    private static void PumpUntil(Func<bool> done,int seconds)
    {
        var deadline=DateTime.UtcNow.AddSeconds(seconds);
        while(!done() && DateTime.UtcNow<deadline)PumpFor(25);
        if(!done())throw new TimeoutException("Tempo limite aguardando cliente/evento de acessibilidade");
    }
    private static void PumpFor(int milliseconds)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(_,_)=>frame.Continue=false;timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
    }
}
