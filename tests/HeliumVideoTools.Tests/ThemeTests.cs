using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;
using HeliumVideoTools.Desktop;

internal static class ThemeTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static readonly ContrastColors Black=new(Colors.Black,Colors.White,Colors.Black,Colors.White,Colors.Yellow,Colors.Black,Colors.Gray);
    private static readonly ContrastColors White=new(Colors.White,Colors.Black,Colors.White,Colors.Black,Colors.DarkBlue,Colors.White,Colors.Gray);
    private static IEnumerable<DependencyObject> Walk(DependencyObject node)
    {yield return node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Walk(VisualTreeHelper.GetChild(node,i)))yield return child;}
    private static Color ColorOf(object brush)=>((SolidColorBrush)brush).Color;
    private static double Contrast(Color a,Color b)
    {
        double Lum(Color c){double Channel(byte x){double n=x/255d;return n<=.04045?n/12.92:Math.Pow((n+.055)/1.055,2.4);}return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);}
        return (Math.Max(Lum(a),Lum(b))+.05)/(Math.Min(Lum(a),Lum(b))+.05);
    }
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var app=new App(isolated:true) {ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();ThemeManager.EnsureInitialized();
        foreach(bool light in new[]{false,true})
        {
            var p=ThemeManager.Palette(light,false);
            foreach(var (text,bg) in new[]{("TextPrimary","Surface"),("TextSecondary","Surface"),("ButtonText","ButtonBackground"),("OnAccent","Accent"),("InfoText","InfoBackground"),("SuccessText","SuccessBackground"),("Error","Surface"),("Warning","Surface")})
                Check(Contrast(p[text],p[bg])>=4.5,$"Contraste insuficiente: {light}/{text}/{bg}");
            Check(Contrast(p["Focus"],p["InputBackground"])>=3,"Foco sem contraste");
        }
        foreach(var colors in new[]{Black,White,new ContrastColors(Colors.Navy,Colors.White,Colors.Navy,Colors.White,Colors.Yellow,Colors.Black,Colors.Silver),new ContrastColors(Colors.Beige,Colors.Black,Colors.Beige,Colors.Black,Colors.DarkGreen,Colors.White,Colors.DimGray)})
        {
            var p=ThemeManager.Palette(false,true,colors);
            Check(p["AppBackground"]==colors.Background && p["OnAccent"]==colors.HighlightText && p["ButtonText"]==colors.ControlText && p["CheckedThumb"]==colors.HighlightText,"Tema ignorou cores pareadas do sistema");
        }
        foreach(string theme in new[]{"dark","light","contrast-black","contrast-white"})
        foreach(string operation in new[]{"Compress","Split","Webm"})
        foreach(var size in new[]{(1280,900),(980,700)})
        {
            ThemeManager.SetPreference(theme=="light"?"light":"dark");
            var window=new MainWindow(preview:true){Width=size.Item1,Height=size.Item2,ShowActivated=false};window.Show();
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
            if(theme.StartsWith("contrast"))ThemeManager.Apply(false,true,theme.EndsWith("black")?Black:White);
            if(operation!="Compress")((Button)window.FindName(operation=="Split"?"SplitNav":"WebmNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            var run=(Button)window.FindName("RunButton");var choice=(ComboBox)window.FindName("ThemeChoice");
            var bounds=run.TransformToAncestor((Visual)window.Content).TransformBounds(new Rect(run.RenderSize));
            Check(bounds.Bottom<=((FrameworkElement)window.Content).ActualHeight && bounds.Right<=((FrameworkElement)window.Content).ActualWidth,"Ação principal cortada");
            Check(choice.IsVisible && choice.ActualHeight>=34,"Seletor de aparência oculto ou menor que o padrão compacto");
            Check(new ButtonAutomationPeer(run).GetName().Contains(operation=="Compress"?"Compactar":operation=="Split"?"Dividir":"Converter"),"Nome acessível da ação incorreto");
            var target=window.Editor.Target;
            target.Focus();window.UpdateLayout();
            Check(target.FocusVisualStyle is not null && choice.FocusVisualStyle is not null,"Controle sem foco visual");
            Check(!string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(target)),"Campo sem nome acessível");
            var buttonText=Walk(run).OfType<TextBlock>().Single(t=>t.Name=="RunLabel");
            Check(ColorOf(buttonText.Foreground)==ColorOf(app.Resources["OnAccent"]),"Texto da ação não acompanha o tema");
            var combo=Walk(window).OfType<ComboBox>().First(c=>c!=choice && c.IsVisible);
            combo.IsDropDownOpen=true;Pump();window.UpdateLayout();
            var selected=(ComboBoxItem)combo.ItemContainerGenerator.ContainerFromIndex(combo.SelectedIndex);
            Check(selected is not null,"Menu não criou item selecionado");
            Check(ColorOf(selected!.Foreground)==ColorOf(app.Resources["OnAccent"]),"Seleção do menu sem cores pareadas");
            combo.IsDropDownOpen=false;
            combo.IsEnabled=false;Pump();window.UpdateLayout();
            Check(ColorOf(combo.Foreground)==ColorOf(app.Resources["DisabledText"]),$"Campo desabilitado não acompanha o tema: {theme}/{operation}/{size}");
            combo.IsEnabled=true;Pump();window.UpdateLayout();
            if(size.Item1==1280 && operation=="Compress")Save(window,Path.Combine(folder,theme+".png"));
            window.Close();
        }
        // Existing windows and controls must react without rebuilding options or losing values.
        var live=new MainWindow(preview:true);live.Show();
        live.Activate();Pump();
        var split=(Button)live.FindName("SplitNav");Keyboard.Focus(split);
        Check(split.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)) && Keyboard.FocusedElement==(Button)live.FindName("CompressNav"),"Ordem de Tab na navegação incorreta");
        var appearance=(ComboBox)live.FindName("ThemeChoice");appearance.SelectedIndex=1;live.UpdateLayout();
        Check(ThemeManager.Preference=="light" && ColorOf(live.Background)==ThemeManager.Palette(true,false)["AppBackground"],"Tema não mudou ao vivo");
        var field=live.Editor.Target;field.Text="97";
        Keyboard.Focus(field);Pump();live.UpdateLayout();
        Check(field.IsKeyboardFocusWithin,"Campo não recebeu foco por teclado");
        var face=(Border)field.Template.FindName("InputFace",field);
        Check(face.BorderThickness.Left==2 && ColorOf(face.BorderBrush)==ColorOf(app.Resources["Focus"]),"Foco do campo não ficou visível");
        appearance.SelectedIndex=2;live.UpdateLayout();Check(field.Text=="97" && ThemeManager.Preference=="dark","Troca de tema perdeu valor");
        live.Close();
        foreach(string theme in new[]{"dark","light","contrast-black"})
        foreach(string state in new[]{"idle","download","verify"})
        {
            ThemeManager.SetPreference(theme=="light"?"light":"dark");
            var updates=new UpdatesWindow(preview:true);updates.Show();
            if(theme.StartsWith("contrast"))ThemeManager.Apply(false,true,Black);
            if(state!="idle")
            {
                typeof(UpdatesWindow).GetMethod("SetTransfer",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(updates,[true]);
                var bar=(ProgressBar)updates.FindName("DownloadProgress");bar.Value=42;bar.IsIndeterminate=state=="verify";
            }
            updates.Dispatcher.Invoke(()=>updates.UpdateLayout(),DispatcherPriority.ContextIdle);
            var scroll=(ScrollViewer)updates.FindName("UpdatesScroll");scroll.InvalidateMeasure();updates.UpdateLayout();
            Check(scroll.ComputedVerticalScrollBarVisibility!=Visibility.Visible,"Atualizações ganhou rolagem");
            if(state=="download")Save(updates,Path.Combine(folder,"updates-"+theme+".png"));
            updates.Close();
        }
        var store=new UserPreferenceStore(Path.Combine(folder,"preferences"));
        Task.Run(()=>store.SaveAsync(new(Theme:"light"))).GetAwaiter().GetResult();Check(store.Load().Theme=="light","Tema não persistiu");
        Check(new UserPreferences(Theme:"invalid").Normalize().Theme=="system","Tema inválido não usa Sistema");
        Console.WriteLine("PASS: temas claro/escuro e duas paletas de contraste, 3 ferramentas em 2 tamanhos, menus/foco/nomes acessíveis, troca ao vivo, preferências e Atualizações sem rolagem em 3 estados.");
    }
    private static void Save(Window window,string path)
    {
        var visual=(FrameworkElement)window.Content;
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
    private static void Pump()
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(_,_)=>frame.Continue=false;timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
    }
}
