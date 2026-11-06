namespace HeliumVideoTools.Core;

public static class VideoAudio
{
    public static IReadOnlyList<int> Bitrates { get; }=Array.AsReadOnly(new[]{64,96,128,160,192,256,320});
    public static bool CanCopy(MediaInfo info, VideoOptions options) =>
        options.Operation == Operation.Compress && options.CopyAudio && info.AudioChannels is 1 or 2;

    public static int OutputChannels(MediaInfo info) =>
        info.AudioChannels == 1 ? 1 : 2;

    public static double BudgetKbps(MediaInfo info,VideoOptions options)
    {
        if(options.Operation==Operation.Compress && !options.CopyAudio)
        {
            if(!Bitrates.Contains(options.AudioBitrateKbps))throw new IOException("Escolha uma taxa de áudio válida: de 64 a 320 kbps.");
            if(options.AudioChannels is not (0 or 1 or 2))throw new IOException("Escolha mono ou estéreo.");
        }
        if(info.AudioKbps==0)return 0;
        return CanCopy(info, options) ? info.AudioKbps
            : options.Operation == Operation.Compress && !options.CopyAudio ? options.AudioBitrateKbps
            : 128;
    }
}
