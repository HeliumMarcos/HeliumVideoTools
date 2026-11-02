using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HeliumVideoTools.Desktop;

internal static class KeyboardAccessibilityTests
{
    internal static void Run()
    {
        var app=new App(isolated:true){ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
        foreach(string operation in new[]{"Compress","Split","Webm"})
        {
            var window=new MainWindow(preview:true){Width=980,Height=700};window.Show();window.Activate();
            if(operation!="Compress")((Button)window.FindName(operation=="Split"?"SplitNav":"WebmNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var start=(Button)window.FindName("SplitNav");var run=(Button)window.FindName("RunButton");
            Keyboard.Focus(start);Pump();
            var visited=Traverse(window,FocusNavigationDirection.Next);
            if(app.Windows.Count!=1)throw new Exception("Host isolado criou janela automática adicional");
            if(!visited.Contains(run) || !visited.Contains(window.Editor.Target) || !visited.Contains(window.Editor.Unit))throw new Exception("Tab não alcançou ação/campos: "+operation+"; "+string.Join(", ",visited.OfType<FrameworkElement>().Select(e=>e.GetType().Name+"/"+e.Name+"/"+System.Windows.Automation.AutomationProperties.GetName(e))));
            Keyboard.Focus(run);Pump();
            var backwards=Traverse(window,FocusNavigationDirection.Previous);
            if(!backwards.Contains(window.Editor.Target) || !backwards.Contains(start))throw new Exception("Shift+Tab não retornou aos campos/navegação: "+operation);
            ((FrameworkElement)window.FindName("ProgressPanel")).Visibility=Visibility.Visible;
            ((Panel)window.FindName("OptionsPanel")).IsEnabled=false;run.IsEnabled=false;
            Keyboard.Focus(start);Pump();visited=Traverse(window,FocusNavigationDirection.Next);
            if(!visited.Contains((Button)window.FindName("ProgressAction")) || visited.Contains(window.Editor.Target) || visited.Contains(run))throw new Exception("Tab durante processamento não alcançou Cancelar ou entrou em campo desabilitado");
            window.Close();
        }
        Console.WriteLine("PASS: navegação WPF Tab/Shift+Tab nas 3 ferramentas, nomes acessíveis, ação/campos/cancelar alcançáveis e desabilitados ignorados.");
    }
    private static HashSet<IInputElement> Traverse(MainWindow window,FocusNavigationDirection direction)
    {
        var seen=new HashSet<IInputElement>();
        for(int i=0;i<100 && Keyboard.FocusedElement is UIElement focused;i++)
        {
            if(!seen.Add(focused))break;
            if(focused is Button or ComboBox or TextBox or CheckBox or ListBox)
            {
                var peer=UIElementAutomationPeer.CreatePeerForElement(focused);
                if(peer is null || string.IsNullOrWhiteSpace(peer.GetName()))throw new Exception("Controle alcançado por teclado não tem nome: "+focused.GetType().Name);
            }
            if(!focused.MoveFocus(new TraversalRequest(direction)))break;
            Pump();
        }
        return seen;
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
}
