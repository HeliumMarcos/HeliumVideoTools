using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HeliumVideoTools.Core;
using HeliumVideoTools.Desktop;

internal static class OperationEditorTests
{
    private static IEnumerable<DependencyObject> Walk(DependencyObject node)
    {yield return node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Walk(VisualTreeHelper.GetChild(node,i)))yield return child;}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object? Invoke(MainWindow window,string method,params object[] args)=>typeof(MainWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,args);
    private static VideoOptions Read(MainWindow window)=>(VideoOptions)Invoke(window,"CurrentOptions")!;
    internal static void Run()
    {
        var app=new App {ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
        var window=new MainWindow(preview:true);window.Show();window.UpdateLayout();
        FrameworkElement Panel()=>(FrameworkElement)window.FindName("OptionsPanel");
        T Field<T>(string label) where T:FrameworkElement=>Walk(Panel()).OfType<T>().Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)==label);
        TextBox Target()=>Walk(Panel()).OfType<TextBox>().Single();
        void Navigate(string control){((Button)window.FindName(control)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();}
        Check(Read(window) is {Operation:Operation.Compress,TargetMb:98,Codec:"h264",CopyAudio:true,Preset:"custom"},"Padrões de compactação alterados");
        Target().Text="900";Field<ComboBox>("Unidade do tamanho").SelectedIndex=1;Check(Read(window).TargetMb==.9,"Unidade KB incorreta");
        Target().Text="2";Field<ComboBox>("Unidade do tamanho").SelectedIndex=2;Check(Read(window).TargetMb==2000,"Unidade GB incorreta");
        Target().Text="";
        try{Read(window);throw new Exception("Campo vazio aceito");}catch(TargetInvocationException ex) when(ex.InnerException is IOException){}
        Field<ComboBox>("Preset").SelectedIndex=2;Check(Read(window) is {Preset:"medium",TargetMb:98},"Preset não ignora alvo personalizado oculto");
        Field<ComboBox>("Codec").SelectedIndex=1;Check(Read(window).Codec=="h265","Codec de compactação incorreto");
        Field<CheckBox>("Áudio: Manter áudio original").IsChecked=false;
        Field<CheckBox>("Precisão: Modo preciso em 2 etapas").IsChecked=false;
        Invoke(window,"RememberToolPreferences");
        Navigate("WebmNav");Check(Read(window) is {Operation:Operation.Webm,TargetMb:299,Codec:"vp8"},"Padrões WebM alterados");
        Field<ComboBox>("Codec").SelectedIndex=1;Check(Read(window).Codec=="vp9","VP9 incorreto");
        Navigate("SplitNav");Check(Read(window) is {Operation:Operation.Split,SplitMode:SplitMode.Size,SplitValue:98},"Padrões da divisão alterados");
        Field<ComboBox>("Dividir por").SelectedIndex=1;
        var parts=Field<ComboBox>("Quantidade");Check(parts.Items.Count==9,"Faixa de partes alterada");parts.SelectedIndex=8;Check(Read(window).SplitValue==10,"Divisão em 10 partes incorreta");
        Field<ComboBox>("Dividir por").SelectedIndex=2;Target().Text="8";Check(Read(window) is {SplitMode:SplitMode.Duration,SplitValue:8},"Duração incorreta");
        Invoke(window,"RememberToolPreferences");
        Navigate("CompressNav");Check(Read(window) is {TargetMb:98,Codec:"h264",CopyAudio:true,TwoPass:false,Preset:"custom"},"Padrões protegidos/preferência de precisão alterados");
        Navigate("SplitNav");Field<ComboBox>("Dividir por").SelectedIndex=1;Check(Read(window).SplitValue==10,"Quantidade lembrada incorretamente");
        Field<ComboBox>("Dividir por").SelectedIndex=2;Check(Read(window).SplitValue==8,"Minutos lembrados incorretamente");
        var result=new BatchResult([new("a.mp4",BatchItemState.Succeeded,["a-out.mp4"]),new("b.mp4",BatchItemState.Failed,[],"erro")],false);
        Invoke(window,"ShowBatchResult",result);
        Check(((TextBlock)window.FindName("ProgressStatus")).Text=="1 concluído • 1 com erro","Contagem do resultado alterada");
        Check(((TextBlock)window.FindName("ProgressHeading")).Text=="Resultado do lote","Título do resultado alterado");
        Invoke(window,"ShowBatchResult",new BatchResult([new("a.mp4",BatchItemState.Cancelled,[]),new("b.mp4",BatchItemState.NotStarted,[])],true));
        Check(((TextBlock)window.FindName("ProgressStatus")).Text=="0 concluídos • 1 cancelado • 1 não iniciado","Resultado cancelado alterado");
        window.Close();
        foreach(var (items,cancelled,color) in new[]{
            (new[]{new BatchItemResult("a",BatchItemState.Succeeded,[])},false,"Success"),
            (new[]{new BatchItemResult("a",BatchItemState.Failed,[])},false,"Error"),
            (new[]{new BatchItemResult("a",BatchItemState.Succeeded,[]),new BatchItemResult("b",BatchItemState.Failed,[])},false,"Warning"),
            (new[]{new BatchItemResult("a",BatchItemState.Cancelled,[])},true,"TextSecondary")})
        {
            var presentation=BatchResultPresentation.From(new(items,cancelled),null);
            Check(presentation.ColorResource==color && presentation.CompleteProgress==!cancelled,"Apresentação de estado alterada");
            var warning=BatchResultPresentation.From(new(items,cancelled),"relatório indisponível");
            Check(warning.Details=="relatório indisponível" && warning.Status==warning.Details,"Aviso de relatório não preservado");
        }
        Console.WriteLine("PASS: configuração das 3 operações, unidades, presets, codecs, validação, preferências protegidas e textos de resultado preservados.");
    }
}
