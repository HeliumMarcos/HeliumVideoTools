using System.Windows;

namespace HeliumVideoTools.Desktop;

// Capture file drops before TextBox/ListBox class handlers consume them.
internal sealed class WindowFileDrop
{
    internal WindowFileDrop(Window window,Func<bool> canAdd,Action<string[]> add)
    {
        window.AllowDrop=true;
        void Handle(object sender,DragEventArgs e)
        {
            if(!e.Data.GetDataPresent(DataFormats.FileDrop,false))return;
            e.Handled=true;
            bool accepted=canAdd() && (e.AllowedEffects&DragDropEffects.Copy)!=0;
            e.Effects=accepted?DragDropEffects.Copy:DragDropEffects.None;
            if(accepted && e.RoutedEvent==DragDrop.PreviewDropEvent && e.Data.GetData(DataFormats.FileDrop,false) is string[] paths)add(paths);
        }
        window.AddHandler(DragDrop.PreviewDragEnterEvent,new DragEventHandler(Handle),true);
        window.AddHandler(DragDrop.PreviewDragOverEvent,new DragEventHandler(Handle),true);
        window.AddHandler(DragDrop.PreviewDropEvent,new DragEventHandler(Handle),true);
    }
}
