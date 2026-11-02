using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HeliumVideoTools.Desktop;

// Reflows existing controls. No editor rebuild, value reset or extra tab stops.
internal sealed class ResponsiveLayout
{
    private readonly Grid root,main,action;
    private readonly Border sidebar,progress;
    private readonly DockPanel dock;
    private readonly StackPanel options,buttons,progressBody;
    private readonly DockPanel progressHeader;
    private readonly StackPanel progressButtons,progressTitle;
    private readonly Button run;
    private readonly ScrollViewer page,sideScroll=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},workspace=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    private readonly WrapPanel wrappedButtons=new();
    private readonly StackPanel stackedProgress=new();
    private readonly Panel fileCard;
    private readonly int buttonIndex;
    private readonly Dictionary<TextBlock,TextWrapping> wrapping=new();
    private readonly Dictionary<Grid,(Thickness Row,Thickness Label)> rows=new();
    private bool applying,pending,sideWrapped,buttonsWrapped,actionStacked,headerStacked,workspaceWrapped;

    internal ResponsiveLayout(MainWindow window)
    {
        root=(Grid)window.Content;sidebar=(Border)root.Children[0];dock=(DockPanel)sidebar.Child;main=(Grid)root.Children[1];
        options=(StackPanel)window.FindName("OptionsPanel");page=(ScrollViewer)window.FindName("PageScroll");
        action=(Grid)((Border)window.FindName("ActionBar")).Child;run=(Button)window.FindName("RunButton");
        buttons=Walk(page).OfType<StackPanel>().Single(p=>p.Orientation==Orientation.Horizontal && p.Children.OfType<Button>().Count()==3);
        fileCard=(Panel)buttons.Parent;buttonIndex=fileCard.Children.IndexOf(buttons);
        progress=(Border)window.FindName("ProgressPanel");progressBody=(StackPanel)progress.Child;progressHeader=(DockPanel)progressBody.Children[0];
        progressButtons=(StackPanel)progressHeader.Children[0];progressTitle=(StackPanel)progressHeader.Children[1];
        root.SizeChanged+=(_,_)=>ScheduleRefresh();progress.IsVisibleChanged+=(_,_)=>ScheduleRefresh();
    }
    private void ScheduleRefresh()
    {
        if(pending)return;pending=true;
        root.Dispatcher.BeginInvoke(new Action(()=>{pending=false;if(root.IsLoaded)Refresh();}),System.Windows.Threading.DispatcherPriority.Loaded);
    }
    internal void Refresh()=>Refresh(root.ActualWidth,root.ActualHeight);
    internal void Refresh(double width,double height)
    {
        if(applying || width<=0 || height<=0)return;
        applying=true;
        try
        {
            bool compact=width<1100 || height<700,narrow=width<1000;
            bool entire=height<420 || (height<600 && progress.Visibility==Visibility.Visible);
            root.ColumnDefinitions[0].Width=new GridLength(width<980?184:232);
            sidebar.Padding=width<980?new Thickness(12,16,12,12):new Thickness(16,24,16,16);
            main.Margin=width<1100?new Thickness(16,16,16,8):new Thickness(28,24,28,12);
            if(compact!=sideWrapped)
            {
                if(compact){sidebar.Child=null;sideScroll.Content=dock;sidebar.Child=sideScroll;}
                else{sideScroll.Content=null;sideScroll.UpdateLayout();sidebar.Child=dock;}
                sideWrapped=compact;
            }
            foreach(var image in Walk(dock).OfType<Image>())image.Width=width<980?156:200;
            var texts=Walk(main).OfType<TextBlock>().Concat(Walk(dock).OfType<TextBlock>()).ToHashSet();
            foreach(var text in texts)
            {
                if(!wrapping.ContainsKey(text))wrapping[text]=text.TextWrapping;
                text.TextWrapping=compact?TextWrapping.Wrap:wrapping[text];
            }
            foreach(var old in wrapping.Keys.Where(k=>!texts.Contains(k)).ToArray())wrapping.Remove(old);
            if(compact!=buttonsWrapped)
            {
                Panel from=compact?buttons:wrappedButtons,to=compact?wrappedButtons:buttons;
                while(from.Children.Count>0){var child=from.Children[0];from.Children.RemoveAt(0);to.Children.Add(child);}
                fileCard.Children.RemoveAt(buttonIndex);fileCard.Children.Insert(buttonIndex,(UIElement)to);buttonsWrapped=compact;
            }
            var currentRows=options.Children.OfType<Grid>().ToHashSet();
            foreach(var old in rows.Keys.Where(k=>!currentRows.Contains(k)).ToArray())rows.Remove(old);
            foreach(var row in currentRows)
            {
                if(row.Children.Count!=2)continue;
                var label=(FrameworkElement)row.Children[0];var field=row.Children[1];
                if(narrow && !rows.ContainsKey(row))
                {
                    rows[row]=(row.Margin,label.Margin);row.ColumnDefinitions.Clear();row.RowDefinitions.Add(new(){Height=GridLength.Auto});row.RowDefinitions.Add(new(){Height=GridLength.Auto});
                    label.Margin=new Thickness(0,0,0,6);row.Margin=new Thickness(0,0,0,14);Grid.SetColumn(field,0);Grid.SetRow(field,1);
                }
                else if(!narrow && rows.Remove(row,out var original))
                {
                    row.RowDefinitions.Clear();row.ColumnDefinitions.Add(new(){Width=new GridLength(164)});row.ColumnDefinitions.Add(new());
                    label.Margin=original.Label;row.Margin=original.Row;Grid.SetColumn(field,1);Grid.SetRow(field,0);
                }
            }
            if(narrow!=actionStacked)
            {
                action.ColumnDefinitions.Clear();action.RowDefinitions.Clear();
                if(narrow)
                {
                    action.RowDefinitions.Add(new(){Height=GridLength.Auto});action.RowDefinitions.Add(new(){Height=GridLength.Auto});
                    Grid.SetColumn(run,0);Grid.SetRow(run,1);run.Margin=new Thickness(0,10,0,0);
                }
                else
                {
                    action.ColumnDefinitions.Add(new());action.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
                    Grid.SetColumn(run,1);Grid.SetRow(run,0);run.Margin=new Thickness(0);
                }
                actionStacked=narrow;
            }
            if(narrow!=headerStacked)
            {
                progressHeader.Children.Clear();stackedProgress.Children.Clear();
                if(narrow){stackedProgress.Children.Add(progressTitle);stackedProgress.Children.Add(progressButtons);progressButtons.Margin=new Thickness(0,8,0,0);}
                else{progressHeader.Children.Add(progressButtons);progressHeader.Children.Add(progressTitle);progressButtons.Margin=new Thickness(0);}
                progressBody.Children.RemoveAt(0);progressBody.Children.Insert(0,narrow?stackedProgress:progressHeader);headerStacked=narrow;
            }
            if(entire!=workspaceWrapped)
            {
                if(entire)
                {
                    root.Children.Remove(main);foreach(var row in main.RowDefinitions)row.Height=GridLength.Auto;
                    page.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;workspace.Content=main;Grid.SetColumn(workspace,1);root.Children.Add(workspace);
                }
                else
                {
                    workspace.Content=null;workspace.UpdateLayout();root.Children.Remove(workspace);main.RowDefinitions[0].Height=new GridLength(1,GridUnitType.Star);
                    page.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;root.Children.Add(main);
                }
                workspaceWrapped=entire;
            }
        }
        finally{applying=false;}
    }
    internal void RevealAction()
    {if(workspaceWrapped)workspace.ScrollToEnd();}
    private static IEnumerable<DependencyObject> Walk(DependencyObject node)
    {yield return node;foreach(var item in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var child in Walk(item))yield return child;}
}
