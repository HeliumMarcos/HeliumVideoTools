namespace HeliumVideoTools.Core;

public static class VideoSizing
{
    public static double TargetMb(MediaInfo info, VideoOptions options)
    {
        double original = info.Bytes / 1000000d;
        return options.Operation == Operation.Webm ? Math.Min(options.TargetMb, original * .98) : options.Preset switch
        { "low" => original * .75, "medium" => original * .55, "high" => original * .35, _ => options.TargetMb };
    }
}
public sealed record OutputEstimate(double? CapMb = null, double? BudgetMb = null, double? ReferencePartMb = null, int? Parts = null, double? PartSeconds = null, string? Warning = null);
public sealed record EstimateEntry(string Source, OutputEstimate? Estimate, string? Error = null);

public static class VideoEstimates
{
    public static OutputEstimate Calculate(MediaInfo info, VideoOptions options)
    {
        if (info.Bytes <= 0 || !double.IsFinite(info.Duration) || info.Duration <= 0) throw new IOException("Metadados insuficientes para estimar.");
        double original = info.Bytes / 1000000d;
        if (options.Operation == Operation.Split)
        {
            double value = options.SplitValue;
            if (!double.IsFinite(value) || value <= 0) throw new IOException("Informe um valor de divisão válido.");
            if (options.SplitMode == SplitMode.Size) return new(CapMb: value);
            double seconds = options.SplitMode == SplitMode.Parts ? info.Duration / value : value * 60;
            if (options.SplitMode == SplitMode.Parts && (value < 2 || value > 10 || value % 1 != 0)) throw new IOException("Escolha de 2 a 10 partes.");
            if (seconds < 1 || Math.Ceiling(info.Duration / seconds) > 10000) throw new IOException("Intervalo de divisão inválido.");
            int count = options.SplitMode == SplitMode.Parts ? (int)value : (int)Math.Ceiling(info.Duration / seconds);
            return new(ReferencePartMb: original * Math.Min(seconds,info.Duration) / info.Duration, Parts: count, PartSeconds: Math.Min(seconds,info.Duration));
        }
        double target = VideoSizing.TargetMb(info,options);
        if (!double.IsFinite(target) || target <= 0) throw new IOException("Informe um tamanho válido.");
        if (options.Operation == Operation.Compress && target >= original)
            return new(CapMb: target, Warning: "O original já está abaixo do alvo; escolha um tamanho menor para compactá-lo.");
        double audio = VideoAudio.BudgetKbps(info,options);
        int rate = VideoBitrate.Calculate(info.Duration,target,audio);
        return new(CapMb: target, BudgetMb: (rate + audio) * info.Duration / 8000);
    }
}
