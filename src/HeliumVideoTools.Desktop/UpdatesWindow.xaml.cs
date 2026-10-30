using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Desktop;

// Tela aprovada pelo usuário e integrada à navegação de atualizações.
public partial class UpdatesWindow : Window
{
    private readonly HttpClient http = ReleaseClient.CreateHttpClient();
    private readonly ReleaseClient releases;
    private readonly Func<bool> mediaBusy;
    private readonly bool preview;
    private AppRelease? available;
    private ReleaseAsset? toolAsset;
    private bool toolsUpdateAvailable;
    private CancellationTokenSource? transfer;
    private string? verifiedDownload;
    private string? ffmpegDetails, ffprobeDetails;
    public bool Installing { get; private set; }
    public UpdatesWindow(Func<bool>? mediaBusy = null, bool preview = false, AppRelease? available = null)
    {
        InitializeComponent(); NativeWindowTheme.Attach(this); this.preview = preview; this.mediaBusy = mediaBusy ?? (() => false); releases = new(http);
        AppVersion.Text = "Instalado: " + typeof(UpdatesWindow).Assembly.GetName().Version!.ToString(3) + " • C# / .NET 10";
        this.available = available;
        if (available is not null) { AppUpdateStatus.Text = "Nova versão disponível: " + available.Version.ToString(3); InstallAppButton.IsEnabled = true; }
        if (preview) { FfmpegVersion.Text = "FFmpeg  9.0.2"; FfprobeVersion.Text = "ffprobe  9.0.2"; }
        else Loaded += async (_, _) => await ReadInstalledAsync();
        Closed += (_, _) => { transfer?.Cancel(); http.Dispose(); };
    }
    private async Task ReadInstalledAsync()
    {
        try
        {
            var ffmpeg = BatchViewModel.FindTool("ffmpeg"); var ffprobe = BatchViewModel.FindTool("ffprobe");
            ffmpegDetails = ffprobeDetails = null;
            ffmpegDetails = ffmpeg is null ? null : await MediaToolUpdater.InstalledVersionAsync(ffmpeg, default);
            ffprobeDetails = ffprobe is null ? null : await MediaToolUpdater.InstalledVersionAsync(ffprobe, default);
            ShowVersion(FfmpegVersion, "FFmpeg", ffmpegDetails); ShowVersion(FfprobeVersion, "ffprobe", ffprobeDetails);
        }
        catch (Exception ex) { ToolsStatus.Text = ex.Message; }
    }
    private static void ShowVersion(TextBlock label, string name, string? details)
    {
        label.Text = details is null ? name + " ausente" : name + "  " + (MediaToolUpdater.ParseInstalledVersion(details)?.ToString() ?? "versão não identificada");
        label.ToolTip = details;
        System.Windows.Automation.AutomationProperties.SetHelpText(label, details ?? "Ferramenta não encontrada");
    }
    private async void CheckApp(object sender, RoutedEventArgs e)
    {
        if (preview || transfer is not null) return;
        CheckAppButton.IsEnabled = false;
        try
        {
            AppUpdateStatus.Text = "Consultando Releases…";
            verifiedDownload = null;
            available = await releases.CheckAppAsync(typeof(UpdatesWindow).Assembly.GetName().Version!, default);
            AppUpdateStatus.Text = available is null ? "Você já usa a versão estável mais recente." : $"Nova versão disponível: {available.Version.ToString(3)}";
            InstallAppButton.IsEnabled = available is not null;
        }
        catch (Exception ex) { AppUpdateStatus.Text = ex.Message; }
        finally { CheckAppButton.IsEnabled = true; }
    }
    private async void CheckTools(object sender, RoutedEventArgs e)
    {
        if (preview || transfer is not null) return;
        CheckToolsButton.IsEnabled = false;
        try
        {
            await ReadInstalledAsync(); toolAsset = await new MediaToolUpdater(releases).CheckAsync(default);
            var ffmpeg = MediaToolUpdater.ParseInstalledVersion(ffmpegDetails);
            var ffprobe = MediaToolUpdater.ParseInstalledVersion(ffprobeDetails);
            bool newer = ffmpeg is null || ffprobe is null || ffmpeg < Version.Parse(toolAsset.Version) || ffprobe < Version.Parse(toolAsset.Version);
            toolsUpdateAvailable = newer;
            ToolsStatus.Text = newer ? $"Conjunto estável disponível: {toolAsset.Version}" : $"Conjunto instalado atualizado; versão estável disponível: {toolAsset.Version}.";
            InstallToolsButton.IsEnabled = newer;
        }
        catch (Exception ex) { ToolsStatus.Text = ex.Message; }
        finally { CheckToolsButton.IsEnabled = true; }
    }
    private async void InstallApp(object sender, RoutedEventArgs e)
    {
        if (preview || available is null || transfer is not null || Installing) return;
        if (mediaBusy()) { AppUpdateStatus.Text = "Aguarde o fim do processamento antes de iniciar a atualização."; return; }
        var release = available;
        transfer = new(); SetTransfer(true);
        try
        {
            string file = verifiedDownload ?? Path.Combine(AppLog.Root, "updates", Guid.NewGuid().ToString("N"), release.Installer.Name);
            if (!File.Exists(file)) await releases.DownloadAsync(release.Installer, file, new Progress<double>(p => { DownloadProgress.Value = p; TransferStatus.Text = $"Baixando aplicativo • {p:0}%"; }), transfer.Token);
            verifiedDownload = file;
            if (mediaBusy()) { AppUpdateStatus.Text = "Download concluído. Aguarde o processamento terminar e tente novamente para instalar."; return; }
            TransferStatus.Text = "Download concluído. Verificando a integridade do instalador…";
            DownloadProgress.IsIndeterminate = true;
            CancelTransfer.Content = "Cancelar";
            using var verified = await Task.Run(() => ReleaseClient.VerifyInstaller(file, release.Installer), transfer.Token);
            transfer.Token.ThrowIfCancellationRequested();
            if (mediaBusy()) { AppUpdateStatus.Text = "Instalador verificado. Aguarde o processamento terminar para instalar."; return; }
            Installing = true;
            using var process = Process.Start(new ProcessStartInfo(file) { Arguments = "/S /RESTART", UseShellExecute = true });
            if (process is null) throw new IOException("O instalador não iniciou.");
            SetTransfer(false); TransferStatus.Text = "Instalador iniciado. O aplicativo será fechado e reaberto.";
            await process.WaitForExitAsync();
            Installing = false;
            // Se este processo ainda está vivo, o instalador encerrou sem substituí-lo.
            // Libera as ações novamente em vez de deixar o app bloqueado indefinidamente.
            TransferStatus.Text = "O instalador foi encerrado. Se a atualização não foi aplicada, tente novamente.";
        }
        catch (OperationCanceledException) { TransferStatus.Text = "Download cancelado."; }
        catch (Exception ex) { Installing = false; verifiedDownload = null; TransferStatus.Text = ex.Message; }
        finally { transfer.Dispose(); transfer = null; SetTransfer(false); }
    }
    private async void InstallTools(object sender, RoutedEventArgs e)
    {
        if (preview || toolAsset is null || transfer is not null) return;
        if (mediaBusy()) { ToolsStatus.Text = "Aguarde o fim do processamento para atualizar as ferramentas."; return; }
        transfer = new(); SetTransfer(true);
        try
        {
            await new MediaToolUpdater(releases).InstallAsync(toolAsset, new Progress<double>(p => { DownloadProgress.Value = p; DownloadProgress.IsIndeterminate = p >= 100; TransferStatus.Text = p >= 100 ? "Verificando e preparando o conjunto de ferramentas…" : $"Baixando ferramentas • {p:0}%"; }), transfer.Token);
            ToolsStatus.Text = "Conjunto atualizado e verificado."; await ReadInstalledAsync();
        }
        catch (OperationCanceledException) { TransferStatus.Text = "Atualização cancelada; conjunto ativo preservado."; }
        catch (Exception ex) { ToolsStatus.Text = ex.Message; }
        finally { transfer.Dispose(); transfer = null; SetTransfer(false); }
    }
    private async void RestoreTools(object sender, RoutedEventArgs e)
    {
        if (preview || transfer is not null || mediaBusy()) return;
        try { await MediaToolUpdater.RestoreAsync(default); await ReadInstalledAsync(); ToolsStatus.Text = "Conjunto anterior restaurado."; }
        catch (Exception ex) { ToolsStatus.Text = ex.Message; }
    }
    private void SetTransfer(bool busy)
    {
        CheckAppButton.IsEnabled = CheckToolsButton.IsEnabled = !busy && !Installing;
        InstallAppButton.IsEnabled = !busy && !Installing && available is not null; InstallToolsButton.IsEnabled = !busy && !Installing && toolsUpdateAvailable;
        if (busy) { DownloadProgress.Value = 0; DownloadProgress.IsIndeterminate = false; CancelTransfer.Content = "Cancelar download"; TransferStatus.Text = "Preparando download…"; }
        else DownloadProgress.IsIndeterminate = Installing;
        DownloadProgress.Visibility = busy || Installing ? Visibility.Visible : Visibility.Collapsed;
        CancelTransfer.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        AppHelp.Visibility = ToolsHelp.Visibility = busy || Installing ? Visibility.Collapsed : Visibility.Visible;
        Dispatcher.InvokeAsync(() => UpdatesScroll.InvalidateMeasure(), System.Windows.Threading.DispatcherPriority.Loaded);
    }
    private void CancelDownload(object sender, RoutedEventArgs e) => transfer?.Cancel();
}
