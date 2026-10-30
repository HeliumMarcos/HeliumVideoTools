using System.Windows;
using System.Runtime.InteropServices;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Desktop;
public partial class App : Application
{
    private Mutex? instance;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new Mutex(true, "Local\\Helium.HeliumVideoTools.Desktop", out bool created);
        if (!created) { Shutdown(); return; }
        SetCurrentProcessExplicitAppUserModelID("Helium.HeliumVideoTools");
        DispatcherUnhandledException += (_, args) => { AppLog.Write("ERROR", "Erro inesperado", args.Exception.ToString()); MessageBox.Show("O aplicativo encontrou um problema. Os detalhes foram salvos no log.", "Helium Vídeo Tools"); args.Handled = true; };
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
