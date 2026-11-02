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
        Console.WriteLine("PASS: 8 fechamentos WPF normais/solicitados pelo instalador, preferências isoladas salvas e eventos sem reentrância.");
    }
}
