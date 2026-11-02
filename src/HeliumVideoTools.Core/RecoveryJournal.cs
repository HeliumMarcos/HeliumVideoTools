using System.Text.Json;
using System.Text.RegularExpressions;

namespace HeliumVideoTools.Core;

public sealed record RecoveryResult(int Cleaned,int Deferred,int Invalid);

// Only artifacts explicitly registered before creation may be recovered.
// An exclusive open journal protects a running operation in any process.
public sealed class RecoveryJournal(string root)
{
    private readonly string journalRoot=PhysicalDirectory(root);
    public static RecoveryJournal Default { get; } = new(Path.Combine(AppLog.Root,"recovery"));
    private sealed record Entry(string Id,string Directory,string Kind,string Extension);
    public TrackedTemporary TrackFile(string directory,string extension)=>Track(directory,"video",extension);
    public TrackedTemporary TrackPassDirectory(string directory)=>Track(directory,"passes","");
    private TrackedTemporary Track(string directory,string kind,string extension)
    {
        var entry=new Entry(Guid.NewGuid().ToString("N"),PhysicalDirectory(directory),kind,extension);
        Validate(entry);
        Directory.CreateDirectory(journalRoot);CheckParents(journalRoot);
        string journal=Path.Combine(journalRoot,entry.Id+".json");
        var lease=new FileStream(journal,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
        try {JsonSerializer.Serialize(lease,entry);lease.Flush(true);return new(journal,lease,Artifact(entry),()=>Clean(entry));}
        catch {lease.Dispose();File.Delete(journal);throw;}
    }
    private static string Artifact(Entry entry)=>Path.Combine(entry.Directory,entry.Kind=="video"?$".hvt-{entry.Id}.partial{entry.Extension}":$"hvt-{entry.Id}");
    private static void Validate(Entry entry)
    {
        if(!Regex.IsMatch(entry.Id??"","^[a-f0-9]{32}$") || entry.Directory is null || !Path.IsPathFullyQualified(entry.Directory) ||
            entry.Kind is not "video" and not "passes" || (entry.Kind=="video"?entry.Extension is not ".mp4" and not ".webm":entry.Extension!=""))
            throw new IOException("Registro de recuperação inválido.");
        CheckParents(entry.Directory);
    }
    internal static void CheckParents(string directory)
    {
        for(DirectoryInfo? current=new(Path.GetFullPath(directory));current is not null;current=current.Parent)
            if(current.Exists && (current.Attributes&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Recuperação não acessa diretórios redirecionados.");
    }
    internal static string PhysicalDirectory(string directory)
    {
        string full=Path.GetFullPath(directory);
        for(int hop=0;hop<16;hop++)
        {
            DirectoryInfo? link=null;
            for(DirectoryInfo? current=new(full);current is not null;current=current.Parent)
                if(current.Exists && (current.Attributes&FileAttributes.ReparsePoint)!=0){link=current;break;}
            if(link is null)return full;
            var target=link.ResolveLinkTarget(true)??throw new IOException("Não foi possível resolver o diretório de temporários.");
            full=Path.GetFullPath(Path.Combine(target.FullName,Path.GetRelativePath(link.FullName,full)));
        }
        throw new IOException("Redirecionamentos demais no diretório de temporários.");
    }
    private static bool Clean(Entry entry)
    {
        Validate(entry);string path=Artifact(entry);
        if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Temporário redirecionado.");
        if(entry.Kind=="video") {File.Delete(path);return true;}
        if(!System.IO.Directory.Exists(path))return true;
        CheckParents(path);
        // Do not recursively remove a directory with unknown contents or links.
        var files=System.IO.Directory.GetFileSystemEntries(path);
        var allowed=new HashSet<string>(StringComparer.Ordinal){"pass","pass.cutree","pass.temp","pass.cutree.temp","pass-0.log","pass-0.log.mbtree","pass-0.log.temp","pass-0.log.mbtree.temp"};
        if(files.Any(file=>(File.GetAttributes(file)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0 || !allowed.Contains(Path.GetFileName(file))))return false;
        foreach(string file in files)File.Delete(file);
        System.IO.Directory.Delete(path,false);return true;
    }
    public Task<RecoveryResult> RecoverAsync(CancellationToken token=default)=>Task.Run(()=>
    {
        int cleaned=0,deferred=0,invalid=0;
        if(!System.IO.Directory.Exists(journalRoot))return new RecoveryResult(0,0,0);
        CheckParents(journalRoot);
        foreach(string path in System.IO.Directory.EnumerateFiles(journalRoot,"*.json").Take(500))
        {
            token.ThrowIfCancellationRequested();bool remove=false;
            try
            {
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0){invalid++;continue;}
                using(var lease=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                {
                    if(lease.Length>8192){invalid++;continue;}
                    var entry=JsonSerializer.Deserialize<Entry>(lease)??throw new JsonException();
                    Validate(entry);
                    if(Path.GetFileNameWithoutExtension(path)!=entry.Id){invalid++;continue;}
                    if(Clean(entry)){remove=true;cleaned++;}else deferred++;
                }
                if(remove)File.Delete(path);
            }
            catch(JsonException){invalid++;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException){deferred++;}
        }
        return new RecoveryResult(cleaned,deferred,invalid);
    },token);
}

public sealed class TrackedTemporary:IDisposable,IAsyncDisposable
{
    private readonly string journal;
    private readonly FileStream lease;
    private readonly Func<bool> clean;
    private int disposed;
    internal TrackedTemporary(string journal,FileStream lease,string path,Func<bool> clean)
    {this.journal=journal;this.lease=lease;Path=path;this.clean=clean;}
    public string Path { get; }
    public ValueTask DisposeAsync()=>new(Task.Run(Dispose));
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        bool removed=false;
        try {removed=clean();}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){AppLog.Write("WARNING","Temporário pendente de recuperação",ex.Message);}
        finally {lease.Dispose();}
        if(removed)try{File.Delete(journal);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){AppLog.Write("WARNING","Registro de recuperação pendente",ex.Message);}
    }
}
