using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

namespace HeliumVideoTools.Core;

public sealed record UserPreferences(double Width = 1280,double Height = 900,bool Maximized = false,string? LastDirectory = null,int SplitParts = 2,int SplitMinutes = 5,bool CompressionTwoPass = true,bool WebmTwoPass = true,bool LegacyImported = false)
{
    public UserPreferences Normalize() => this with
    {
        Width = double.IsFinite(Width) && Width is >= 980 and <= 10000 ? Width : 1280,
        Height = double.IsFinite(Height) && Height is >= 700 and <= 10000 ? Height : 900,
        LastDirectory = LastDirectory is { Length: > 0 and < 2048 } && Path.IsPathFullyQualified(LastDirectory) ? LastDirectory : null,
        SplitParts = Math.Clamp(SplitParts,2,10), SplitMinutes = Math.Clamp(SplitMinutes,1,1440)
    };
}

public sealed class UserPreferenceStore(string directory,Func<IReadOnlyDictionary<string,string>>? legacyReader = null)
{
    private readonly string path = Path.Combine(directory,"preferences.json");
    private readonly SemaphoreSlim writeLock = new(1);
    private bool preserveInvalid;
    public UserPreferences Load()
    {
        try
        {
            if(File.Exists(path))
            {
                if(new FileInfo(path).Length > 16384) throw new InvalidDataException("Preferências maiores que o limite.");
                return (JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(path)) ?? throw new JsonException()).Normalize();
            }
            var legacy = legacyReader?.Invoke() ?? new Dictionary<string,string>();
            int Number(string key,int fallback) => legacy.TryGetValue(key,out var text) && int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var n) ? n : fallback;
            bool Flag(string key) => !legacy.TryGetValue(key,out var text) || !(text.Equals("false",StringComparison.OrdinalIgnoreCase) || text == "0");
            return new UserPreferences(SplitParts:Number("split/parts",2),SplitMinutes:Number("split/minutes",5),CompressionTwoPass:Flag("compress/precise"),WebmTwoPass:Flag("webm/precise"),LegacyImported:true).Normalize();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or System.Security.SecurityException)
        { preserveInvalid = File.Exists(path); AppLog.Write("WARNING","Preferências indisponíveis; padrões preservados",ex.Message); return new(); }
    }
    public async Task SaveAsync(UserPreferences preferences)
    {
        preferences = preferences.Normalize(); await writeLock.WaitAsync();
        try
        {
            await Task.Run(async () =>
            {
                Directory.CreateDirectory(directory);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(preferences,new JsonSerializerOptions { WriteIndented = true }));
                    if(preserveInvalid && File.Exists(path)) { File.Move(path,path+".invalid-"+Guid.NewGuid().ToString("N"),false); preserveInvalid = false; }
                    File.Move(temporary,path,true);
                }
                finally { if(File.Exists(temporary)) File.Delete(temporary); }
            });
        }
        finally { writeLock.Release(); }
    }
}

public static class LegacyPreferences
{
    public static IReadOnlyDictionary<string,string> Read()
    {
        var result = new Dictionary<string,string>();
        foreach(var (section,key) in new[]{("split","parts"),("split","minutes"),("compress","precise"),("webm","precise")})
        {
            using var registry = Registry.CurrentUser.OpenSubKey(@"Software\Helium\Helium Vídeo Tools\"+section,false);
            if(registry?.GetValue(key) is { } value) result[section+"/"+key] = Convert.ToString(value,CultureInfo.InvariantCulture) ?? "";
        }
        return result;
    }
}
