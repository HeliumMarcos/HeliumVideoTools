namespace HeliumVideoTools.Core;

// Estes padrões são requisitos do produto, não preferências recuperáveis.
public static class StartupDefaults
{
    public static VideoOptions For(Operation operation,UserPreferences preferences) => new(operation,
        TargetMb:operation==Operation.Webm?299:98,Codec:operation==Operation.Webm?"vp8":"h264",CopyAudio:true,
        TwoPass:operation==Operation.Webm?preferences.WebmTwoPass:preferences.CompressionTwoPass,
        SplitMode:SplitMode.Size,SplitValue:98,Preset:"custom");
}
