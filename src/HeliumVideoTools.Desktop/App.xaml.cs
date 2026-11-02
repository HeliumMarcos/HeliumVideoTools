using System.Windows;
using System.Runtime.InteropServices;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;

namespace HeliumVideoTools.Desktop;
public partial class App : Application
{
    private readonly bool isolated;
    public App():this(false) { }
    internal App(bool isolated) {this.isolated=isolated;}
    private Mutex? instance;
    internal static Task StartupRecoveryTask {get;private set;}=Task.CompletedTask;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    protected override void OnStartup(StartupEventArgs e)
    {
        if(isolated){base.OnStartup(e);return;}
        if (Mutex.TryOpenExisting("Local\\Helium.HeliumVideoTools.Installer", out var installing)) { installing.Dispose(); Shutdown(); return; }
        instance = new Mutex(true, "Local\\Helium.HeliumVideoTools.Desktop", out bool created);
        if (!created) { Shutdown(); return; }
        SetCurrentProcessExplicitAppUserModelID("Helium.HeliumVideoTools");
        StartupRecoveryTask=RecoverAfterStartupAsync();
        DispatcherUnhandledException += (_, args) => { AppLog.Write("ERROR", "Erro inesperado", args.Exception.ToString()); MessageBox.Show("O aplicativo encontrou um problema. Os detalhes foram salvos no log.", "Helium Vídeo Tools"); args.Handled = true; };
        base.OnStartup(e);
        MainWindow=new MainWindow();MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
    private static async Task RecoverAfterStartupAsync()
    {
        try
        {
            var tools=await Task.Run(()=>MediaToolUpdater.DefaultStore.RecoverAsync());
            if(tools.Cleaned>0 || tools.Deferred>0 || tools.Invalid>0)AppLog.Write("INFO","Recuperação da atualização de ferramentas",System.Text.Json.JsonSerializer.Serialize(tools));
        }
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {AppLog.Write("WARNING","Recuperação das ferramentas adiada",ex.Message);}
        try
        {
            var result=await RecoveryJournal.Default.RecoverAsync();
            if(result.Cleaned>0 || result.Deferred>0 || result.Invalid>0)AppLog.Write("INFO","Recuperação de temporários após interrupção",System.Text.Json.JsonSerializer.Serialize(result));
        }
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {AppLog.Write("WARNING","Recuperação adiada",ex.Message);}
    }
}
