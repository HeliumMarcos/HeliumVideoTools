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
    public MainWindow()
    {
        InitializeComponent(); DataContext = model;
        model.Files.CollectionChanged += (_, _) => { DropHint.Visibility = model.Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed; UpdateEstimate(); };
        BuildOptions();
        Status.Text = BatchViewModel.FindTool("ffmpeg") is not null && BatchViewModel.FindTool("ffprobe") is not null ? "Pronto • FFmpeg e ffprobe detectados" : "Ferramentas não encontradas • coloque FFmpeg e ffprobe na pasta tools";
    }
    private void Navigate(object sender, RoutedEventArgs e)
    {
        operation = Enum.Parse<Operation>((string)((Button)sender).Tag);
        Heading.Text = operation switch { Operation.Split => "Dividir vídeo", Operation.Webm => "Converter para WebM", _ => "Compactar vídeos" };
        Subtitle.Text = operation switch { Operation.Split => "Divida por quantidade, duração ou tamanho. Cortes sem recodificação dependem dos quadros-chave.", Operation.Webm => "Prepare mídias para o SEEU, com tamanho final verificado.", _ => "Prepare vídeos de audiências para o Projudi, com tamanho final verificado." };
        RunButton.Content = operation switch { Operation.Split => "Dividir", Operation.Webm => "Converter", _ => "Compactar" };
        foreach (var nav in new[] { SplitNav, CompressNav, WebmNav }) nav.Background = new SolidColorBrush(nav == sender ? Color.FromRgb(59, 59, 59) : Color.FromRgb(48, 48, 48));
        BuildOptions();
    }
    private ComboBox Choice(params string[] items)
    {
        var box = new ComboBox { ItemsSource = items, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        box.SelectionChanged += (_, _) => UpdateEstimate(); return box;
    }
    private void Row(string label, UIElement field)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(150) }); grid.ColumnDefinitions.Add(new());
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        Grid.SetColumn(field, 1); grid.Children.Add(field); OptionsPanel.Children.Add(grid);
        if (field is FrameworkElement element) System.Windows.Automation.AutomationProperties.SetName(element, label);
    }
    private void BuildOptions()
    {
        OptionsPanel.Children.Clear(); mode = preset = codec = unit = null; target = null; parts = null; copyAudio = precise = null;
        if (operation == Operation.Split)
        {
            mode = Choice("Tamanho por parte • Projudi", "Número de partes", "Duração em minutos");
            Row("Dividir por", mode);
            parts = Choice(Enumerable.Range(2, 9).Select(n => $"{n} partes").ToArray());
            Row("Quantidade", parts); parts.IsEnabled = false;
            mode.SelectionChanged += (_, _) => { parts.IsEnabled = mode.SelectedIndex == 1; if (target != null) { target.IsEnabled = mode.SelectedIndex != 1; target.Text = mode.SelectedIndex == 2 ? "5" : "98"; } if (unit != null) unit.IsEnabled = mode.SelectedIndex == 0; };
        }
        if (operation == Operation.Compress) { preset = Choice("Personalizada • tamanho definido por você", "Baixa compressão • mais qualidade", "Média • equilibrada", "Alta compressão • menor arquivo"); Row("Preset", preset); }
        target = new TextBox { Text = operation == Operation.Webm ? "299" : "98" };
        target.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsAsciiDigit);
        DataObject.AddPastingHandler(target, (_, e) => { if (e.DataObject.GetData(DataFormats.UnicodeText) is not string value || !value.All(char.IsAsciiDigit)) e.CancelCommand(); });
        target.TextChanged += (_, _) => UpdateEstimate();
        unit = Choice("MB", "KB", "GB"); unit.Width = 92;
        var size = new Grid(); size.ColumnDefinitions.Add(new()); size.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        target.Margin = new Thickness(0, 0, 12, 0); size.Children.Add(target); Grid.SetColumn(unit, 1); size.Children.Add(unit);
        Row(operation == Operation.Split ? "Tamanho / minutos" : operation == Operation.Webm ? "Limite por arquivo" : "Tamanho final", size);
        if (operation != Operation.Split)
        {
            codec = operation == Operation.Webm ? Choice("VP8 • conversão mais rápida", "VP9 • melhor compressão") : Choice("H.264 • maior compatibilidade", "H.265 • arquivo menor"); Row("Codec", codec);
            if (operation == Operation.Compress) { copyAudio = new CheckBox { Content = "Manter áudio original", IsChecked = true }; Row("Áudio", copyAudio); }
            precise = new CheckBox { Content = "Modo preciso em 2 etapas", IsChecked = true }; Row("Precisão", precise);
        }
        estimate = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 190)), Padding = new Thickness(0, 6, 0, 6) }; Row("Estimativa", estimate);
        var note = new TextBlock { Text = "MB decimal (1.000.000 bytes). O tamanho é conferido antes de publicar cada resultado. A aceitação também depende dos demais requisitos do sistema de destino.", Foreground = Brushes.Silver, FontSize = 12 }; OptionsPanel.Children.Add(note);
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
