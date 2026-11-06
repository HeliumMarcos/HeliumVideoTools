using System.Windows;
using System.Windows.Controls;

namespace HeliumVideoTools.Desktop;

// Reflows the same controls without rebuilding editors or losing their values.
internal sealed class ResponsiveLayout
{
    private readonly Grid root,main,work,action;
    private readonly Border sidebar,files,output,progress;
    private readonly DockPanel dock,progressHeader;
    private readonly StackPanel options,progressBody,progressButtons,progressTitle;
    private readonly StackPanel stackedProgress=new();
    private readonly Button run;
    private readonly TextBlock subtitle;
    private readonly ScrollViewer page,sideScroll=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},workspace=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    private bool applying,pending,sideWrapped,actionStacked,headerStacked,workspaceWrapped;
    internal ResponsiveLayout(MainWindow window)
    {
        root=(Grid)window.Content;sidebar=(Border)root.Children[0];dock=(DockPanel)sidebar.Child;main=(Grid)root.Children[1];
        work=(Grid)window.FindName("WorkPanels");files=(Border)window.FindName("FilesCard");output=(Border)window.FindName("OutputCard");
        options=(StackPanel)window.FindName("OptionsPanel");page=(ScrollViewer)window.FindName("PageScroll");
        action=(Grid)((Border)window.FindName("ActionBar")).Child;run=(Button)window.FindName("RunButton");
        subtitle=(TextBlock)window.FindName("Subtitle");
        progress=(Border)window.FindName("ProgressPanel");progressBody=(StackPanel)progress.Child;progressHeader=(DockPanel)progressBody.Children[0];
        progressButtons=(StackPanel)progressHeader.Children[0];progressTitle=(StackPanel)progressHeader.Children[1];
        root.SizeChanged+=(_,_)=>ScheduleRefresh();options.SizeChanged+=(_,_)=>ScheduleRefresh();progress.IsVisibleChanged+=(_,_)=>ScheduleRefresh();
    }
    private void ScheduleRefresh()
    {
        if(pending)return;pending=true;
        root.Dispatcher.BeginInvoke(new Action(()=>{pending=false;if(root.IsLoaded)Refresh();}),System.Windows.Threading.DispatcherPriority.Loaded);
    }
    internal void Refresh()=>Refresh(root.ActualWidth,root.ActualHeight);
    internal void Refresh(double width,double height)
    {
        if(applying || width<=0 || height<=0)return;applying=true;
        try
        {
            bool narrow=width<850,small=height<760;
            bool entire=height<420 || (height<600 && progress.Visibility==Visibility.Visible);
            root.ColumnDefinitions[0].Width=new GridLength(narrow?156:194);
            sidebar.Padding=narrow?new Thickness(8,12,8,10):new Thickness(12,20,12,12);
            main.Margin=narrow?new Thickness(12,12,12,8):new Thickness(20,18,20,10);
            var audioSettings=options.Children.OfType<Border>().FirstOrDefault(b=>b.Tag as string=="AudioSettings");
            bool audioExpanded=audioSettings?.Visibility==Visibility.Visible;
            bool compactProgress = audioExpanded && height < 700;
            progress.Padding = compactProgress ? new Thickness(12, 6, 12, 6) : new Thickness(12);
            var status = (TextBlock)progressBody.Children[1];
            var details = (TextBlock)progressBody.Children[2];
            status.Margin = compactProgress ? new Thickness(0, 4, 0, 4) : new Thickness(0, 10, 0, 10);
            details.Margin = new Thickness(0, 0, 0, compactProgress ? 2 : 10);

            subtitle.Visibility=audioExpanded && height<700?Visibility.Collapsed:Visibility.Visible;
            foreach(var hint in options.Children.OfType<TextBlock>().Where(t=>t.Tag as string=="OperationHint"))hint.Visibility=audioExpanded && height<700?Visibility.Collapsed:Visibility.Visible;
            if(audioExpanded && height<700 && !narrow)main.Margin=new Thickness(20,12,20,8);
            if(audioSettings is not null)
            {
                bool compactAudio=height<760,inlineAudio=compactAudio && width>=1100;
                audioSettings.Padding=new Thickness(compactAudio?6:10);
                foreach(var text in Walk(audioSettings).OfType<TextBlock>().Where(t=>t.Tag as string is "AudioHeading" or "AudioHint"))text.Visibility=compactAudio?Visibility.Collapsed:Visibility.Visible;
                foreach(var stack in Walk(audioSettings).OfType<StackPanel>().Where(s=>s.Tag as string=="AudioField"))
                {
                    stack.Orientation=inlineAudio?Orientation.Horizontal:Orientation.Vertical;
                    var label=(TextBlock)stack.Children[0];label.VerticalAlignment=VerticalAlignment.Center;label.Margin=inlineAudio?new Thickness(0,0,6,0):new Thickness(0,0,0,4);
                }
            }
            bool wrapSide=height<650;
            if(wrapSide!=sideWrapped)
            {
                if(wrapSide){sidebar.Child=null;sideScroll.Content=dock;sidebar.Child=sideScroll;}
                else{sideScroll.Content=null;sideScroll.UpdateLayout();sidebar.Child=dock;}
                sideWrapped=wrapSide;
            }
            foreach(var image in Walk(dock).OfType<Image>())image.Width=narrow?132:162;
            work.ColumnDefinitions[0].Width=new GridLength(narrow?1:.9,GridUnitType.Star);
            work.ColumnDefinitions[1].Width=narrow?new GridLength(0):new GridLength(1.1,GridUnitType.Star);
            if(narrow && work.RowDefinitions.Count==1)work.RowDefinitions.Add(new(){Height=GridLength.Auto});
            if(!narrow && work.RowDefinitions.Count>1)work.RowDefinitions.RemoveAt(1);
            Grid.SetColumn(output,narrow?0:1);Grid.SetRow(output,narrow?1:0);
            files.Margin=narrow?new Thickness(0,0,0,10):new Thickness(0,0,12,0);
            bool inline=small && !narrow;
            foreach(var row in options.Children.OfType<Grid>().Where(g=>g.Tag as string=="OptionRow"))
            {
                if(row.Children.Count!=2)continue;
                var label=(FrameworkElement)row.Children[0];var field=row.Children[1];
                row.ColumnDefinitions.Clear();row.RowDefinitions.Clear();
                if(inline && field is not Border)
                {
                    row.ColumnDefinitions.Add(new(){Width=new GridLength(104)});row.ColumnDefinitions.Add(new());
                    Grid.SetColumn(field,1);Grid.SetRow(field,0);label.Margin=new Thickness(0,0,8,0);
                }
                else
                {
                    row.RowDefinitions.Add(new(){Height=GridLength.Auto});row.RowDefinitions.Add(new(){Height=GridLength.Auto});
                    Grid.SetColumn(field,0);Grid.SetRow(field,1);label.Margin=new Thickness(0,0,0,4);
                }
            }
            if(narrow!=actionStacked)
            {
                action.ColumnDefinitions.Clear();action.RowDefinitions.Clear();
                if(narrow)
                {
                    action.RowDefinitions.Add(new(){Height=GridLength.Auto});action.RowDefinitions.Add(new(){Height=GridLength.Auto});
                    Grid.SetColumn(run,0);Grid.SetRow(run,1);run.Margin=new Thickness(0,8,0,0);
                }
                else
                {
                    action.ColumnDefinitions.Add(new());action.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
                    Grid.SetColumn(run,1);Grid.SetRow(run,0);run.Margin=new Thickness(0);
                }
                actionStacked=narrow;
            }
            bool stackHeader=width<900;
            if(stackHeader!=headerStacked)
            {
                progressHeader.Children.Clear();stackedProgress.Children.Clear();
                if(stackHeader){stackedProgress.Children.Add(progressTitle);stackedProgress.Children.Add(progressButtons);progressButtons.Margin=new Thickness(0,8,0,0);}
                else{progressHeader.Children.Add(progressButtons);progressHeader.Children.Add(progressTitle);progressButtons.Margin=new Thickness(0);}
                progressBody.Children.RemoveAt(0);progressBody.Children.Insert(0,stackHeader?stackedProgress:progressHeader);headerStacked=stackHeader;
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
    internal void RevealAction(){if(workspaceWrapped)workspace.ScrollToEnd();}
    private static IEnumerable<DependencyObject> Walk(DependencyObject node)
    {yield return node;foreach(var item in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var child in Walk(item))yield return child;}
}
