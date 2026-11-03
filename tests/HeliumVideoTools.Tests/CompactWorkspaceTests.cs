using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HeliumVideoTools.Core;
using HeliumVideoTools.Desktop;

internal static class CompactWorkspaceTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static T Find<T>(MainWindow w,string name)=>(T)w.FindName(name);
    private static void Click(MainWindow w,string name)=>Find<Button>(w,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Busy(BatchViewModel model,bool value)=>typeof(BatchViewModel).GetProperty(nameof(model.Busy))!.SetValue(model,value);
    private static DragEventArgs Drag(UIElement target,IDataObject data,RoutedEvent routed)
    {
        var args=(DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),BindingFlags.Instance|BindingFlags.NonPublic,null,[data,DragDropKeyStates.None,DragDropEffects.Copy,target,new Point(1,1)],null)!;
        args.RoutedEvent=routed;target.RaiseEvent(args);return args;
    }
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var app=new App(isolated:true){ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();ThemeManager.SetPreference("dark");
        foreach(string operation in new[]{"Compress","Split","Webm"})
        {
            var w=new MainWindow(preview:true){ShowActivated=false};w.Show();if(operation!="Compress")Click(w,operation=="Split"?"SplitNav":"WebmNav");
            var model=(BatchViewModel)w.DataContext;
            var targets=new UIElement[]{w,w.Editor.Target,w.Editor.Unit,Find<ListBox>(w,"FilesList"),Find<Button>(w,"SplitNav"),Find<Border>(w,"OutputCard")};
            int count=0;
            foreach(var target in targets)
            {
                string path=Path.GetFullPath(Path.Combine(folder,$"drop-{operation}-{count}.mp4"));File.WriteAllText(path,"synthetic drop-routing fixture");
                var data=new DataObject(DataFormats.FileDrop,new[]{path});string number=w.Editor.Target.Text;
                var over=Drag(target,data,DragDrop.PreviewDragOverEvent);Check(over.Handled && over.Effects==DragDropEffects.Copy,"Sem feedback de cópia sobre "+target.GetType().Name);
                var drop=Drag(target,data,DragDrop.PreviewDropEvent);Check(drop.Handled && model.Files.Count==++count,"Drop global não adicionou em "+operation+"/"+target.GetType().Name);
                Check(w.Editor.Target.Text==number,"Drop substituiu o campo numérico pelo caminho");
                Drag(target,data,DragDrop.PreviewDropEvent);Check(model.Files.Count==count,"Drop duplicou arquivo");
                Busy(model,true);var blocked=Drag(target,data,DragDrop.PreviewDropEvent);Check(blocked.Effects==DragDropEffects.None && model.Files.Count==count,"Drop aceito durante processamento");Busy(model,false);
            }
            var text=new DataObject(DataFormats.UnicodeText,"98");Check(!Drag(w.Editor.Target,text,DragDrop.PreviewDropEvent).Handled,"Texto comum foi interceptado como arquivo");
            string saved=Path.Combine(folder,$"result-{operation}.mp4");File.WriteAllText(saved,"output must survive reset");
            var result=new BatchResult([new(model.Files[0].Path,BatchItemState.Succeeded,[saved])],false);
            typeof(BatchViewModel).GetProperty(nameof(model.LastResult))!.SetValue(model,result);
            typeof(BatchViewModel).GetProperty(nameof(model.LastReportPath))!.SetValue(model,saved);
            typeof(MainWindow).GetMethod("ShowBatchResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,[result]);
            Find<Border>(w,"ProgressPanel").Visibility=Visibility.Visible;
            Busy(model,true);Click(w,"ResetButton");Check(model.Files.Count==count,"Reset limpou lote ativo");Busy(model,false);
            string next=Path.GetFullPath(Path.Combine(folder,$"after-result-{operation}.mp4"));File.WriteAllText(next,"next selection");
            Drag(w.Editor.Target,new DataObject(DataFormats.FileDrop,new[]{next}),DragDrop.PreviewDropEvent);
            Check(model.Files.Count==++count && model.LastResult is null && Find<Border>(w,"ActionBar").Visibility==Visibility.Visible,"Nova seleção após resultado não permitiu processar novamente");
            typeof(BatchViewModel).GetProperty(nameof(model.LastResult))!.SetValue(model,result);
            typeof(MainWindow).GetMethod("ShowBatchResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,[result]);
            Find<Border>(w,"ProgressPanel").Visibility=Visibility.Visible;
            Click(w,"ResetButton");
            Check(model.Files.Count==0 && model.LastResult is null && model.LastReportPath is null && model.LastOutputFolder is null,"Reset deixou estado da sessão anterior");
            Check(w.Editor.Read() is {Operation:Operation.Compress,TargetMb:98,CopyAudio:true},"Reset não voltou a Compactar/98 MB/áudio original");
            Check(Find<Border>(w,"ProgressPanel").Visibility==Visibility.Collapsed && Find<Border>(w,"ActionBar").Visibility==Visibility.Visible,"Reset deixou painel errado");
            Check(File.Exists(saved),"Reset apagou saída");w.Close();
        }
        Console.WriteLine("PASS: drop global sobre janela, lateral, lista, campo e seletor nas três operações; duplicatas/bloqueio/texto/reset/preservação.");
        foreach(string operation in new[]{"Compress","Split","Webm"})
        {
            var w=new MainWindow(preview:true){ShowActivated=false};w.Show();if(operation!="Compress")Click(w,operation=="Split"?"SplitNav":"WebmNav");
            var root=(Grid)w.Content;root.HorizontalAlignment=HorizontalAlignment.Left;root.VerticalAlignment=VerticalAlignment.Top;
            var field=w.Editor.Target;field.Text="97";w.Editor.Unit.SelectedIndex=2;
            foreach(string state in new[]{"idle","progress","result"})
            foreach(var size in new[]{(1120,700),(1280,650),(1280,720),(1536,800),(1920,1000),(900,650),(675,449)})
            {
                Find<Border>(w,"ActionBar").Visibility=state=="idle"?Visibility.Visible:Visibility.Collapsed;
                Find<Border>(w,"ProgressPanel").Visibility=state=="idle"?Visibility.Collapsed:Visibility.Visible;
                Find<Button>(w,"ResetButton").Visibility=Find<Button>(w,"ReportButton").Visibility=state=="result"?Visibility.Visible:Visibility.Collapsed;
                Find<TextBlock>(w,"ProgressStatus").Text=state=="progress"?"1/1 · audiência.mp4 · 42% · restante ≈ 00:25":"1 concluído · 0 erros";
                Find<TextBlock>(w,"ResultDetails").Text="Processamento concluído. Os arquivos originais foram preservados.";
                Find<TextBlock>(w,"ResultDetails").Visibility=state=="result"?Visibility.Visible:Visibility.Collapsed;
                Find<Button>(w,"ProgressAction").Content=state=="progress"?"Cancelar tudo":"Abrir pasta";
                w.Editor.Estimate!.Text=operation=="Split"?"Referência: ≈ 97,0 MB/parte • 3 partes no total":"Orçamento: ≈ 194,0 MB no total • 2 vídeo(s)";
                w.Editor.EstimateNote!.Text=operation=="Split"?"Até 00:15:00 por parte. A recodificação pode alterar o tamanho; não há teto em MB neste modo.":"Teto aplicado: 98,0 MB por vídeo. O tamanho real pode variar e será conferido.";
                w.Editor.EstimateNote.Visibility=Visibility.Visible;
                root.Width=size.Item1;root.Height=size.Item2;w.AdaptiveLayout!.Refresh(size.Item1,size.Item2);root.Measure(new Size(size.Item1,size.Item2));root.Arrange(new Rect(0,0,size.Item1,size.Item2));root.UpdateLayout();
                w.Dispatcher.Invoke(()=>root.UpdateLayout(),System.Windows.Threading.DispatcherPriority.ContextIdle);
                if(size.Item1>=900)
                {
                    var scroll=Find<ScrollViewer>(w,"PageScroll");
                    Check(scroll.ComputedVerticalScrollBarVisibility!=Visibility.Visible,$"Rolagem geral em {operation}/{state}/{size}: conteúdo {scroll.ExtentHeight:0}, área {scroll.ViewportHeight:0}");
                    foreach(var el in new FrameworkElement[]{Find<Border>(w,"FilesCard"),Find<Border>(w,"OutputCard"),state=="idle"?Find<Button>(w,"RunButton"):Find<Button>(w,"ProgressAction")})
                    {var b=el.TransformToAncestor(root).TransformBounds(new Rect(el.RenderSize));Check(b.Top>=-1 && b.Bottom<=size.Item2+1 && b.Left>=-1 && b.Right<=size.Item1+1,$"Corte em {el.Name}/{operation}/{state}/{size}: {b}");}
                    Check(Grid.GetColumn(Find<Border>(w,"OutputCard"))==1,"Dois painéis não foram mantidos");
                    var buttons=Find<System.Windows.Controls.Primitives.UniformGrid>(w,"FileButtons").Children.OfType<Button>().ToArray();
                    Check(buttons.Max(b=>b.ActualWidth)-buttons.Min(b=>b.ActualWidth)<1,"Botões de arquivos com larguras diferentes");
                    var card=Find<Border>(w,"FilesCard");
                    var buttonBounds=buttons[0].TransformToAncestor(card).TransformBounds(new Rect(buttons[0].RenderSize));
                    Check(card.ActualHeight-buttonBounds.Bottom<17,"Espaço vazio sob os botões da entrada");
                }
                else Check(Grid.GetRow(Find<Border>(w,"OutputCard"))==1,"Janela estreita não reorganizou painéis");
                Check(field.Text=="97" && w.Editor.Unit.SelectedIndex==2 && ReferenceEquals(field,w.Editor.Target),"Reflow perdeu valores");
                if(size==(1120,700))Save(root,Path.Combine(folder,$"{operation}-{state}.png"),size.Item1,size.Item2);
            }
            w.Close();
        }
        Console.WriteLine("PASS: layout responsivo nas três operações e estados; sem scroll geral em 900×650 a 1920×1000; valores preservados.");
    }
    private static void Save(Visual root,string path,int width,int height)
    {var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file);}
}
