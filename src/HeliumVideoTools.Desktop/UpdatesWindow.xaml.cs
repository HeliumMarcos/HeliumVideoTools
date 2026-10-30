using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Desktop;

// Tela preparada para revisão visual; ainda sem entrada no menu do pacote atual.
public partial class UpdatesWindow : Window
{
    private readonly HttpClient http = ReleaseClient.CreateHttpClient();
    private readonly ReleaseClient releases;
    private readonly Func<bool> mediaBusy;
    private readonly bool preview;
    private AppRelease? available;
    private ReleaseAsset? toolAsset;
    private CancellationTokenSource? transfer;
    public UpdatesWindow(Func<bool>? mediaBusy = null, bool preview = false)
    {
        InitializeComponent(); this.preview = preview; this.mediaBusy = mediaBusy ?? (() => false); releases = new(http);
        AppVersion.Text = "Instalado: " + typeof(UpdatesWindow).Assembly.GetName().Version!.ToString(3) + " • C# / .NET 10";
        if (preview) { FfmpegVersion.Text = "FFmpeg instalado: 9.0.2"; FfprobeVersion.Text = "ffprobe instalado: 9.0.2"; }
        else Loaded += async (_, _) => await ReadInstalledAsync();
        Closed += (_, _) => { transfer?.Cancel(); http.Dispose(); };
    }
    private async Task ReadInstalledAsync()
    {
        try
        {
            var ffmpeg = BatchViewModel.FindTool("ffmpeg"); var ffprobe = BatchViewModel.FindTool("ffprobe");
            FfmpegVersion.Text = ffmpeg is null ? "FFmpeg ausente" : await MediaToolUpdater.InstalledVersionAsync(ffmpeg, default);
            FfprobeVersion.Text = ffprobe is null ? "ffprobe ausente" : await MediaToolUpdater.InstalledVersionAsync(ffprobe, default);
        }
        catch (Exception ex) { ToolsStatus.Text = ex.Message; }
    }
    private async void CheckApp(object sender, RoutedEventArgs e)
    {
        if (preview || transfer is not null) return;
        CheckAppButton.IsEnabled = false;
        try
        {
            AppUpdateStatus.Text = "Consultando Releases…";
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
            var current = MediaToolUpdater.Active()?.Version ?? BundledVersion();
            bool newer = !Version.TryParse(current, out var installed) || Version.Parse(toolAsset.Version) > installed;
            ToolsStatus.Text = newer ? $"Conjunto estável disponível: {toolAsset.Version}" : $"Conjunto instalado atualizado ({current}).";
            InstallToolsButton.IsEnabled = newer;
        }
        catch (Exception ex) { ToolsStatus.Text = ex.Message; }
        finally { CheckToolsButton.IsEnabled = true; }
    }
    private static string? BundledVersion()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "tools", "manifest.json");
        if (!File.Exists(path)) return null;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)); return doc.RootElement.GetProperty("version").GetString();
    }
    private async void InstallApp(object sender, RoutedEventArgs e)
    {
        if (preview || available is null || transfer is not null) return;
        if (mediaBusy()) { AppUpdateStatus.Text = "Aguarde o fim do processamento antes de iniciar a atualização."; return; }
        var release = available;
        transfer = new(); SetTransfer(true);
        try
        {
            string directory = Path.Combine(AppLog.Root, "updates", Guid.NewGuid().ToString("N"));
            string file = Path.Combine(directory, release.Installer.Name);
            await releases.DownloadAsync(release.Installer, file, new Progress<double>(p => { DownloadProgress.Value = p; TransferStatus.Text = $"Baixando aplicativo • {p:0}%"; }), transfer.Token);
            if (mediaBusy()) { AppUpdateStatus.Text = "Download concluído. Aguarde o processamento terminar e tente novamente para instalar."; return; }
            using var verified = ReleaseClient.VerifyInstaller(file, release.Installer);
            var process = Process.Start(new ProcessStartInfo(file) { Arguments = "/S /RESTART", UseShellExecute = true });
            if (process is null) throw new IOException("O instalador não iniciou.");
            process.Dispose(); TransferStatus.Text = "Instalador iniciado. O aplicativo será fechado e reaberto.";
        }
        catch (OperationCanceledException) { TransferStatus.Text = "Download cancelado."; }
        catch (Exception ex) { TransferStatus.Text = ex.Message; }
        finally { transfer.Dispose(); transfer = null; SetTransfer(false); }
    }
    private async void InstallTools(object sender, RoutedEventArgs e)
    {
        if (preview || toolAsset is null || transfer is not null) return;
        if (mediaBusy()) { ToolsStatus.Text = "Aguarde o fim do processamento para atualizar as ferramentas."; return; }
        transfer = new(); SetTransfer(true);
        try
        {
            await new MediaToolUpdater(releases).InstallAsync(toolAsset, new Progress<double>(p => { DownloadProgress.Value = p; TransferStatus.Text = $"Baixando ferramentas • {p:0}%"; }), transfer.Token);
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
        CheckAppButton.IsEnabled = CheckToolsButton.IsEnabled = !busy;
        InstallAppButton.IsEnabled = !busy && available is not null; InstallToolsButton.IsEnabled = !busy && toolAsset is not null;
        DownloadProgress.Visibility = CancelTransfer.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
    private void CancelDownload(object sender, RoutedEventArgs e) => transfer?.Cancel();
}
