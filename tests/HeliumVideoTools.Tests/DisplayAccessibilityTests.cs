using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using HeliumVideoTools.Desktop;

internal static class DisplayAccessibilityTests
{
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct Info{public int Size;public Rect Monitor,Work;public uint Flags;}
    private delegate bool MonitorCallback(nint monitor,nint dc,ref Rect rect,nint data);
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(nint dc,nint clip,MonitorCallback callback,nint data);
    [DllImport("user32.dll")]private static extern bool GetMonitorInfo(nint monitor,ref Info info);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")]private static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);var monitors=new List<(nint Handle,Info Details)>();
        MonitorCallback callback=(nint monitor,nint dc,ref Rect rect,nint data)=>
        {var info=new Info{Size=Marshal.SizeOf<Info>()};if(!GetMonitorInfo(monitor,ref info))return false;monitors.Add((monitor,info));return true;};
        if(!EnumDisplayMonitors(0,0,callback,0) || monitors.Count==0)throw new Exception("Monitores não puderam ser enumerados");
        var app=new App(isolated:true){ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
        var window=new MainWindow(preview:true){ShowActivated=false};WindowWorkArea.Attach(window);
        int changes=0;window.DpiChanged+=(_,_)=>changes++;window.Show();
        var handle=new WindowInteropHelper(window).Handle;var results=new List<object>();var dpis=new HashSet<uint>();
        try
        {
            foreach(var monitor in monitors)
            {
                var work=monitor.Details.Work;int width=work.Right-work.Left,height=work.Bottom-work.Top;
                if(!SetWindowPos(handle,0,work.Left+10,work.Top+10,Math.Min(1200,width-20),Math.Min(800,height-20),0x14))throw new Exception("Janela de teste não foi movida");
                window.Dispatcher.Invoke(()=>window.UpdateLayout(),DispatcherPriority.ContextIdle);
                uint dpi=GetDpiForWindow(handle);dpis.Add(dpi);
                if(dpi==0 || MonitorFromWindow(handle,2)!=monitor.Handle || !GetWindowRect(handle,out var bounds))throw new Exception("Janela não chegou ao monitor esperado");
                bool fits=bounds.Left>=work.Left && bounds.Top>=work.Top && bounds.Right<=work.Right && bounds.Bottom<=work.Bottom;
                if(!fits)throw new Exception("Janela fora da área útil após mover");
                results.Add(new{width,height,dpi,scale=dpi/96d,insideWorkArea=fits});
            }
            bool mixed=dpis.Count>1;
            if(mixed && changes==0)throw new Exception("Monitores com DPI diferente não geraram evento de mudança");
            File.WriteAllText(Path.Combine(folder,"displays.json"),JsonSerializer.Serialize(new{monitors=monitors.Count,dpiChanges=changes,mixedDpiValidated=mixed,results,limitation=mixed?null:"This environment has no different-DPI monitor pair; 100/125/150/200% physical campaign remains pending."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"PASS: janela própria verificada em {monitors.Count} monitor(es), DPI(s) {string.Join(",",dpis)}, limites respeitados. DPI misto validado: {mixed}.");
        }
        finally{window.Close();}
    }
}
