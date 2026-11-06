using System.Globalization;
using System.Text.Json;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Infrastructure;

public sealed class MediaProbe(string ffprobe)
{
    public async Task<MediaInfo> ProbeAsync(string source, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var json = await MediaProcess.ExecuteAsync(ffprobe, ["-v", "error", "-show_format", "-show_streams", "-of", "json", source], null, timeout.Token);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.GetProperty("streams").EnumerateArray().Any(s => s.GetProperty("codec_type").GetString() == "video")) throw new IOException("Este arquivo não possui vídeo.");
        var duration = double.Parse(root.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        if (duration <= 0 || !double.IsFinite(duration)) throw new IOException("Duração de vídeo inválida.");
        var audioStream = root.GetProperty("streams").EnumerateArray()
            .FirstOrDefault(stream => stream.GetProperty("codec_type").GetString() == "audio");
        int channels = 0;
        double audio = 0;
        if (audioStream.ValueKind != JsonValueKind.Undefined)
        {
            if (audioStream.TryGetProperty("channels", out var count))
                count.TryGetInt32(out channels);
            audio = audioStream.TryGetProperty("bit_rate", out var bitrate)
                && double.TryParse(bitrate.GetString(), CultureInfo.InvariantCulture, out var rate) && rate > 0
                ? rate / 1000 : 128;
        }
        var video = root.GetProperty("streams").EnumerateArray().First(s => s.GetProperty("codec_type").GetString() == "video");
        double fps = 25;
        if (video.TryGetProperty("avg_frame_rate", out var frameRate))
        {
            var ratio = (frameRate.GetString() ?? "").Split('/');
            if (ratio.Length == 2 && double.TryParse(ratio[0], CultureInfo.InvariantCulture, out var numerator) && double.TryParse(ratio[1], CultureInfo.InvariantCulture, out var denominator) && denominator > 0 && numerator > 0) fps = numerator / denominator;
        }
        return new(duration, new FileInfo(source).Length, audio, fps, channels);
    }
}
