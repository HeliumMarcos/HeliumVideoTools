namespace HeliumVideoTools.Core;

public sealed record UserPreferences(double Width = 1280,double Height = 900,bool Maximized = false,string? LastDirectory = null,int SplitParts = 2,int SplitMinutes = 5,bool CompressionTwoPass = true,bool WebmTwoPass = true,bool LegacyImported = false,string Theme = "system")
{
    public UserPreferences Normalize() => this with
    {
        Theme = Theme is "light" or "dark" ? Theme : "system",
        Width = double.IsFinite(Width) && Width is >= 980 and <= 10000 ? Width : 1280,
        Height = double.IsFinite(Height) && Height is >= 700 and <= 10000 ? Height : 900,
        LastDirectory = LastDirectory is { Length: > 0 and < 2048 } && Path.IsPathFullyQualified(LastDirectory) ? LastDirectory : null,
        SplitParts = Math.Clamp(SplitParts,2,10), SplitMinutes = Math.Clamp(SplitMinutes,1,1440)
    };
}
