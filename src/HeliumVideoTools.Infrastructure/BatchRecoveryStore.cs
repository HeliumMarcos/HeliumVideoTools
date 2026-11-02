using System.Text.Json;
using System.Text.RegularExpressions;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Infrastructure;

public sealed record RecoveredBatch(BatchResult Result,string ReportPath,DateTimeOffset CreatedUtc);

// Journal metadata only. Recovery never writes to, deletes or resumes a video.
public sealed class BatchRecoveryStore(string directory,string reports)
{
    private readonly string root=RecoveryJournal.PhysicalDirectory(directory);
    private readonly string reports=RecoveryJournal.PhysicalDirectory(reports);
    public static BatchRecoveryStore Default {get;}=new(Path.Combine(AppLog.Root,"batches"),Path.Combine(AppLog.Root,"reports"));
    internal sealed record Output(string Path,long Bytes,long Modified);
    internal sealed record Item(string Source,bool Started,BatchItemState? State,Output[] Outputs,string? Error);
    internal sealed record Snapshot(int Schema,string Id,DateTimeOffset CreatedUtc,Item[] Items);
    private static bool Id(string value)=>Regex.IsMatch(value,"^[a-f0-9]{32}$");
    public BatchLease Begin(string[] sources)
    {
        if(sources.Length is 0 or >10000)throw new IOException("Quantidade de arquivos inválida para registro do lote.");
        Directory.CreateDirectory(root);RecoveryJournal.CheckParents(root);
        var data=new Snapshot(1,Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,sources.Select(s=>new Item(Path.GetFullPath(s),false,null,[],null)).ToArray());
        var lease=new FileStream(Path.Combine(root,data.Id+".lease"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
        try{var result=new BatchLease(root,data,lease);result.Save();return result;}
        catch{lease.Dispose();throw;}
    }
    public Task<IReadOnlyList<RecoveredBatch>> RecoverAsync(CancellationToken token=default)=>Task.Run<IReadOnlyList<RecoveredBatch>>(async ()=>
    {
        var results=new List<RecoveredBatch>();if(!Directory.Exists(root))return results;
        RecoveryJournal.CheckParents(root);
        foreach(var path in Directory.EnumerateFiles(root,"*.json").Take(500))
        {
            token.ThrowIfCancellationRequested();string id=Path.GetFileNameWithoutExtension(path);
            if(!Id(id))continue;
            FileStream? lease=null;
            try
            {
                string leasePath=Path.Combine(root,id+".lease");
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0 || File.Exists(leasePath) && (File.GetAttributes(leasePath)&FileAttributes.ReparsePoint)!=0)continue;
                lease=new FileStream(leasePath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
                if(new FileInfo(path).Length>8_000_000)continue;
                var data=JsonSerializer.Deserialize<Snapshot>(await File.ReadAllTextAsync(path,token))??throw new IOException("Registro vazio.");
                Validate(data,id);
                var items=data.Items.Select(item=>
                {
                    var outputs=item.Outputs.Where(o=>Matches(o)).Select(o=>o.Path).ToArray();
                    bool changed=outputs.Length!=item.Outputs.Length;
                    var state=item.State??(item.Started?BatchItemState.Cancelled:BatchItemState.NotStarted);
                    if(changed && state==BatchItemState.Succeeded)state=BatchItemState.Failed;
                    string? error=changed?"Uma saída registrada não está disponível ou mudou desde a interrupção.":item.Error??(item.State is null?item.Started?"Processamento interrompido inesperadamente. Saídas já publicadas foram preservadas.":"Não iniciado: aplicativo interrompido.":null);
                    return new BatchItemResult(item.Source,state,outputs,error);
                }).ToArray();
                var result=new BatchResult(items,true);
                string report=await BatchReportStore.SaveAsync(result,reports,"recuperado-"+id,data.CreatedUtc);
                File.Delete(path); // remove only after both deterministic reports exist
                results.Add(new(result,Path.ChangeExtension(report,".txt"),data.CreatedUtc));
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
            {AppLog.Write("WARNING","Recuperação de lote adiada",ex.Message);}
            finally
            {
                lease?.Dispose();
                if(lease is not null && !File.Exists(path))try{File.Delete(Path.Combine(root,id+".lease"));}catch(IOException){}
            }
        }
        return results.OrderBy(r=>r.CreatedUtc).ToArray();
    },token);
    private static bool Matches(Output output)
    {
        try
        {
            RecoveryJournal.CheckParents(Path.GetDirectoryName(output.Path)!);
            var file=new FileInfo(output.Path);
            return file.Exists && (file.Attributes&FileAttributes.ReparsePoint)==0 && file.Length==output.Bytes && file.LastWriteTimeUtc.Ticks==output.Modified;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return false;}
    }
    private static void Validate(Snapshot data,string id)
    {
        if(data.Schema!=1 || data.Id!=id || data.Items is null || data.Items.Length is 0 or >10000)throw new IOException("Registro de lote inválido.");
        foreach(var item in data.Items)
        {
            if(item is null || !Path.IsPathFullyQualified(item.Source) || item.Outputs is null || item.Outputs.Length>10000 || item.State.HasValue && !Enum.IsDefined(item.State.Value))throw new IOException("Item de lote inválido.");
            foreach(var output in item.Outputs)
                if(output is null || !Path.IsPathFullyQualified(output.Path) || output.Bytes<=0 || output.Modified<=0)throw new IOException("Saída de lote inválida.");
        }
    }
    public sealed class BatchLease:IDisposable
    {
        private readonly string root;
        private readonly Snapshot data;
        private readonly FileStream lease;
        private readonly object gate=new();
        private bool disposed;
        internal BatchLease(string root,Snapshot data,FileStream lease){this.root=root;this.data=data;this.lease=lease;}
        internal void Save()
        {
            ObjectDisposedException.ThrowIf(disposed,this);RecoveryJournal.CheckParents(root);
            string path=Path.Combine(root,data.Id+".json"),temp=path+".tmp";
            if(File.Exists(temp) && (File.GetAttributes(temp)&FileAttributes.ReparsePoint)!=0)throw new IOException("Registro temporário redirecionado.");
            using(var output=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            {JsonSerializer.Serialize(output,data);if(output.Length>8_000_000)throw new IOException("Registro do lote excedeu o limite de recuperação.");output.Flush(true);}
            File.Move(temp,path,true);
        }
        public void Start(int index){lock(gate){data.Items[index]=data.Items[index] with {Started=true};Save();}}
        public void Published(int index,string path)
        {
            lock(gate)
            {
                var file=new FileInfo(Path.GetFullPath(path));if(!file.Exists || file.Length<=0)throw new IOException("Saída publicada indisponível para registro.");
                var old=data.Items[index];
                if(old.Outputs.Any(o=>o.Path.Equals(file.FullName,StringComparison.OrdinalIgnoreCase)))return;
                data.Items[index]=old with {Outputs=[..old.Outputs,new(file.FullName,file.Length,file.LastWriteTimeUtc.Ticks)]};Save();
            }
        }
        public void Finish(int index,BatchItemResult result){lock(gate){data.Items[index]=data.Items[index] with {State=result.State,Error=result.Error};Save();}}
        public void Complete(){lock(gate){File.Delete(Path.Combine(root,data.Id+".json"));}}
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;lease.Dispose();try{File.Delete(Path.Combine(root,data.Id+".lease"));}catch(IOException){}}}
    }
}
