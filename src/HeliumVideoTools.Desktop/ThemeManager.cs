using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace HeliumVideoTools.Desktop;

internal sealed record ContrastColors(Color Background,Color Text,Color Control,Color ControlText,Color Highlight,Color HighlightText,Color Disabled)
{
    internal static ContrastColors Windows => new(SystemColors.WindowColor,SystemColors.WindowTextColor,SystemColors.ControlColor,SystemColors.ControlTextColor,SystemColors.HighlightColor,SystemColors.HighlightTextColor,SystemColors.GrayTextColor);
}

internal static class ThemeManager
{
    internal static event EventHandler? Changed;
    internal static string Preference { get; private set; } = "system";
    internal static bool IsLight { get; private set; }
    internal static bool HighContrast { get; private set; }
    private static bool initialized;
    internal static void EnsureInitialized()
    {
        if(initialized)return;
        initialized=true;
        SystemParameters.StaticPropertyChanged+=SystemChanged;
        SystemEvents.UserPreferenceChanged+=UserChanged;
        Application.Current.Exit+=(_,_)=>
        {SystemParameters.StaticPropertyChanged-=SystemChanged;SystemEvents.UserPreferenceChanged-=UserChanged;};
        Refresh();
    }
    private static void SystemChanged(object? sender,PropertyChangedEventArgs e)
    {if(e.PropertyName==nameof(SystemParameters.HighContrast))QueueRefresh();}
    private static void UserChanged(object sender,UserPreferenceChangedEventArgs e)=>QueueRefresh();
    private static void QueueRefresh()
    {
        var dispatcher=Application.Current?.Dispatcher;
        if(dispatcher is not null && !dispatcher.HasShutdownStarted)_=dispatcher.BeginInvoke(new Action(Refresh));
    }
    internal static void SetPreference(string preference)
    {EnsureInitialized();Preference=preference is "light" or "dark"?preference:"system";Refresh();}
    private static void Refresh()
    {
        bool light=Preference=="light" || Preference=="system" && SystemIsLight();
        Apply(light,SystemParameters.HighContrast);
    }
    private static bool SystemIsLight()
    {
        try {using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("AppsUseLightTheme") is not int value || value!=0;}
        catch(Exception ex) when(ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException){return true;}
    }
    internal static void Apply(bool light,bool contrast,ContrastColors? colors=null)
    {
        var palette=Palette(light,contrast,colors);
        foreach(var (key,color) in palette)
        {var brush=new SolidColorBrush(color);brush.Freeze();Application.Current.Resources[key]=brush;}
        HighContrast=contrast;
        Application.Current.Resources["DisabledOpacity"]=contrast?1d:.4d;
        IsLight=contrast ? Luminance(palette["AppBackground"])>.5 : light;
        Application.Current.Resources["BrandSignature"]=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/HeliumVideoTools;component/Assets/"+(IsLight?"brand-signature-light.png":"brand-signature.png")));
        Changed?.Invoke(null,EventArgs.Empty);
    }
    internal static Dictionary<string,Color> Palette(bool light,bool contrast,ContrastColors? colors=null)
    {
        Color C(string text)=>(Color)ColorConverter.ConvertFromString(text);
        var result=new Dictionary<string,Color>
        {
            ["AppBackground"]=C(light?"#F3F3F3":"#202020"),["Sidebar"]=C(light?"#ECEFF3":"#252525"),["Surface"]=C(light?"#FFFFFF":"#292929"),
            ["ButtonBackground"]=C(light?"#F7F8FA":"#303030"),["InputBackground"]=C(light?"#FFFFFF":"#242424"),["DropBackground"]=C(light?"#FAFCFE":"#232323"),
            ["TextPrimary"]=C(light?"#17212B":"#F3F3F3"),["TextSecondary"]=C(light?"#556372":"#BDBDBD"),["Icon"]=C(light?"#0067C0":"#6CCCF0"),
            ["ControlBorder"]=C(light?"#7C8996":"#7D8C98"),["CardBorder"]=C(light?"#C9D1D9":"#454545"),["Divider"]=C(light?"#CCD4DC":"#393939"),
            ["Accent"]=C("#0067C0"),["OnAccent"]=Colors.White,["Focus"]=C(light?"#0067C0":"#57CCF2"),["HoverBorder"]=C(light?"#526272":"#BDBDBD"),
            ["SelectedBackground"]=C(light?"#E0EEFA":"#353D44"),["SelectedText"]=C(light?"#17212B":"#F3F3F3"),["SelectedIcon"]=C(light?"#0067C0":"#6CCCF0"),
            ["HoverBackground"]=C(light?"#E9EEF4":"#454545"),["InfoBackground"]=C(light?"#EAF4FB":"#27353F"),["InfoText"]=C(light?"#164F70":"#B1DAED"),
            ["SuccessBackground"]=C(light?"#E9F5ED":"#28352F"),["SuccessText"]=C(light?"#28603C":"#A5DDBB"),
            ["Success"]=C(light?"#28603C":"#83D19E"),["Error"]=C(light?"#A32121":"#F28B82"),["Warning"]=C(light?"#805000":"#F2C66D"),
            ["TrackOff"]=C(light?"#697885":"#747474"),["TrackBorder"]=C(light?"#697885":"#909090"),["Thumb"]=Colors.White,["ScrollThumb"]=C(light?"#697885":"#747474"),
            ["DisabledText"]=C(light?"#697885":"#A3A3A3")
        };
        result["ButtonText"]=result["TextPrimary"];result["CheckedThumb"]=Colors.White;result["DisabledBackground"]=result["InputBackground"];
        if(!contrast)return result;
        var c=colors??ContrastColors.Windows;
        foreach(var key in result.Keys.ToArray())result[key]=c.Text;
        foreach(var key in new[]{"AppBackground","Sidebar","Surface","InputBackground","DropBackground","InfoBackground","SuccessBackground"})result[key]=c.Background;
        result["ButtonBackground"]=c.Control;result["TextPrimary"]=c.Text;result["TextSecondary"]=c.Text;
        result["ButtonText"]=c.ControlText;result["CheckedThumb"]=c.HighlightText;result["DisabledBackground"]=c.Control;
        foreach(var key in new[]{"Accent","SelectedBackground","HoverBackground","Focus","ScrollThumb"})result[key]=c.Highlight;
        foreach(var key in new[]{"OnAccent","SelectedText","SelectedIcon"})result[key]=c.HighlightText;
        result["TrackOff"]=c.Background;result["Thumb"]=c.Text;result["DisabledText"]=c.Disabled;
        return result;
    }
    private static double Luminance(Color c)=>(.2126*c.R+.7152*c.G+.0722*c.B)/255;
}
