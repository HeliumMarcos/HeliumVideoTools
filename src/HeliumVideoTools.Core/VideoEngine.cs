using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace HeliumVideoTools.Core;

public enum Operation { Split, Compress, Webm }
public enum SplitMode { Parts, Duration, Size }
public sealed record VideoOptions(Operation Operation = Operation.Compress, double TargetMb = 98,
    string Codec = "h264", bool CopyAudio = true, bool TwoPass = false,
    SplitMode SplitMode = SplitMode.Parts, double SplitValue = 3, string Preset = "custom");
public sealed record MediaInfo(double Duration, long Bytes, double AudioKbps, double FrameRate = 25);
public sealed record VideoProgress(double Percent, string Status);

public sealed class VideoEngine(string ffmpeg, string ffprobe) : IVideoProcessor
{
    public static bool Supported(string path) => new[] { ".mp4", ".mkv", ".avi", ".mov", ".m4v", ".webm" }.Contains(Path.GetExtension(path).ToLowerInvariant());
    private static string N(double value) => value.ToString("0.#########", CultureInfo.InvariantCulture);
    public static int Bitrate(double duration, double targetMb, double audioKbps)
    {
        if (duration <= 0 || targetMb <= 0 || !double.IsFinite(targetMb)) throw new IOException("Informe um tamanho válido.");
        double rate = targetMb * 8000 * .94 / duration - audioKbps;
        if (rate < 80 || rate > int.MaxValue / 4) throw new IOException("O tamanho escolhido é incompatível com a duração e o áudio do vídeo.");
        return (int)rate;
    }
    public static async Task<string> ExecuteAsync(string binary, IEnumerable<string> args, Action<string>? line, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(binary) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        process.Start();
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var error = process.StandardError.ReadToEndAsync();
        var output = new System.Text.StringBuilder();
        while (await process.StandardOutput.ReadLineAsync() is { } text) { output.AppendLine(text); line?.Invoke(text); }
        await process.WaitForExitAsync();
        var details = await error;
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
        {
            AppLog.Write("ERROR", "FFmpeg", details);
            throw new IOException(details.Contains("No space left", StringComparison.OrdinalIgnoreCase) ? "O disco ficou sem espaço." : "Não foi possível processar o vídeo. Verifique o formato, áudio e permissões da pasta. Os detalhes estão no log.");
        }
        return output.ToString();
    }
    public async Task<MediaInfo> ProbeAsync(string source, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var json = await ExecuteAsync(ffprobe, ["-v", "error", "-show_format", "-show_streams", "-of", "json", source], null, timeout.Token);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.GetProperty("streams").EnumerateArray().Any(s => s.GetProperty("codec_type").GetString() == "video")) throw new IOException("Este arquivo não possui vídeo.");
        var duration = double.Parse(root.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        if (duration <= 0 || !double.IsFinite(duration)) throw new IOException("Duração de vídeo inválida.");
        var audio = root.GetProperty("streams").EnumerateArray().Where(s => s.GetProperty("codec_type").GetString() == "audio").Take(1).Sum(s => s.TryGetProperty("bit_rate", out var b) && double.TryParse(b.GetString(), out var rate) ? rate / 1000 : 128);
        var video = root.GetProperty("streams").EnumerateArray().First(s => s.GetProperty("codec_type").GetString() == "video");
        double fps = 25;
        if (video.TryGetProperty("avg_frame_rate", out var frameRate))
        {
            var ratio = (frameRate.GetString() ?? "").Split('/');
            if (ratio.Length == 2 && double.TryParse(ratio[0], CultureInfo.InvariantCulture, out var numerator) && double.TryParse(ratio[1], CultureInfo.InvariantCulture, out var denominator) && denominator > 0 && numerator > 0) fps = numerator / denominator;
        }
        return new(duration, new FileInfo(source).Length, audio, fps);
    }
    public async Task<IReadOnlyList<string>> ProcessAsync(string source, VideoOptions options, IProgress<VideoProgress> progress, CancellationToken token, Action<string>? outputPublished = null)
    {
        if (!File.Exists(source) || !Supported(source)) throw new IOException("Selecione um vídeo existente em formato suportado.");
        progress.Report(new(0, "Analisando vídeo…"));
        var info = await ProbeAsync(source, token);
        var folder = Path.GetDirectoryName(Path.GetFullPath(source))!;
        var originalMb = info.Bytes / 1000000d;
        var target = options.Operation == Operation.Webm ? Math.Min(options.TargetMb, originalMb * .98) : options.Preset switch { "low" => originalMb * .75, "medium" => originalMb * .55, "high" => originalMb * .35, _ => options.TargetMb };
        if (options.Operation == Operation.Compress && target >= originalMb) throw new IOException("O tamanho final deve ser menor que o vídeo original.");
        var required = (options.Operation == Operation.Split ? info.Bytes : target * 1000000) * 1.15 + 33554432;
        if (new DriveInfo(Path.GetPathRoot(folder)!).AvailableFreeSpace < required) throw new IOException("Espaço insuficiente na unidade de saída.");
        var outputs = new List<string>();
        if (options.Operation == Operation.Split)
        {
            if (options.SplitMode == SplitMode.Size)
                return await SplitBySizeAsync(source, options.SplitValue, info, progress, token, outputPublished);
            double length = options.SplitMode switch { SplitMode.Parts => info.Duration / options.SplitValue, SplitMode.Duration => options.SplitValue * 60, _ => info.Duration * options.SplitValue / originalMb };
            if (!double.IsFinite(length) || length < 1 || options.SplitValue <= 0 || (options.SplitMode == SplitMode.Parts && (options.SplitValue < 2 || options.SplitValue > 10 || options.SplitValue % 1 != 0))) throw new IOException("Divisão inválida: escolha 2 a 10 partes ou intervalos de pelo menos um segundo.");
            var count = options.SplitMode == SplitMode.Parts ? (int)options.SplitValue : (int)Math.Ceiling(info.Duration / length);
            if (count > 10000) throw new IOException("A divisão criaria arquivos demais.");
            for (var i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                var index = i;
                var segment = Math.Min(length, info.Duration - i * length);
                var output = Unique(Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(source)}_parte_{i + 1:000}.mp4"));
                // Busca precisa com recodificação: não transporta o pré-roll do
                // quadro-chave anterior para a parte seguinte, como stream copy.
                var args = CutArgs(source, i * length, segment);
                await RunOutput(args, output, segment, p => progress.Report(new((index + p / 100) / count * 100, $"Parte {index + 1}/{count}")), token, outputPublished: outputPublished);
                outputs.Add(output);
            }
        }
        else
        {
            bool webm = options.Operation == Operation.Webm;
            var encoder = webm ? options.Codec == "vp9" ? "libvpx-vp9" : "libvpx" : options.Codec == "h265" ? "libx265" : "libx264";
            var audio = info.AudioKbps == 0 ? 0 : webm ? 128 : options.CopyAudio ? info.AudioKbps : 128;
            var rate = Bitrate(info.Duration, target, audio);
            var output = Unique(Path.Combine(folder, Path.GetFileNameWithoutExtension(source) + (webm ? $"_{options.Codec}.webm" : "_compactado.mp4")));
            var passFolder = Path.Combine(Path.GetTempPath(), "HeliumVideoTools", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(passFolder);
            try
            {
                var passes = options.TwoPass ? 2 : 1;
                for (var attempt = 0; attempt < 3; attempt++)
                {
                try
                {
                for (var pass = 1; pass <= passes; pass++)
                {
                    int stage = pass;
                    var args = new List<string> { "-y", "-hide_banner", "-i", source, "-map", "0:v:0", "-c:v", encoder, "-b:v", $"{rate}k", "-pix_fmt", "yuv420p" };
                    if (webm) args.AddRange(["-deadline", "good", "-cpu-used", options.Codec == "vp9" ? "3" : "4"]);
                    else args.AddRange(["-preset", "medium"]);
                    var log = Path.Combine(passFolder, "pass");
                    if (options.TwoPass)
                    {
                        if (encoder == "libx265") args.AddRange(["-x265-params", $"pass={pass}:stats={log.Replace('\\', '/')}"]);
                        else args.AddRange(["-pass", pass.ToString(), "-passlogfile", log]);
                    }
                    Action<double> report = p => progress.Report(new((stage - 1 + p / 100) / passes * 100, $"{(webm ? "Convertendo" : "Compactando")} • etapa {stage}/{passes}"));
                    if (passes == 2 && pass == 1)
                    {
                        args.AddRange(["-an", "-f", "null", "-progress", "pipe:1", "-nostats", "NUL"]);
                        await Run(args, info.Duration, report, token);
                    }
                    else
                    {
                        if (audio > 0) args.AddRange(webm ? ["-map", "0:a:0?", "-c:a", "libopus", "-b:a", "128k"] : options.CopyAudio ? ["-map", "0:a:0?", "-c:a", "copy"] : ["-map", "0:a:0?", "-c:a", "aac", "-b:a", "128k"]);
                        else args.Add("-an");
                        if (!webm) args.AddRange(["-movflags", "+faststart"]);
                        await RunOutput(args, output, info.Duration, report, token, (long)(target * 1000000), outputPublished);
                    }
                }
                break;
                }
                catch (OutputTooLargeException ex) when (attempt < 2)
                {
                    var next = (int)(rate * ex.Limit / ex.Actual * .9);
                    if (next < 80) throw new IOException("O áudio e a duração não cabem neste limite. Reduza o tamanho do trecho ou permita recodificar o áudio.");
                    rate = next;
                    progress.Report(new(0, "Ajustando a taxa de vídeo para respeitar o tamanho final…"));
                }
                }
            }
            finally { try { Directory.Delete(passFolder, true); } catch (IOException) { } }
            outputs.Add(output);
        }
        return outputs;
    }
    private async Task RunOutput(List<string> args, string output, double duration, Action<double> report, CancellationToken token, long? maxBytes = null, Action<string>? outputPublished = null)
    {
        var temp = Path.Combine(Path.GetDirectoryName(output)!, $".{Guid.NewGuid():N}.partial{Path.GetExtension(output)}");
        try
        {
            args.AddRange(["-progress", "pipe:1", "-nostats", temp]);
            await Run(args, duration, report, token);
            token.ThrowIfCancellationRequested();
            if (maxBytes is { } limit && new FileInfo(temp).Length > limit)
                throw new OutputTooLargeException(limit, new FileInfo(temp).Length);
            await ProbeAsync(temp, token); // Não publicar um arquivo vazio ou sem vídeo.
            File.Move(temp, output, false);
            outputPublished?.Invoke(output);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private async Task<IReadOnlyList<string>> SplitBySizeAsync(string source, double targetMb, MediaInfo info, IProgress<VideoProgress> progress, CancellationToken token, Action<string>? outputPublished)
    {
        if (targetMb <= 0 || !double.IsFinite(targetMb) || targetMb * 1000000 > long.MaxValue) throw new IOException("Tamanho por parte inválido.");
        long limit = (long)(targetMb * 1000000);
        var outputs = new List<string>();
        double start = 0;
        double suggested = Math.Min(info.Duration, info.Duration * limit / info.Bytes * .9);
        if (suggested < .25) throw new IOException("O limite por parte é pequeno demais.");
        while (start < info.Duration - .001)
        {
            token.ThrowIfCancellationRequested();
            if (outputs.Count >= 10000) throw new IOException("A divisão criaria arquivos demais.");
            double length = Math.Min(suggested, info.Duration - start);
            length = Math.Max(1 / info.FrameRate, Math.Floor(length * info.FrameRate) / info.FrameRate);
            if (info.Duration - start - length < 2 / info.FrameRate) length = info.Duration - start;
            var folder = Path.GetDirectoryName(Path.GetFullPath(source))!;
            var output = Unique(Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(source)}_parte_{outputs.Count + 1:000}.mp4"));
            for (int attempt = 0; ; attempt++)
            {
                var args = CutArgs(source, start, length);
                try
                {
                    await RunOutput(args, output, length, p => progress.Report(new((start + length * p / 100) / info.Duration * 100, $"Criando parte {outputs.Count + 1} • conferindo limite de {targetMb:0.###} MB")), token, limit, outputPublished);
                    break;
                }
                catch (OutputTooLargeException ex) when (attempt < 7 && length > .3)
                {
                    length = Math.Floor(length * ex.Limit / ex.Actual * .88 * info.FrameRate) / info.FrameRate;
                    if (length < .25) throw new IOException("O limite é pequeno demais para uma parte reproduzível.");
                    progress.Report(new(start / info.Duration * 100, "Ajustando a duração para o tamanho de cada parte…"));
                }
            }
            outputs.Add(output);
            // Próximo trecho começa exatamente no fim solicitado do anterior.
            start += length;
            suggested = length;
        }
        progress.Report(new(100, $"{outputs.Count} partes criadas, todas dentro do limite"));
        return outputs;
    }
    private static List<string> CutArgs(string source,double start,double length) => ["-y", "-hide_banner", "-ss", N(start), "-accurate_seek", "-i", source, "-t", N(length), "-map", "0:v:0", "-map", "0:a:0?", "-c:v", "libx264", "-preset", "fast", "-crf", "20", "-pix_fmt", "yuv420p", "-fps_mode", "passthrough", "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart"];
    private Task Run(List<string> args, double duration, Action<double> report, CancellationToken token) => ExecuteAsync(ffmpeg, args, line =>
    {
        if (line.StartsWith("out_time_us=") && double.TryParse(line[12..], CultureInfo.InvariantCulture, out var micro)) report(Math.Clamp(micro / 1000000 / duration * 100, 0, 99.9));
        if (line == "progress=end") report(100);
    }, token);
    public static string Unique(string path)
    {
        if (!File.Exists(path)) return path;
        for (int i = 2; i < 10000; i++) { var candidate = Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)} ({i}){Path.GetExtension(path)}"); if (!File.Exists(candidate)) return candidate; }
        throw new IOException("Não foi possível reservar um nome para o resultado.");
    }
}

public sealed class OutputTooLargeException(long limit, long actual) : IOException("O resultado excedeu o limite mesmo após os ajustes e não foi publicado.")
{
    public long Limit { get; } = limit;
    public long Actual { get; } = actual;
}

public static class AppLog
{
    private static readonly object Gate = new();
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeliumVideoTools");
    public static void Write(string level, string message, string? detail = null)
    {
        try { lock (Gate) { Directory.CreateDirectory(Root); var path = Path.Combine(Root, "app.jsonl"); if (File.Exists(path) && new FileInfo(path).Length > 2097152) File.Move(path, path + ".1", true); File.AppendAllText(path, JsonSerializer.Serialize(new { timestamp = DateTimeOffset.Now, level, message, detail }) + Environment.NewLine); } } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }
}
