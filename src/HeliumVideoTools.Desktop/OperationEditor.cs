using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Desktop;

// Owns the operation controls and their mapping, without starting jobs or processes.
internal sealed class OperationEditor(StackPanel panel,Action changed)
{
    private Operation operation;
    private UserPreferences preferences=new();
    private ComboBox? mode,preset,codec,unit,parts;
    private TextBox? target;
    private CheckBox? copyAudio,precise;
    private TextBlock? estimate,estimateNote;
    private bool buildingOptions;
    internal bool IsBuilding=>buildingOptions;
    internal TextBox Target=>target!;
    internal ComboBox Unit=>unit!;
    internal TextBlock? Estimate=>estimate;
    internal TextBlock? EstimateNote=>estimateNote;
    private void UpdateEstimate()=>changed();
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
        var rowIcon=new Symbol {Kind=symbol,Width=17,Height=17,Margin=new Thickness(0,0,9,0)};rowIcon.SetResourceReference(Control.ForegroundProperty,"TextSecondary");labelPanel.Children.Add(rowIcon);
        labelPanel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
        grid.Children.Add(labelPanel);
        Grid.SetColumn(field, 1); grid.Children.Add(field); panel.Children.Add(grid);
        if (field is FrameworkElement element) System.Windows.Automation.AutomationProperties.SetName(element,field is CheckBox check?label+": "+check.Content:label);
        return grid;
    }
    internal void Build(Operation operation,UserPreferences preferences)
    {
        this.operation=operation;this.preferences=preferences;buildingOptions=true;
        estimate = estimateNote = null; panel.Children.Clear(); mode = preset = codec = unit = null; target = null; parts = null; copyAudio = precise = null;
        var defaults=StartupDefaults.For(operation,preferences);
        Grid? quantityRow = null;
        if (operation == Operation.Split)
        {
            mode = Choice("Tamanho por parte • Projudi", "Número de partes", "Duração em minutos");
            Row("Dividir por", mode);
            parts = Choice(Enumerable.Range(2, 9).Select(n => $"{n} partes").ToArray());
            parts.SelectedIndex=preferences.SplitParts-2;
            quantityRow = Row("Quantidade", parts);
        }
        if (operation == Operation.Compress) { preset = Choice("Personalizada • tamanho definido por você", "Baixa compressão • mais qualidade", "Média • equilibrada", "Alta compressão • menor arquivo"); Row("Preset", preset); }
        target = new TextBox { Text = defaults.TargetMb.ToString("0",CultureInfo.InvariantCulture) };
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
                target.Text = mode.SelectedIndex == 2 ? preferences.SplitMinutes.ToString(CultureInfo.InvariantCulture) : "98";
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
            if (operation == Operation.Compress) { copyAudio = new CheckBox { Content = "Manter áudio original", IsChecked = true }; copyAudio.Checked+=(_,_)=>UpdateEstimate(); copyAudio.Unchecked+=(_,_)=>UpdateEstimate(); Row("Áudio", copyAudio); }
            precise = new CheckBox { Content = "Modo preciso em 2 etapas", IsChecked = defaults.TwoPass }; precise.Checked+=(_,_)=>UpdateEstimate(); precise.Unchecked+=(_,_)=>UpdateEstimate(); Row("Precisão", precise);
        }
        estimate = new TextBlock { FontSize = 12 };estimate.SetResourceReference(TextBlock.ForegroundProperty,"InfoText");
        estimateNote=new TextBlock {FontSize=11,Margin=new Thickness(0,6,0,0),Visibility=Visibility.Collapsed };estimateNote.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");
        var estimateContent=new StackPanel { Margin=new Thickness(12,9,12,9),MinHeight=34 }; estimateContent.Children.Add(estimate); estimateContent.Children.Add(estimateNote);
        var estimateBox=new Border {CornerRadius=new CornerRadius(6),Child=estimateContent};estimateBox.SetResourceReference(Border.BackgroundProperty,"InfoBackground");Row("Estimativa",estimateBox);
        var note = new TextBlock { Text = operation == Operation.Webm ? "O arquivo final respeita o teto escolhido e um orçamento menor que o original." : operation == Operation.Split ? "Divisão precisa em MP4 (H.264/AAC), com recodificação. Os originais são preservados." : "O tamanho é conferido em MB decimais antes de salvar o resultado.", FontSize = 11 };note.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");panel.Children.Add(note);
        buildingOptions=false; UpdateEstimate();
    }
    private double SizeMb()
    {
        if (!double.TryParse(target?.Text, NumberStyles.None, CultureInfo.InvariantCulture, out double value) || value <= 0 || value > 999999999) throw new IOException("Digite um tamanho inteiro maior que zero.");
        return value * (unit?.SelectedIndex switch { 1 => .001, 2 => 1000, _ => 1 });
    }
    internal VideoOptions Read()
    {
        double size=operation==Operation.Compress && preset?.SelectedIndex>0?98:SizeMb();
        return new(operation,size,operation==Operation.Webm ? codec?.SelectedIndex==1?"vp9":"vp8" : codec?.SelectedIndex==1?"h265":"h264",copyAudio?.IsChecked!=false,precise?.IsChecked==true,
            mode?.SelectedIndex switch {1=>SplitMode.Parts,2=>SplitMode.Duration,_=>SplitMode.Size},mode?.SelectedIndex==1?parts!.SelectedIndex+2:mode?.SelectedIndex==2?double.Parse(target!.Text,CultureInfo.InvariantCulture):size,preset?.SelectedIndex switch {1=>"low",2=>"medium",3=>"high",_=>"custom"});
    }
    internal UserPreferences Remember(UserPreferences preferences)
    {
        if(operation==Operation.Compress && precise is not null)preferences=preferences with {CompressionTwoPass=precise.IsChecked==true};
        if(operation==Operation.Webm && precise is not null)preferences=preferences with {WebmTwoPass=precise.IsChecked==true};
        if(operation==Operation.Split)
        {
            if(parts is {SelectedIndex:>=0})preferences=preferences with {SplitParts=parts.SelectedIndex+2};
            if(mode?.SelectedIndex==2 && int.TryParse(target?.Text,out int minutes) && minutes is >=1 and <=1440)preferences=preferences with {SplitMinutes=minutes};
        }
        return preferences;
    }
}
