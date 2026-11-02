using System.Windows;
using HeliumVideoTools.Infrastructure;

namespace HeliumVideoTools.Desktop;

internal sealed class UpdateWindowController(Window owner,Func<bool> busy,Func<AppRelease?> available,
    Func<Func<bool>,AppRelease?,UpdatesWindow>? create=null)
{
    private UpdatesWindow? window;
    internal bool Installing=>window?.Installing==true;

    internal void Show()
    {
        if(window is {IsLoaded:true}) {window.Activate();return;}
        var opened=create is null?new UpdatesWindow(busy,available:available()):create(busy,available());
        window=opened;opened.Owner=owner;
        opened.Closed+=(_,_)=>{if(ReferenceEquals(window,opened))window=null;};
        opened.Show();
    }
}
