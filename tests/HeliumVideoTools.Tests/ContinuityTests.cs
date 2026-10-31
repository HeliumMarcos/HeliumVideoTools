using HeliumVideoTools.Core;
using System.IO;
using System.Globalization;
using System.Text.Json;

internal static class ContinuityTests
{
    public static async Task RunAsync(string ffmpeg,string ffprobe,string folder)
    {
        const int frames=200, pixels=160*90;
        string raw=Path.Combine(folder,"quadros-marcados.gray"), source=Path.Combine(folder,"continuidade.mp4");
        byte[] data=new byte[frames*pixels];
        for(int i=0;i<frames;i++) data.AsSpan(i*pixels,pixels).Fill((byte)(32+(i*53%190)));
        await File.WriteAllBytesAsync(raw,data);
        await VideoEngine.ExecuteAsync(ffmpeg,["-y","-f","rawvideo","-pixel_format","gray","-video_size","160x90","-framerate","25","-i",raw,"-f","lavfi","-i","sine=frequency=440:duration=8","-c:v","libx264","-preset","ultrafast","-crf","12","-g","999","-pix_fmt","yuv420p","-c:a","aac","-shortest",source],null,default);
        string variable=Path.Combine(folder,"continuidade-vfr.mp4");
        await VideoEngine.ExecuteAsync(ffmpeg,["-y","-i",source,"-vf","select='not(eq(mod(n,7),1))'","-fps_mode","vfr","-c:v","libx264","-preset","ultrafast","-crf","12","-g","999","-c:a","copy",variable],null,default);
        var engine=new VideoEngine(ffmpeg,ffprobe);
        foreach(var input in new[]{source,variable})
        {
        string kind=input==source?"CFR":"VFR";
        var reference=await Decode(ffmpeg,input,folder,"referência-"+kind);
        foreach(var options in new[]{new VideoOptions(Operation.Split,SplitMode:SplitMode.Parts,SplitValue:2),new VideoOptions(Operation.Split,SplitMode:SplitMode.Duration,SplitValue:.05),new VideoOptions(Operation.Split,SplitMode:SplitMode.Size,SplitValue:.08)})
        {
            var parts=await engine.ProcessAsync(input,options,new IgnoreProgress(),default);
            var joined=new List<byte>();
            double audioTotal=0;
            for(int i=0;i<parts.Count;i++)
            {
                joined.AddRange(await Decode(ffmpeg,parts[i],folder,$"{kind}-{options.SplitMode}-{i}"));
                using var metadata=JsonDocument.Parse(await VideoEngine.ExecuteAsync(ffprobe,["-v","error","-show_streams","-of","json",parts[i]],null,default));
                var streams=metadata.RootElement.GetProperty("streams").EnumerateArray().ToArray();
                var video=streams.First(s=>s.GetProperty("codec_type").GetString()=="video");
                var audio=streams.First(s=>s.GetProperty("codec_type").GetString()=="audio");
                double Duration(JsonElement s)=>double.Parse(s.GetProperty("duration").GetString()!,CultureInfo.InvariantCulture);
                Check(Math.Abs(Duration(video)-Duration(audio))<.13,$"{kind}/{options.SplitMode}: duração de áudio/vídeo divergiu no trecho {i+1}");
                audioTotal+=Duration(audio);
                await VideoEngine.ExecuteAsync(ffmpeg,["-v","error","-i",parts[i],"-map","0:a:0","-f","null","NUL"],null,default);
            }
            Check(Math.Abs(audioTotal-8)<.15,$"{options.SplitMode}: duração total do áudio divergiu do original");
            Check(joined.Count==reference.Length,$"{kind}/{options.SplitMode}: esperados {reference.Length} quadros; encontrados {joined.Count}. Há perda ou repetição nos cortes.");
            for(int i=0;i<reference.Length;i++) Check(Math.Abs(joined[i]-reference[i])<=3,$"{kind}/{options.SplitMode}: sequência visual divergente no quadro {i}");
        }
        }
        Console.WriteLine("PASS: sequência de quadros marcada CFR/VFR preservada nas três divisões; áudio decodificável e durações A/V dentro das tolerâncias.");
    }
    private static async Task<byte[]> Decode(string ffmpeg,string path,string folder,string id)
    {
        string output=Path.Combine(folder,id+".decoded.gray");
        await VideoEngine.ExecuteAsync(ffmpeg,["-y","-v","error","-i",path,"-map","0:v:0","-vf","scale=1:1,format=gray","-fps_mode","passthrough","-f","rawvideo",output],null,default);
        return await File.ReadAllBytesAsync(output);
    }
    private static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
    private sealed class IgnoreProgress : IProgress<VideoProgress> { public void Report(VideoProgress p){} }
}
