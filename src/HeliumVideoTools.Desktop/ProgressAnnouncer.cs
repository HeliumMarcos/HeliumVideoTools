using System.Windows.Automation.Peers;
using System.Windows.Controls;
using HeliumVideoTools.Infrastructure;

namespace HeliumVideoTools.Desktop;

internal sealed class AnnouncementGate(Func<long>? clock=null)
{
    private readonly Func<long> now=clock??(()=>Environment.TickCount64);
    private string? previous;
    private long last;
    internal void Reset(){previous=null;last=0;}
    internal bool Accept(string text,bool final=false)
    {
        if(string.IsNullOrWhiteSpace(text) || text==previous)return false;
        long time=now();
        if(!final && previous is not null && time-last<5000)return false;
        previous=text;last=time;return true;
    }
}

// Text remains the normal visible status. Only explicit live-region events are
// throttled; the screen reader decides when to speak a Polite announcement.
internal sealed class ProgressAnnouncer(TextBlock status,AnnouncementGate? gate=null)
{
    private readonly AnnouncementGate gate=gate??new();
    internal void Reset()=>gate.Reset();
    internal void Notify(bool final=false)
    {
        if(!status.IsVisible || !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))return;
        if(!gate.Accept(status.Text,final))return;
        try
        {
            var peer=FrameworkElementAutomationPeer.FromElement(status)??new TextBlockAutomationPeer(status);
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
        catch(Exception ex) when(ex is System.Windows.Automation.ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {AppLog.Write("INFO","Notificação acessível indisponível",ex.Message);}
    }
}
