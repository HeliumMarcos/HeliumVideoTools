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
    private CancellationTokenSource? transfer;
    private string? verifiedDownload;
    public bool Installing { get; private set; }
    public UpdatesWindow(Func<bool>? mediaBusy = null, bool preview = false, AppRelease? available = null)
    {
        InitializeComponent(); this.preview = preview; this.mediaBusy = mediaBusy ?? (() => false); releases = new(http);
        AppVersion.Text = "Instalado: " + typeof(UpdatesWindow).Assembly.GetName().Version!.ToString(3) + " • C# / .NET 10";
        this.available = available;
        if (available is not null) { AppUpdateStatus.Text = "Nova versão disponível: " + available.Version.ToString(3); InstallAppButton.IsEnabled = true; }
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
            var ffmpeg = System.Text.RegularExpressions.Regex.Match(FfmpegVersion.Text, @"version ([0-9]+(?:\.[0-9]+){1,2})");
            var ffprobe = System.Text.RegularExpressions.Regex.Match(FfprobeVersion.Text, @"version ([0-9]+(?:\.[0-9]+){1,2})");
            bool newer = !ffmpeg.Success || !ffprobe.Success || Version.Parse(ffmpeg.Groups[1].Value) < Version.Parse(toolAsset.Version) || Version.Parse(ffprobe.Groups[1].Value) < Version.Parse(toolAsset.Version);
            ToolsStatus.Text = newer ? $"Conjunto estável disponível: {toolAsset.Version}" : $"Conjunto instalado atualizado; versão estável disponível: {toolAsset.Version}.";
            InstallToolsButton.IsEnabled = newer;
        }
        catch (Exception ex) { ToolsStatus.Text = ex.Message; }
        finally { CheckToolsButton.IsEnabled = true; }
    }
    private async void InstallApp(object sender, RoutedEventArgs e)
    {
        if (preview || available is null || transfer is not null) return;
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
            using var verified = await Task.Run(() => ReleaseClient.VerifyInstaller(file, release.Installer), transfer.Token);
            transfer.Token.ThrowIfCancellationRequested();
            if (mediaBusy()) { AppUpdateStatus.Text = "Instalador verificado. Aguarde o processamento terminar para instalar."; return; }
            Installing = true;
            var process = Process.Start(new ProcessStartInfo(file) { Arguments = "/S /RESTART", UseShellExecute = true });
            if (process is null) throw new IOException("O instalador não iniciou.");
            process.Dispose(); TransferStatus.Text = "Instalador iniciado. O aplicativo será fechado e reaberto.";
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
