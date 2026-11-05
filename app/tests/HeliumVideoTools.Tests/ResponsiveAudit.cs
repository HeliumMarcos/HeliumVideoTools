using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using HeliumVideoTools.Desktop;

internal static class ResponsiveAudit
{
    [DllImport("user32.dll")] private static extern nint GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(nint left,nint right);
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]private static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll")]private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint hwnd,out NativeRect rect);
    private static IEnumerable<DependencyObject> Walk(DependencyObject node)
    {yield return node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Walk(VisualTreeHelper.GetChild(node,i)))yield return child;}

    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var app=new App(isolated:true) {ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
        ThemeManager.EnsureInitialized();ThemeManager.SetPreference("dark");
        var context=GetThreadDpiAwarenessContext();
        string awareness=AreDpiAwarenessContextsEqual(context,-4)?"PerMonitorV2":AreDpiAwarenessContextsEqual(context,-3)?"PerMonitor":AreDpiAwarenessContextsEqual(context,-2)?"System":"Other";
        if(awareness!="PerMonitorV2")throw new Exception("Manifesto não ativou PerMonitorV2 no host de teste");
        var results=new List<object>();
        // Logical client-area models, not changes to the user's monitor/DPI.
        foreach(var size in new[]{(1280,860),(1092,545),(900,449),(952,485),(675,329)})
        foreach(string operation in new[]{"Compress","Split","Webm"})
        foreach(bool proposed in new[]{false,true})
        {
            var window=new MainWindow(preview:true,adaptive:proposed) {ShowActivated=false};window.Show();
            if(operation!="Compress")((Button)window.FindName(operation=="Split"?"SplitNav":"WebmNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            var root=(Grid)window.Content;
            double frameWidth=window.ActualWidth-root.ActualWidth,frameHeight=window.ActualHeight-root.ActualHeight;
            double width=proposed?size.Item1:Math.Max(size.Item1,window.MinWidth-frameWidth);
            double height=proposed?size.Item2:Math.Max(size.Item2,window.MinHeight-frameHeight);
            window.AdaptiveLayout?.Refresh(width,height);
            // Fix the root's logical dimensions while retaining WPF's live
            // visual tree/templates and automation peers.
            root.HorizontalAlignment=HorizontalAlignment.Left;root.VerticalAlignment=VerticalAlignment.Top;
            root.Width=width;root.Height=height;
            root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
            var run=(Button)window.FindName("RunButton");
            var rect=run.TransformToAncestor(root).TransformBounds(new Rect(run.RenderSize));
            var missing=Walk(root).OfType<Button>().Where(b=>b.Visibility==Visibility.Visible && string.IsNullOrWhiteSpace(new ButtonAutomationPeer(b).GetName())).Select(b=>b.Name.Length>0?b.Name:b.Content is Panel panel?string.Join(" ",panel.Children.OfType<TextBlock>().Select(t=>t.Text)):"button: "+b.Content?.GetType().Name).ToArray();
            var scroll=(ScrollViewer)window.FindName("PageScroll");
            bool inside=rect.Left>=0 && rect.Right<=size.Item1 && rect.Top>=0 && rect.Bottom<=size.Item2;
            if(operation=="Compress" && size.Item1 is 1092 or 900 or 1280)
                Save(root,Path.Combine(folder,$"{size.Item1}x{size.Item2}-{(proposed?"proposal":"current")}.png"),size.Item1,size.Item2);
            if(proposed && operation=="Compress" && size.Item1==900)
            {
                scroll.ScrollToVerticalOffset(330);root.UpdateLayout();
                Save(root,Path.Combine(folder,"900x449-proposal-fields.png"),size.Item1,size.Item2);
            }
            if(proposed && size.Item2<420)
            {
                window.AdaptiveLayout!.RevealAction();root.UpdateLayout();
                window.Dispatcher.Invoke(()=>root.UpdateLayout(),System.Windows.Threading.DispatcherPriority.ContextIdle);
                rect=run.TransformToAncestor(root).TransformBounds(new Rect(run.RenderSize));
                if(operation=="Compress")Save(root,Path.Combine(folder,"675x329-proposal-action.png"),size.Item1,size.Item2);
            }
            bool reachable=rect.Left>=0 && rect.Right<=size.Item1 && rect.Top>=0 && rect.Bottom<=size.Item2;
            results.Add(new {operation,proposed,viewport=new{width=size.Item1,height=size.Item2},layout=new{width,height},actionInside=inside,actionReachable=reachable,formViewport=scroll.ViewportHeight,unnamedButtons=missing});
            if(proposed && !reachable)throw new Exception($"Ação não alcançável no protótipo: {operation}/{size}");
            if(proposed && missing.Length>0)throw new Exception("Botão sem nome acessível: "+string.Join(", ",missing));
            window.Close();
        }
        File.WriteAllText(Path.Combine(folder,"audit.json"),JsonSerializer.Serialize(new{awareness,method="Logical viewport simulation; not physical DPI or multi-monitor validation",results},new JsonSerializerOptions{WriteIndented=true}));
        if(!File.ReadAllBytes(Path.Combine(folder,"1280x860-current.png")).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(folder,"1280x860-proposal.png"))))throw new Exception("Layout padrão difere da referência sem adaptação");
        Console.WriteLine("PASS: conteúdo padrão idêntico à referência sem adaptação.");
        VerifyReflow();
        VerifyWorkArea();
        Console.WriteLine("Auditoria/previews geradas em "+Path.GetFullPath(folder)+"; contexto DPI real do host: "+awareness);
    }

    private static void VerifyWorkArea()
    {
        var window=new Window {Width=SystemParameters.VirtualScreenWidth+400,Height=SystemParameters.VirtualScreenHeight+400,Left=SystemParameters.VirtualScreenLeft-500,Top=SystemParameters.VirtualScreenTop-500,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
        WindowWorkArea.Attach(window);window.Show();window.UpdateLayout();
        window.Dispatcher.Invoke(()=>window.UpdateLayout(),System.Windows.Threading.DispatcherPriority.ContextIdle);
        var handle=new WindowInteropHelper(window).Handle;
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if(!GetWindowRect(handle,out var bounds) || !GetMonitorInfo(MonitorFromWindow(handle,2),ref info))throw new Exception("Falha ao ler área útil nativa");
        bool fits=bounds.Left>=info.Work.Left && bounds.Top>=info.Work.Top && bounds.Right<=info.Work.Right && bounds.Bottom<=info.Work.Bottom;
        window.Close();if(!fits)throw new Exception("Janela não foi limitada à área útil nativa");
        Console.WriteLine("PASS: janela própria maior/fora da tela foi ajustada à área útil nativa sem alterar o monitor.");
    }

    private static void VerifyReflow()
    {
        foreach(string operation in new[]{"Compress","Split","Webm"})
        {
            var window=new MainWindow(preview:true){ShowActivated=false};window.Show();
            if(operation!="Compress")((Button)window.FindName(operation=="Split"?"SplitNav":"WebmNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var root=(Grid)window.Content;root.HorizontalAlignment=HorizontalAlignment.Left;root.VerticalAlignment=VerticalAlignment.Top;
            var field=window.Editor.Target;field.Text="97";window.Editor.Unit.SelectedIndex=2;
            var run=(Button)window.FindName("RunButton");var progress=(Border)window.FindName("ProgressPanel");
            foreach(string state in new[]{"idle","running","result"})
            foreach(var size in new[]{(675,329),(900,449),(1092,545),(1280,860)})
            {
                progress.Visibility=state=="idle"?Visibility.Collapsed:Visibility.Visible;
                ((TextBlock)window.FindName("ProgressStatus")).Text=state=="running"?"Compactando • etapa 1/2 • 45% • restante 15s":"Concluídos: 2 • erros: 1";
                ((TextBlock)window.FindName("ResultDetails")).Visibility=state=="result"?Visibility.Visible:Visibility.Collapsed;
                ((Button)window.FindName("ReportButton")).Visibility=state=="result"?Visibility.Visible:Visibility.Collapsed;
                root.Width=size.Item1;root.Height=size.Item2;window.AdaptiveLayout!.Refresh(size.Item1,size.Item2);window.UpdateLayout();
                window.AdaptiveLayout.RevealAction();window.UpdateLayout();
                window.Dispatcher.Invoke(()=>window.UpdateLayout(),System.Windows.Threading.DispatcherPriority.ContextIdle);
                var bounds=run.TransformToAncestor(root).TransformBounds(new Rect(run.RenderSize));
                if(bounds.Left<0 || bounds.Right>size.Item1 || bounds.Top<0 || bounds.Bottom>size.Item2)throw new Exception($"Ação cortada após reflow: {operation}/{state}/{size}");
                if(field.Text!="97" || window.Editor.Unit.SelectedIndex!=2 || !ReferenceEquals(field,window.Editor.Target))throw new Exception("Reflow recriou campos ou perdeu valores");
            }
            if(Grid.GetRow(field.Parent as UIElement ?? field)!=1)throw new Exception("Layout amplo não restaurou os rótulos acima dos campos");
            window.Close();
        }
        Console.WriteLine("PASS: reflow reversível nas 3 operações, idle/progresso/resultado, valores e instâncias preservados.");
    }

    private static void Save(Visual visual,string path,int width,int height)
    {
        var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var output=File.Create(path);encoder.Save(output);
    }
}
