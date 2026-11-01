using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HeliumVideoTools.Desktop;

// Apenas cores da moldura: controles, Snap Layouts e redimensionamento nativos.
internal static class NativeWindowTheme
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    public static void Attach(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        void Refresh() => Apply(window);
        PropertyChangedEventHandler systemChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast) && !window.Dispatcher.HasShutdownStarted)
                window.Dispatcher.InvokeAsync(Refresh);
        };
        window.SourceInitialized += (_, _) => Refresh();
        window.Activated += (_, _) => Refresh();
        window.Deactivated += (_, _) => Refresh();
        EventHandler themeChanged=(_,_)=>Refresh();ThemeManager.Changed+=themeChanged;
        SystemParameters.StaticPropertyChanged += systemChanged;
        window.Closed += (_, _) => {SystemParameters.StaticPropertyChanged -= systemChanged;ThemeManager.Changed-=themeChanged;};
    }
    internal static bool Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0 || !OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)) return false;
        bool contrast = ThemeManager.HighContrast;
        int Color(string key){var c=((System.Windows.Media.SolidColorBrush)Application.Current.Resources[key]).Color;return c.R|(c.G<<8)|(c.B<<16);}
        return (Set(handle,20,contrast || ThemeManager.IsLight ? 0 : 1) >= 0)
            & (Set(handle,35,contrast ? -1 : Color("AppBackground")) >= 0)
            & (Set(handle,36,contrast ? -1 : Color(window.IsActive?"TextPrimary":"TextSecondary")) >= 0);
    }
    private static int Set(nint handle, int attribute, int value) => DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
}
