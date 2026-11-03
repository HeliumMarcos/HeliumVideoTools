using System.IO;
using System.Text.Json;
using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;

internal static class AudioEncodingTests
{
    private static void Check(bool value,string text){if(!value)throw new Exception(text);}
    internal static async Task RunAsync(string folder,string ffmpeg,string ffprobe)
    {
        Directory.CreateDirectory(folder);
        var info=new MediaInfo(60,20_000_000,192);
        var low=new VideoOptions(TargetMb:2,CopyAudio:false,AudioBitrateKbps:64,AudioChannels:1);
        Check(VideoAudio.BudgetKbps(info,low)==64,"Orçamento ignora a taxa selecionada");
        Check(VideoEstimates.Calculate(info,low).BudgetMb is >1.8 and <=2,"Estimativa de AAC fora do orçamento");
        try{VideoEstimates.Calculate(info,low with {AudioBitrateKbps=320});throw new Exception("Alvo impossível com áudio de 320 kbps aceito");}catch(IOException){}
        foreach(var invalid in new[]{low with {AudioBitrateKbps=65},low with {AudioBitrateKbps=-1},low with {AudioChannels=3}})
        {try{VideoAudio.BudgetKbps(info,invalid);throw new Exception("Configuração inválida de áudio aceita");}catch(IOException){}}
        Check(VideoAudio.BudgetKbps(info,low with {CopyAudio=true,AudioChannels=9,AudioBitrateKbps=-1})==192,"Ajustes ocultos alteraram a cópia original");
        Check(VideoAudio.BudgetKbps(info,low with {Operation=Operation.Webm,AudioBitrateKbps=320})==128,"AAC personalizado alterou Opus/WebM");
        var legacy=JsonSerializer.Deserialize<VideoOptions>("{\"Operation\":0,\"TargetMb\":98}")!;
        Check(legacy.AudioBitrateKbps==128 && legacy.AudioChannels==0,"Opções antigas não receberam padrões de áudio");
        string source=Path.Combine(folder,"audio-stereo.mp4");
        await VideoEngine.ExecuteAsync(ffmpeg,["-y","-hide_banner","-f","lavfi","-i","testsrc2=size=640x360:rate=25:duration=8","-f","lavfi","-i","aevalsrc=sin(440*2*PI*t)|sin(880*2*PI*t):s=48000:d=8","-c:v","libx264","-preset","ultrafast","-crf","0","-c:a","aac","-b:a","160k","-shortest",source],null,default);
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(source)));
        var engine=new VideoEngine(ffmpeg,ffprobe);var progress=new Progress<VideoProgress>();
        async Task<JsonElement[]> Audio(string path)
        {using var doc=JsonDocument.Parse(await VideoEngine.ExecuteAsync(ffprobe,["-v","error","-select_streams","a:0","-show_entries","stream=codec_name,channels,bit_rate","-of","json",path],null,default));return doc.RootElement.GetProperty("streams").EnumerateArray().Select(s=>s.Clone()).ToArray();}
        var outputs=new List<string>();
        foreach(var (rate,channels,twoPass) in new[]{(64,1,false),(192,2,true),(128,0,false)})
        {
            var result=await engine.ProcessAsync(source,new(TargetMb:.8,CopyAudio:false,TwoPass:twoPass,AudioBitrateKbps:rate,AudioChannels:channels),progress,default);
            string output=result.Single();outputs.Add(output);var stream=(await Audio(output)).Single();
            Check(stream.GetProperty("codec_name").GetString()=="aac" && stream.GetProperty("channels").GetInt32()==(channels==0?2:channels),"Codec/canais não correspondem à seleção");
            double actual=double.Parse(stream.GetProperty("bit_rate").GetString()!,System.Globalization.CultureInfo.InvariantCulture)/1000;
            Check(actual>=rate*.55 && actual<=rate*1.35,$"Taxa AAC inesperada: selecionada {rate}, real {actual:0.0}");
            Check(new FileInfo(output).Length<=800_000,"AAC personalizado ultrapassou o teto");
            await VideoEngine.ExecuteAsync(ffmpeg,["-v","error","-i",output,"-map","0:a:0","-f","null","NUL"],null,default);
        }
        var copied=(await engine.ProcessAsync(source,new(TargetMb:.8,CopyAudio:true,AudioBitrateKbps:64,AudioChannels:1),progress,default)).Single();
        async Task<string> Packets(string path)
        {var text=await VideoEngine.ExecuteAsync(ffmpeg,["-v","error","-i",path,"-map","0:a:0","-c:a","copy","-f","hash","-hash","sha256","-"],null,default);return text.Split('\n').Single(line=>line.StartsWith("SHA256=",StringComparison.Ordinal)).Trim();}
        Check(await Packets(source)==await Packets(copied),"Manter áudio original alterou os pacotes AAC");
        string silent=Path.Combine(folder,"silent.mp4");await VideoEngine.ExecuteAsync(ffmpeg,["-y","-v","error","-i",source,"-map","0:v:0","-c:v","copy","-an",silent],null,default);
        var silentOutput=(await engine.ProcessAsync(silent,new(TargetMb:.8,CopyAudio:false,AudioBitrateKbps:320,AudioChannels:1),progress,default)).Single();
        Check((await Audio(silentOutput)).Length==0,"Configuração criou áudio em vídeo sem som");
        Check(VideoAudio.BudgetKbps(new(8,2_000_000,0),low)==0,"Vídeo sem áudio consumiu orçamento de som");
        Check(hash==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(source))),"Fonte de áudio alterada");
        Console.WriteLine("PASS: AAC 64/192/128 kbps, mono/estéreo/canais originais, uma/duas etapas, teto, decodificação, cópia AAC idêntica, vídeo sem áudio, validação e orçamento.");
    }
}
