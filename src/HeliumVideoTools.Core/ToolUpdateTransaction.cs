using System.Text.Json;
using System.Text.RegularExpressions;

namespace HeliumVideoTools.Core;

// Paths are derived from a validated ID under the store, never read from a journal.
internal sealed class ToolUpdateTransaction:IDisposable,IAsyncDisposable
{
    private readonly MediaToolStore store;
    private readonly FileStream lease;
    private readonly string journal;
    private bool committed;
    internal string Id {get;}
    internal string DirectoryPath=>Path.Combine(store.Root,Id);
    internal string ArchivePath=>Path.Combine(store.Root,Id+".zip");
    private ToolUpdateTransaction(MediaToolStore store,string id,FileStream lease,string journal)
    {this.store=store;Id=id;this.lease=lease;this.journal=journal;}
    internal static ToolUpdateTransaction Begin(MediaToolStore store)
    {
        RecoveryJournal.CheckParents(store.Root);
        string folder=Path.Combine(store.Root,"transactions");Directory.CreateDirectory(folder);RecoveryJournal.CheckParents(folder);
        string id=Guid.NewGuid().ToString("N"),journal=Path.Combine(folder,id+".json");
        var lease=new FileStream(journal,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
        try{JsonSerializer.Serialize(lease,id);lease.Flush(true);return new(store,id,lease,journal);}
        catch{lease.Dispose();File.Delete(journal);throw;}
    }
    internal void Commit()=>committed=true;
    private static async Task<bool> CleanAsync(MediaToolStore store,string id,bool committed=false)
    {
        RecoveryJournal.CheckParents(store.Root);
        string directory=Path.Combine(store.Root,id);
        foreach(string file in Directory.EnumerateFiles(store.Root,id+".zip*",SearchOption.TopDirectoryOnly))
        {
            string name=Path.GetFileName(file);
            if(name!=id+".zip" && !Regex.IsMatch(name,"^"+id+@"\.zip\.[a-f0-9]{32}\.partial$"))continue;
            if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return false;
            await RemoveAsync(()=>File.Delete(file)).ConfigureAwait(false);
        }
        if(!Directory.Exists(directory) || committed || store.References(id))return true;
        RecoveryJournal.CheckParents(directory);
        var files=Directory.GetFileSystemEntries(directory);
        if(files.Any(file=>(File.GetAttributes(file)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0 || Path.GetFileName(file) is not "ffmpeg.exe" and not "ffprobe.exe" and not "LICENSE"))return false;
        foreach(string file in files)await RemoveAsync(()=>File.Delete(file)).ConfigureAwait(false);
        await RemoveAsync(()=>Directory.Delete(directory,false)).ConfigureAwait(false);return true;
    }
    private static async Task RemoveAsync(Action remove)
    {
        for(int attempt=0;;attempt++)
        {
            try{remove();return;}
            catch(IOException ex) when(attempt<7 && (ex.HResult&0xffff) is 32 or 33)
            {await Task.Delay(50*(attempt+1)).ConfigureAwait(false);}
        }
    }
    public void Dispose()
    {
        bool clean=false;
        try{clean=CleanAsync(store,Id,committed).GetAwaiter().GetResult();}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){AppLog.Write("WARNING","Transação de ferramentas pendente",ex.Message);}
        finally{lease.Dispose();}
        if(clean)try{File.Delete(journal);}catch(IOException){}
    }
    public ValueTask DisposeAsync()=>new(Task.Run(Dispose));
    internal static async Task<RecoveryResult> RecoverAsync(MediaToolStore store,CancellationToken token)
    {
        int cleaned=0,deferred=0,invalid=0;
        string folder=Path.Combine(store.Root,"transactions");
        if(!Directory.Exists(folder))return new(0,0,0);
        RecoveryJournal.CheckParents(folder);
        foreach(string journal in Directory.EnumerateFiles(folder,"*.json").Take(500))
        {
            token.ThrowIfCancellationRequested();bool clean=false;
            try
            {
                if((File.GetAttributes(journal)&FileAttributes.ReparsePoint)!=0){invalid++;continue;}
                using(var lease=new FileStream(journal,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                {
                    if(lease.Length>256){invalid++;continue;}
                    string? id=await JsonSerializer.DeserializeAsync<string>(lease,cancellationToken:token);
                    if(id is null || !Regex.IsMatch(id,"^[a-f0-9]{32}$") || Path.GetFileNameWithoutExtension(journal)!=id){invalid++;continue;}
                    clean=await CleanAsync(store,id);
                }
                if(clean){File.Delete(journal);cleaned++;}else deferred++;
            }
            catch(JsonException){invalid++;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){deferred++;AppLog.Write("INFO","Limpeza da transação de ferramentas adiada",ex.Message);}
        }
        return new(cleaned,deferred,invalid);
    }
}
