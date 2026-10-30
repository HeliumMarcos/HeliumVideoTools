using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Desktop;
public partial class MainWindow : Window
{
    private readonly BatchViewModel model = new();
    private Operation operation = Operation.Compress;
    private ComboBox? mode, preset, codec, unit;
    private TextBox? target;
    private ComboBox? parts;
    private CheckBox? copyAudio, precise;
    private TextBlock? estimate;
    private bool closing;
    private UpdatesWindow? updates;
    private AppRelease? availableRelease;
    private readonly CancellationTokenSource lifetime = new();
    private bool updateChecked;
    public MainWindow()
    {
        InitializeComponent(); DataContext = model;
        model.Files.CollectionChanged += (_, _) => { DropHint.Visibility = model.Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed; FileCount.Text = model.Files.Count == 0 ? "Nenhum arquivo" : $"{model.Files.Count} arquivo(s)"; UpdateEstimate(); };
        BuildOptions();
        try { Status.Text = BatchViewModel.FindTool("ffmpeg") is not null && BatchViewModel.FindTool("ffprobe") is not null ? "Pronto • FFmpeg e ffprobe detectados" : "Ferramentas não encontradas • abra Atualizações para instalar o conjunto"; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { Status.Text = "Conjunto de ferramentas indisponível. Abra Atualizações para restaurar o anterior."; AppLog.Write("WARNING", "Ferramentas na abertura", ex.Message); }
        ContentRendered += async (_, _) =>
        {
            if (updateChecked) return; updateChecked = true;
            try
            {
                using var http = ReleaseClient.CreateHttpClient();
                availableRelease = await new ReleaseClient(http).CheckAppAsync(typeof(MainWindow).Assembly.GetName().Version!, lifetime.Token);
                if (availableRelease is not null)
                {
                    UpdatesLabel.Text = "Atualizações • nova";
                    UpdatesButton.ToolTip = "Versão " + availableRelease.Version.ToString(3) + " disponível para baixar e instalar";
                    if (!model.Busy) Status.Text = "Nova versão disponível. Abra Atualizações para baixar e instalar.";
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { AppLog.Write("INFO", "Consulta de atualização indisponível", ex.Message); }
        };
        Closed += (_, _) => lifetime.Cancel();
    }
    private void ShowUpdates(object sender, RoutedEventArgs e)
    {
        if (updates is { IsLoaded: true }) { updates.Activate(); return; }
        updates = new UpdatesWindow(() => model.Busy, available: availableRelease) { Owner = this };
        updates.Show();
    }
    private void Navigate(object sender, RoutedEventArgs e)
    {
        operation = Enum.Parse<Operation>((string)((Button)sender).Tag);
        Heading.Text = operation switch { Operation.Split => "Dividir vídeo", Operation.Webm => "Converter para WebM", _ => "Compactar vídeos" };
        Subtitle.Text = operation switch { Operation.Split => "Crie partes por tamanho, quantidade ou duração, preservando o arquivo original.", Operation.Webm => "Converta para o SEEU. O limite é um teto: arquivos menores não precisam aumentar.", _ => "Reduza o tamanho para envio ao Projudi. O arquivo final é conferido antes de salvar." };
        RunLabel.Text = operation switch { Operation.Split => "Dividir", Operation.Webm => "Converter", _ => "Compactar" };
        RunIcon.Kind = operation switch { Operation.Split => "split", Operation.Webm => "convert", _ => "compress" };
        System.Windows.Automation.AutomationProperties.SetName(RunButton, RunLabel.Text + " vídeos agora");
        DestinationLabel.Text = operation == Operation.Webm ? "SEEU • WEBM" : operation == Operation.Split ? "PARTES PARA ENVIO" : "PROJUDI • MP4";
        foreach (var nav in new[] { SplitNav, CompressNav, WebmNav })
        {
            nav.Background = nav == sender ? new SolidColorBrush(Color.FromRgb(53, 61, 68)) : Brushes.Transparent;
            nav.BorderBrush = nav == sender ? new SolidColorBrush(Color.FromRgb(108, 204, 240)) : Brushes.Transparent;
            if (nav.Content is StackPanel content)
                foreach (var text in content.Children.OfType<TextBlock>()) text.FontWeight = nav == sender ? FontWeights.SemiBold : FontWeights.Normal;
        }
        BuildOptions();
    }
    private ComboBox Choice(params string[] items)
    {
        var box = new ComboBox { ItemsSource = items, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        box.SelectionChanged += (_, _) => UpdateEstimate(); return box;
    }
    private Grid Row(string label, UIElement field)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 9) };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(164) }); grid.ColumnDefinitions.Add(new());
        var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        string symbol = label switch { "Preset" => "preset", "Codec" => "codec", "Áudio" => "audio", "Precisão" => "shield", "Estimativa" => "estimate", "Dividir por" => "split", "Quantidade" => "parts", _ => "size" };
        labelPanel.Children.Add(new Symbol { Kind = symbol, Width = 17, Height = 17, Foreground = Brushes.Silver, Margin = new Thickness(0, 0, 9, 0) });
        labelPanel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
        grid.Children.Add(labelPanel);
        Grid.SetColumn(field, 1); grid.Children.Add(field); OptionsPanel.Children.Add(grid);
        if (field is FrameworkElement element) System.Windows.Automation.AutomationProperties.SetName(element, label);
        return grid;
    }
    private void BuildOptions()
    {
        estimate = null; OptionsPanel.Children.Clear(); mode = preset = codec = unit = null; target = null; parts = null; copyAudio = precise = null;
        Grid? quantityRow = null;
        if (operation == Operation.Split)
        {
            mode = Choice("Tamanho por parte • Projudi", "Número de partes", "Duração em minutos");
            Row("Dividir por", mode);
            parts = Choice(Enumerable.Range(2, 9).Select(n => $"{n} partes").ToArray());
            quantityRow = Row("Quantidade", parts);
        }
        if (operation == Operation.Compress) { preset = Choice("Personalizada • tamanho definido por você", "Baixa compressão • mais qualidade", "Média • equilibrada", "Alta compressão • menor arquivo"); Row("Preset", preset); }
        target = new TextBox { Text = operation == Operation.Webm ? "299" : "98" };
        System.Windows.Automation.AutomationProperties.SetName(target, operation == Operation.Split ? "Tamanho por parte ou duração em minutos" : "Tamanho de saída por arquivo");
        target.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsAsciiDigit);
        DataObject.AddPastingHandler(target, (_, e) => { if (e.DataObject.GetData(DataFormats.UnicodeText) is not string value || !value.All(char.IsAsciiDigit)) e.CancelCommand(); });
        target.TextChanged += (_, _) => UpdateEstimate();
        unit = Choice("MB", "KB", "GB"); unit.Width = 92;
        System.Windows.Automation.AutomationProperties.SetName(unit, "Unidade do tamanho");
        var size = new Grid(); size.ColumnDefinitions.Add(new()); size.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        target.Margin = new Thickness(0, 0, 12, 0); size.Children.Add(target); Grid.SetColumn(unit, 1); size.Children.Add(unit);
        var sizeRow = Row(operation == Operation.Split ? "Tamanho por parte" : operation == Operation.Webm ? "Limite por arquivo" : "Tamanho final", size);
        if (mode is not null)
        {
            void UpdateSplitFields()
            {
                bool byParts = mode.SelectedIndex == 1;
                quantityRow!.Visibility = byParts ? Visibility.Visible : Visibility.Collapsed;
                sizeRow.Visibility = byParts ? Visibility.Collapsed : Visibility.Visible;
                unit.Visibility = mode.SelectedIndex == 2 ? Visibility.Collapsed : Visibility.Visible;
                target.Text = mode.SelectedIndex == 2 ? "5" : "98";
                ((StackPanel)sizeRow.Children[0]).Children.OfType<TextBlock>().Single().Text = mode.SelectedIndex == 2 ? "Duração (minutos)" : "Tamanho por parte";
                System.Windows.Automation.AutomationProperties.SetName(target, mode.SelectedIndex == 2 ? "Duração em minutos de cada parte" : "Tamanho de cada parte");
                UpdateEstimate();
            }
            mode.SelectionChanged += (_, _) => UpdateSplitFields();
            UpdateSplitFields();
        }
        if (preset is not null) preset.SelectionChanged += (_, _) => { sizeRow.Visibility = preset.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed; };
        if (operation != Operation.Split)
        {
            codec = operation == Operation.Webm ? Choice("VP8 • conversão mais rápida", "VP9 • melhor compressão") : Choice("H.264 • maior compatibilidade", "H.265 • arquivo menor"); Row("Codec", codec);
            if (operation == Operation.Compress) { copyAudio = new CheckBox { Content = "Manter áudio original", IsChecked = true }; Row("Áudio", copyAudio); }
            precise = new CheckBox { Content = "Modo preciso em 2 etapas", IsChecked = true }; Row("Precisão", precise);
        }
        estimate = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(177, 218, 237)), Padding = new Thickness(12, 9, 12, 9), FontSize = 12 };
        Row("Estimativa", new Border { Background = new SolidColorBrush(Color.FromRgb(39, 53, 63)), CornerRadius = new CornerRadius(6), Child = estimate });
        var note = new TextBlock { Text = operation == Operation.Webm ? "O arquivo final respeita o teto escolhido e um orçamento menor que o original." : operation == Operation.Split ? "Cortes rápidos dependem dos quadros-chave. O tamanho é conferido antes de salvar." : "O tamanho é conferido em MB decimais antes de salvar o resultado.", Foreground = Brushes.Silver, FontSize = 11 }; OptionsPanel.Children.Add(note);
        UpdateEstimate();
    }
    private double SizeMb()
    {
        if (!double.TryParse(target?.Text, NumberStyles.None, CultureInfo.InvariantCulture, out double value) || value <= 0 || value > 999999999) throw new IOException("Digite um tamanho inteiro maior que zero.");
        return value * (unit?.SelectedIndex switch { 1 => .001, 2 => 1000, _ => 1 });
    }
    private void UpdateEstimate()
    {
        if (estimate is null) return;
        if (model.Files.Count == 0) { estimate.Text = "Adicione vídeos para estimar o tamanho final"; return; }
        try
        {
            var sizes = model.Files.Select(f => new FileInfo(f.Path).Length / 1000000d).ToArray();
            if (operation == Operation.Split && mode?.SelectedIndex == 1) estimate.Text = $"≈ {sizes.Sum() / (parts!.SelectedIndex + 2):0.0} MB por parte no conjunto selecionado";
            else if (operation == Operation.Split && mode?.SelectedIndex == 2) estimate.Text = "O tamanho depende da duração e da taxa de bits de cada vídeo.";
            else estimate.Text = $"Limite: {SizeMb():0.###} MB por arquivo • estimativa total ≈ {sizes.Sum(s => Math.Min(s, SizeMb())):0.0} MB";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { estimate.Text = ex.Message; }
    }
    private void Browse(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "Vídeos|*.mp4;*.mkv;*.mov;*.avi;*.m4v;*.webm" }; if (dialog.ShowDialog() == true) model.Add(dialog.FileNames); }
    private void Remove(object sender, RoutedEventArgs e) { foreach (var item in FilesList.SelectedItems.Cast<InputVideo>().ToArray()) model.Files.Remove(item); }
    private void Clear(object sender, RoutedEventArgs e) => model.Files.Clear();
    private void FilesDropped(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) model.Add(paths); e.Handled = true; }
    private void FilesDragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void Run(object sender, RoutedEventArgs e)
    {
        if (model.Busy) return;
        if (updates?.Installing == true) { Status.Text = "Instalação da atualização iniciada. Aguarde a reabertura do aplicativo."; return; }
        try
        {
            var options = new VideoOptions(operation, SizeMb(), operation == Operation.Webm ? codec?.SelectedIndex == 1 ? "vp9" : "vp8" : codec?.SelectedIndex == 1 ? "h265" : "h264", copyAudio?.IsChecked != false, precise?.IsChecked == true,
                mode?.SelectedIndex switch { 1 => SplitMode.Parts, 2 => SplitMode.Duration, _ => SplitMode.Size }, mode?.SelectedIndex == 1 ? parts!.SelectedIndex + 2 : mode?.SelectedIndex == 2 ? double.Parse(target!.Text) : SizeMb(), preset?.SelectedIndex switch { 1 => "low", 2 => "medium", 3 => "high", _ => "custom" });
            ProgressPanel.Visibility = Visibility.Visible; ProgressAction.Content = "Cancelar tudo"; ProgressAction.IsEnabled = true; OverallBar.Value = 0;
            RunButton.IsEnabled = false; OptionsPanel.IsEnabled = false;
            var count = await model.RunAsync(options, new Progress<VideoProgress>(p => { OverallBar.Value = p.Percent; ProgressStatus.Text = p.Status; }));
            OverallBar.Value = 100; ProgressStatus.Text = $"{count} arquivo(s) concluído(s) • tamanhos verificados"; Status.Text = "Processamento concluído";
        }
        catch (OperationCanceledException) { ProgressPanel.Visibility = Visibility.Collapsed; Status.Text = "Processamento cancelado. Os originais foram preservados."; }
        catch (Exception ex) { AppLog.Write("ERROR", "Processamento", ex.ToString()); Status.Text = ex is IOException ? ex.Message : "Não foi possível concluir. Consulte o log para detalhes."; ProgressStatus.Text = Status.Text; }
        finally { RunButton.IsEnabled = true; OptionsPanel.IsEnabled = true; ProgressAction.Content = "Abrir pasta"; ProgressAction.IsEnabled = model.LastOutputFolder is not null; }
    }
    private void ProgressActionClicked(object sender, RoutedEventArgs e)
    {
        if (model.Busy) { model.Cancel(); ProgressAction.IsEnabled = false; Status.Text = "Cancelando…"; }
        else if (model.LastOutputFolder is { } folder) Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }
    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (!model.Busy || closing) return;
        e.Cancel = true;
        if (MessageBox.Show(this, "Cancelar o processamento e fechar?", "Helium Vídeo Tools", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        model.Cancel(); while (model.Busy) await Task.Delay(50); closing = true; Close();
    }
}
