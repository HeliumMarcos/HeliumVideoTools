namespace HeliumVideoTools.Core;

public static class VideoAudio
{
    public static IReadOnlyList<int> Bitrates { get; }=Array.AsReadOnly(new[]{64,96,128,160,192,256,320});
    public static double BudgetKbps(MediaInfo info,VideoOptions options)
    {
        if(options.Operation==Operation.Compress && !options.CopyAudio)
        {
            if(!Bitrates.Contains(options.AudioBitrateKbps))throw new IOException("Escolha uma taxa de áudio válida: de 64 a 320 kbps.");
            if(options.AudioChannels is not (0 or 1 or 2))throw new IOException("Escolha manter os canais, mono ou estéreo.");
        }
        if(info.AudioKbps==0)return 0;
        return options.Operation==Operation.Webm?128:options.CopyAudio?info.AudioKbps:options.AudioBitrateKbps;
    }
}
