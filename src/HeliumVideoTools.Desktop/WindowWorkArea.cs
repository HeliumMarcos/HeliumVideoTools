using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HeliumVideoTools.Desktop;

internal static class WindowWorkArea
{
    [StructLayout(LayoutKind.Sequential)]private struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]private static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll")]private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
    internal static void Attach(Window window)
    {
        double minimumWidth=Math.Min(675,window.MinWidth),minimumHeight=Math.Min(329,window.MinHeight);
        bool applying=false; nint lastMonitor=0;
        void Fit()
        {
            if(applying || !window.IsLoaded && new WindowInteropHelper(window).Handle==0)return;
            var handle=new WindowInteropHelper(window).Handle;var monitor=MonitorFromWindow(handle,2);
            var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
            if(!GetMonitorInfo(monitor,ref info) || !GetWindowRect(handle,out var rect))return;
            applying=true;
            try
            {
                lastMonitor=monitor;double scale=GetDpiForWindow(handle)/96d;if(scale<=0)scale=1;
                int availableWidth=info.Work.Right-info.Work.Left,availableHeight=info.Work.Bottom-info.Work.Top;
                window.MinWidth=Math.Min(minimumWidth,availableWidth/scale);window.MinHeight=Math.Min(minimumHeight,availableHeight/scale);
                if(window.WindowState!=WindowState.Normal)return;
                int width=Math.Min(rect.Right-rect.Left,availableWidth),height=Math.Min(rect.Bottom-rect.Top,availableHeight);
                int left=Math.Clamp(rect.Left,info.Work.Left,info.Work.Right-width),top=Math.Clamp(rect.Top,info.Work.Top,info.Work.Bottom-height);
                if(width!=rect.Right-rect.Left || height!=rect.Bottom-rect.Top || left!=rect.Left || top!=rect.Top)
                    SetWindowPos(handle,0,left,top,width,height,0x0014); // no activation or z-order changes
            }
            finally{applying=false;}
        }
        window.SourceInitialized+=(_,_)=>
        {
            Fit();
            HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook((nint hwnd,int message,nint wParam,nint lParam,ref bool handled)=>
            {
                if(message is 0x007e or 0x001a)window.Dispatcher.BeginInvoke(new Action(Fit));
                return 0;
            });
        };
        window.Loaded+=(_,_)=>Fit();
        window.DpiChanged+=(_,_)=>window.Dispatcher.BeginInvoke(new Action(Fit));
        window.LocationChanged+=(_,_)=>{if(MonitorFromWindow(new WindowInteropHelper(window).Handle,2)!=lastMonitor)Fit();};
        window.StateChanged+=(_,_)=>window.Dispatcher.BeginInvoke(new Action(Fit));
    }
}
