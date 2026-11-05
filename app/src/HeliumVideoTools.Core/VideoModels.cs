namespace HeliumVideoTools.Core;

public enum Operation { Split, Compress, Webm }
public enum SplitMode { Parts, Duration, Size }
public sealed record VideoOptions(Operation Operation = Operation.Compress, double TargetMb = 98,
    string Codec = "h264", bool CopyAudio = true, bool TwoPass = false,
    SplitMode SplitMode = SplitMode.Parts, double SplitValue = 3, string Preset = "custom",
    int AudioBitrateKbps = 128, int AudioChannels = 0);
public sealed record MediaInfo(double Duration, long Bytes, double AudioKbps, double FrameRate = 25);
public sealed record VideoProgress(double Percent, string Status);

public static class VideoBitrate
{
    public static int Calculate(double duration, double targetMb, double audioKbps)
    {
        if (duration <= 0 || !double.IsFinite(duration) || targetMb <= 0 || !double.IsFinite(targetMb) || audioKbps < 0 || !double.IsFinite(audioKbps)) throw new IOException("Informe um tamanho e metadados válidos.");
        double rate = targetMb * 8000 * .94 / duration - audioKbps;
        if (rate < 80 || rate > int.MaxValue / 4) throw new IOException("O tamanho escolhido é incompatível com a duração e o áudio do vídeo.");
        return (int)rate;
    }
}
