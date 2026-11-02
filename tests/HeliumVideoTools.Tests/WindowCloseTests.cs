using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;
using HeliumVideoTools.Desktop;

internal static class WindowCloseTests
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool PostMessage(nint window,uint message,nint wParam,nint lParam);
    public static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var app=new App {ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
        for(int i=0;i<8;i++)
        {
            string path=Path.Combine(folder,"preferences.json");
            File.WriteAllText(path,"{\"SplitParts\":4,\"TargetMb\":12}");
            var store=new UserPreferenceStore(folder);
            var window=new MainWindow(preview:true);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(MainWindow).GetField("preferenceStore",flags)!.SetValue(window,store);
            typeof(MainWindow).GetField("preferences",flags)!.SetValue(window,store.Load());
            bool closed=false;window.Closed+=(_,_)=>closed=true;
            window.Show();
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
            if(i%2==0)window.Close();
            else if(!PostMessage(new WindowInteropHelper(window).Handle,RegisterWindowMessage("Helium.HeliumVideoTools.CloseForUpdate"),0,0))throw new Exception("Solicitação de atualização não enviada");
            var frame=new DispatcherFrame();var deadline=DateTime.UtcNow.AddSeconds(10);
            var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(10)};
            timer.Tick+=(_,_)=>{if(closed || DateTime.UtcNow>=deadline)frame.Continue=false;};
            timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
            if(!closed || File.ReadAllText(path).Contains("TargetMb") || store.Load().SplitParts!=4)throw new Exception("Fechamento não salvou preferências isoladas");
        }
        var owner=new Window();owner.Show();bool busy=false;int created=0;
        Func<bool>? suppliedBusy=null;
        var release=new AppRelease(new Version(9,0,0),"teste",new("9.0.0","Setup.exe",new Uri("https://github.com/HeliumMarcos/HeliumVideoTools/releases/download/v9.0.0/Setup.exe"),100,new string('a',64)));
        var controller=new UpdateWindowController(owner,()=>busy,()=>release,(guard,available)=>
        {created++;suppliedBusy=guard;return new UpdatesWindow(guard,preview:true,available:available);});
        controller.Show();controller.Show();
        var dialog=owner.OwnedWindows.OfType<UpdatesWindow>().Single();
        if(created!=1 || dialog.Owner!=owner || controller.Installing || !((System.Windows.Controls.TextBlock)dialog.FindName("AppUpdateStatus")).Text.Contains("9.0.0"))throw new Exception("Atualizações duplicadas ou versão/owner perdidos");
        busy=true;if(suppliedBusy?.Invoke()!=true)throw new Exception("Janela de atualização perdeu bloqueio durante processamento/encerramento");
        dialog.Close();controller.Show();
        if(created!=2 || owner.OwnedWindows.OfType<UpdatesWindow>().Count()!=1)throw new Exception("Atualizações não reabriu após fechar");
        owner.Close();
        Console.WriteLine("PASS: 8 fechamentos WPF normais/solicitados pelo instalador, preferências isoladas salvas e eventos sem reentrância.");
        Console.WriteLine("PASS: Atualizações reutiliza janela aberta, preserva versão/owner/bloqueio e permite reabrir após fechar.");
    }
}
