using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;

namespace HeliumVideoTools.Desktop;
public partial class MainWindow : Window
{
    private readonly BatchViewModel model = new();
    private Operation operation = Operation.Compress;
    internal OperationEditor Editor {get;}
    internal ResponsiveLayout? AdaptiveLayout {get;}
    private readonly ProgressAnnouncer progressAnnouncements;
    private readonly ProgressAnnouncer statusAnnouncements;
    private TextBlock? estimate=>Editor.Estimate;
    private TextBlock? estimateNote=>Editor.EstimateNote;
    private readonly EstimateCoordinator estimates=new();
    private readonly UserPreferenceStore? preferenceStore;
    private UserPreferences preferences;
    private bool preferredMaximized;
    private readonly WindowCloseCoordinator shutdown=new();
    private bool buildingOptions=>Editor.IsBuilding;
    private readonly UpdateWindowController updates;
    private readonly StartupUpdateCheck updateCheck=new();
    private readonly CancellationTokenSource lifetime = new();
    public MainWindow() : this(false) { }
    public MainWindow(bool preview):this(preview,true) { }
    internal MainWindow(bool preview,bool adaptive)
    {
        preferenceStore=preview?null:new UserPreferenceStore(AppLog.Root,LegacyPreferences.Read);
        preferences=preferenceStore?.Load()??new(); preferredMaximized=preferences.Maximized;
        ThemeManager.EnsureInitialized();
        if(!preview)ThemeManager.SetPreference(preferences.Theme);
        InitializeComponent(); NativeWindowTheme.Attach(this); DataContext = model;
        progressAnnouncements=new ProgressAnnouncer(ProgressStatus);
        System.Windows.Automation.AutomationProperties.SetLiveSetting(Status,System.Windows.Automation.AutomationLiveSetting.Polite);
        statusAnnouncements=new ProgressAnnouncer(Status);
        updates=new UpdateWindowController(this,()=>model.Busy || shutdown.Saving,()=>updateCheck.Available);
        Editor=new OperationEditor(OptionsPanel,UpdateEstimate);
        ThemeChoice.ItemsSource=new[]{"Sistema (Windows)","Claro","Escuro"};
        ThemeChoice.SelectedIndex=ThemeManager.Preference switch {"light"=>1,"dark"=>2,_=>0};
        void RefreshThemeHelp()=>ThemeHelp.Text=ThemeManager.HighContrast?"Contraste do Windows ativo":"Tab navega · Espaço alterna";
        EventHandler themeChanged=(_,_)=>RefreshThemeHelp();ThemeManager.Changed+=themeChanged;RefreshThemeHelp();
        Closed+=(_,_)=>ThemeManager.Changed-=themeChanged;
        UpdateShutdown.Attach(this);
        if(!preview)
        {
            var work=SystemParameters.WorkArea;
            if(adaptive){MinWidth=Math.Min(675,work.Width);MinHeight=Math.Min(329,work.Height);}
            if(work.Width>=MinWidth && work.Height>=MinHeight) { Width=Math.Clamp(preferences.Width,MinWidth,work.Width); Height=Math.Clamp(preferences.Height,MinHeight,work.Height); }
            if(preferences.Maximized) WindowState=WindowState.Maximized;
        }
        StateChanged+=(_,_)=>{if(WindowState!=WindowState.Minimized) preferredMaximized=WindowState==WindowState.Maximized;};
        ProductVersion.Text = typeof(MainWindow).Assembly.GetName().Version!.ToString(3) + " • .NET";
        model.Files.CollectionChanged += (_, _) => { DropHint.Visibility = model.Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed; FileCount.Text = model.Files.Count == 0 ? "Nenhum arquivo" : $"{model.Files.Count} arquivo(s)"; UpdateEstimate(); };
        BuildOptions();
        if(adaptive){AdaptiveLayout=new ResponsiveLayout(this);if(!preview)WindowWorkArea.Attach(this);}
        try { Status.Text = BatchViewModel.FindTool("ffmpeg") is not null && BatchViewModel.FindTool("ffprobe") is not null ? "Pronto • FFmpeg e ffprobe detectados" : "Ferramentas não encontradas • abra Atualizações para instalar o conjunto"; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { Status.Text = "Conjunto de ferramentas indisponível. Abra Atualizações para restaurar o anterior."; AppLog.Write("WARNING", "Ferramentas na abertura", ex.Message); }
        ContentRendered += async (_, _) =>
        {
            if(preview)return;
            await updateCheck.RunOnceAsync(App.StartupRecoveryTask,RefreshRecoveredTools,release=>
            {
                UpdatesLabel.Text="Atualizações • nova";
                UpdatesButton.ToolTip="Versão "+release.Version.ToString(3)+" disponível para baixar e instalar";
                if(!model.Busy)Status.Text="Nova versão disponível. Abra Atualizações para baixar e instalar.";
            },lifetime.Token);
        };
        Closed += (_, _) => lifetime.Cancel();
    }
    private void RefreshRecoveredTools()
    {
        if(model.Busy || !Status.Text.StartsWith("Conjunto de ferramentas indisponível",StringComparison.Ordinal))return;
        try{if(BatchViewModel.FindTool("ffmpeg") is not null && BatchViewModel.FindTool("ffprobe") is not null)Status.Text="Pronto • FFmpeg e ffprobe detectados";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){AppLog.Write("WARNING","Ferramentas ainda indisponíveis",ex.Message);}
    }
    private void ThemeChanged(object sender,SelectionChangedEventArgs e)
    {
        if(ThemeChoice.SelectedIndex<0)return;
        string theme=ThemeChoice.SelectedIndex switch {1=>"light",2=>"dark",_=>"system"};
        preferences=preferences with {Theme=theme};ThemeManager.SetPreference(theme);
    }
    private void ShowUpdates(object sender, RoutedEventArgs e)
        =>updates.Show();
    private void Navigate(object sender, RoutedEventArgs e)
    {
        RememberToolPreferences();
        operation = Enum.Parse<Operation>((string)((Button)sender).Tag);
        Heading.Text = operation switch { Operation.Split => "Dividir vídeo", Operation.Webm => "Converter para WebM", _ => "Compactar vídeos" };
        Subtitle.Text = operation switch { Operation.Split => "Crie partes por tamanho, quantidade ou duração, preservando o arquivo original.", Operation.Webm => "Converta para o SEEU. O limite é um teto: arquivos menores não precisam aumentar.", _ => "Reduza o tamanho para envio ao Projudi. O arquivo final é conferido antes de salvar." };
        RunLabel.Text = operation switch { Operation.Split => "Dividir", Operation.Webm => "Converter", _ => "Compactar" };
        RunIcon.Kind = operation switch { Operation.Split => "split", Operation.Webm => "convert", _ => "compress" };
        System.Windows.Automation.AutomationProperties.SetName(RunButton, RunLabel.Text + " vídeos agora");
        DestinationLabel.Text = operation == Operation.Webm ? "SEEU • WEBM" : operation == Operation.Split ? "PARTES PARA ENVIO" : "PROJUDI • MP4";
        foreach (var nav in new[] { SplitNav, CompressNav, WebmNav })
        {
            if(nav==sender) {nav.SetResourceReference(Control.BackgroundProperty,"SelectedBackground");nav.SetResourceReference(Control.BorderBrushProperty,"Icon");nav.SetResourceReference(Control.ForegroundProperty,"SelectedText");}
            else {nav.Background=Brushes.Transparent;nav.BorderBrush=Brushes.Transparent;nav.SetResourceReference(Control.ForegroundProperty,"TextPrimary");}
            if (nav.Content is StackPanel content)
            {
                foreach(var icon in content.Children.OfType<Symbol>())icon.SetResourceReference(Control.ForegroundProperty,nav==sender?"SelectedIcon":"Icon");
                foreach (var text in content.Children.OfType<TextBlock>()) text.FontWeight = nav == sender ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }
        BuildOptions();
    }
    private void BuildOptions()
    {estimates.Cancel();Editor.Build(operation,preferences);AdaptiveLayout?.Refresh();}
    private VideoOptions CurrentOptions()=>Editor.Read();
    private async void UpdateEstimate()
    {
        if (estimate is null || buildingOptions) return;
        estimates.Cancel();
        estimate.ToolTip=null; System.Windows.Automation.AutomationProperties.SetHelpText(estimate,"");
        if(model.Files.Count==0) {estimate.Text="Adicione vídeos para estimar o tamanho final";estimate.ToolTip=null;estimateNote!.Visibility=Visibility.Collapsed;return;}
        await estimates.RequestAsync(model.Files.Select(f=>f.Path).ToArray(),CurrentOptions,
            ()=>{estimate.Text="Analisando duração, áudio e tamanho…";estimateNote!.Visibility=Visibility.Collapsed;},
            presentation=>{
            estimate.Text=presentation.Summary; estimate.ToolTip=new TextBlock {Text=presentation.Details,MaxWidth=640};
            System.Windows.Automation.AutomationProperties.SetHelpText(estimate,presentation.Details);
            estimateNote!.Text=presentation.Explanation;estimateNote.Visibility=Visibility.Visible;
            },
            ex=>{estimate.Text=ex is IOException?ex.Message:"Não foi possível calcular a estimativa.";estimateNote!.Visibility=Visibility.Collapsed;AppLog.Write("INFO","Estimativa indisponível",ex.Message);},
            lifetime.Token);
    }
    private void RememberToolPreferences()=>preferences=Editor.Remember(preferences);
    private void Browse(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "Vídeos|*.mp4;*.mkv;*.mov;*.avi;*.m4v;*.webm",InitialDirectory=preferences.LastDirectory??"" }; if (dialog.ShowDialog() == true) {preferences=preferences with {LastDirectory=Path.GetDirectoryName(dialog.FileNames[0])};model.Add(dialog.FileNames);} }
    private void Remove(object sender, RoutedEventArgs e) { foreach (var item in FilesList.SelectedItems.Cast<InputVideo>().ToArray()) model.Files.Remove(item); }
    private void Clear(object sender, RoutedEventArgs e) => model.Files.Clear();
    private void FilesDropped(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) model.Add(paths); e.Handled = true; }
    private void FilesDragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void Run(object sender, RoutedEventArgs e)
    {
        if (model.Busy || shutdown.Saving) return;
        if (updates.Installing) { Status.Text = "Instalação da atualização iniciada. Aguarde a reabertura do aplicativo."; return; }
        try
        {
            progressAnnouncements.Reset();
            statusAnnouncements.Reset();
            var options = CurrentOptions();
            ProgressPanel.Visibility = Visibility.Visible; ProgressAction.Content = "Cancelar tudo"; ProgressAction.IsEnabled = true; OverallBar.Value = 0;
            ReportButton.Visibility = ResultDetails.Visibility = Visibility.Collapsed; ProgressAction.ContextMenu = null;
            ProgressHeading.Text = "Progresso geral"; SetProgressColor("Focus");
            RunButton.IsEnabled = false; OptionsPanel.IsEnabled = false;
            var result = await model.RunAsync(options, new Progress<VideoProgress>(p => { if (model.Busy && model.LastResult is null) { OverallBar.Value = p.Percent; ProgressStatus.Text = p.Status; progressAnnouncements.Notify(); } }));
            ShowBatchResult(result);
        }
        catch (OperationCanceledException) { ProgressPanel.Visibility = Visibility.Collapsed; Status.Text = "Processamento cancelado. Os originais foram preservados."; statusAnnouncements.Notify(final:true); }
        catch (Exception ex) { AppLog.Write("ERROR", "Processamento", ex.ToString()); Status.Text = ex is IOException ? ex.Message : "Não foi possível concluir. Consulte o log para detalhes."; ProgressStatus.Text = Status.Text; if(ProgressStatus.IsVisible)progressAnnouncements.Notify(final:true);else statusAnnouncements.Notify(final:true); }
        finally { RunButton.IsEnabled = true; OptionsPanel.IsEnabled = true; int folders = model.LastResult?.OutputFolders.Count ?? 0; ProgressAction.Content = folders > 1 ? $"Pastas de saída ({folders})" : "Abrir pasta"; ProgressAction.IsEnabled = model.LastOutputFolder is not null; }
    }
    private void SetProgressColor(string resource)
    {OverallBar.SetResourceReference(Control.ForegroundProperty,resource);ProgressIcon.SetResourceReference(Control.ForegroundProperty,resource);ProgressPanel.SetResourceReference(Border.BorderBrushProperty,resource);ProgressStatus.SetResourceReference(TextBlock.ForegroundProperty,resource);}
    private void ShowBatchResult(BatchResult result)
    {
        var presentation=BatchResultPresentation.From(result,model.ReportWarning);
        ProgressHeading.Text=presentation.Heading;ProgressStatus.Text=presentation.Counts;
        if(presentation.CompleteProgress)OverallBar.Value=100;
        SetProgressColor(presentation.ColorResource);
        ResultDetails.Text=presentation.Details;
        ResultDetails.Visibility = Visibility.Visible; ReportButton.Visibility = model.LastReportPath is null ? Visibility.Collapsed : Visibility.Visible;
        Status.Text=presentation.Status;
        progressAnnouncements.Notify(final:true);
    }
    private void OpenReport(object sender,RoutedEventArgs e)
    {
        if (model.LastReportPath is not { } path) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { AppLog.Write("WARNING","Abrir relatório",ex.Message); Status.Text = "Não foi possível abrir o relatório: " + path; }
    }
    private void OpenFolder(string path)
    {
        try { if (!Directory.Exists(path)) throw new IOException("Pasta não encontrada."); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { AppLog.Write("WARNING","Abrir pasta",ex.Message); Status.Text = "Não foi possível abrir a pasta: " + path; }
    }
    private void ProgressActionClicked(object sender, RoutedEventArgs e)
    {
        if (model.Busy) { model.Cancel(); ProgressAction.IsEnabled = false; Status.Text = "Cancelando…"; }
        else if (model.LastResult is { } result && result.OutputFolders.Count > 1)
        {
            var menu = new ContextMenu {MaxHeight = 360, PlacementTarget = ProgressAction };
            var template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='MenuItem'><Border Name='Face' Background='{DynamicResource Surface}' Padding='12,10'><ContentPresenter ContentSource='Header'/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource Accent}'/><Setter Property='Foreground' Value='{DynamicResource OnAccent}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Face' Property='BorderBrush' Value='{DynamicResource Focus}'/><Setter TargetName='Face' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            foreach (var folder in result.OutputFolders) { var item = new MenuItem { Header = new TextBlock { Text = folder, MaxWidth = 600, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap }, ToolTip = folder, Template = template }; item.Click += (_,_) => OpenFolder(folder); menu.Items.Add(item); }
            ProgressAction.ContextMenu = menu; menu.IsOpen = true;
        }
        else if (model.LastOutputFolder is { } folder) OpenFolder(folder);
    }
    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        await shutdown.HandleAsync(e,preferenceStore is null,()=>model.Busy,
            ()=>MessageBox.Show(this,"Cancelar o processamento e fechar?","Helium Vídeo Tools",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes,
            ()=>RunButton.IsEnabled=false,model.Cancel,async ()=>{
            RememberToolPreferences();
            var bounds=RestoreBounds;
            preferences=preferences with { Width=bounds.IsEmpty?Width:bounds.Width,Height=bounds.IsEmpty?Height:bounds.Height,Maximized=preferredMaximized };
            if(preferenceStore is not null)await preferenceStore.SaveAsync(preferences);
            },()=>{_ = Dispatcher.BeginInvoke(new Action(Close));});
    }
}
