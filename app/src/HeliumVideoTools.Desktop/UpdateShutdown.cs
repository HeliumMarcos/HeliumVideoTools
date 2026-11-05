using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HeliumVideoTools.Desktop;

// Explicit installer request also reaches a hidden/minimized main window.
// Closing still uses the normal WPF path, including saving preferences.
internal static class UpdateShutdown
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    internal static void Attach(Window window)
    {
        uint message=RegisterWindowMessage("Helium.HeliumVideoTools.CloseForUpdate");
        if(message==0)return;
        window.SourceInitialized+=(_,_)=>
        {
            var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source?.AddHook((nint hwnd,int msg,nint wParam,nint lParam,ref bool handled)=>
            {
                if((uint)msg==message)
                {handled=true;window.Dispatcher.BeginInvoke(new Action(window.Close));}
                return 0;
            });
        };
    }
}
