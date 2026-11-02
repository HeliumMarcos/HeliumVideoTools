using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HeliumVideoTools.Core;

// Unnamed, non-inheritable job: only this operation's children are members.
// Windows closes the handle even when this host is terminated forcibly.
internal sealed class OwnedProcessGroup:IDisposable
{
    private readonly JobHandle handle;
    internal OwnedProcessGroup()
    {
        handle=CreateJobObject(0,null);
        if(handle.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());
        var limits=new ExtendedLimits();limits.Basic.Flags=0x2000; // KILL_ON_JOB_CLOSE
        if(!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf<ExtendedLimits>()))
        {int error=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(error);}
    }
    internal void Attach(Process process)
    {
        if(AssignProcessToJobObject(handle,process.Handle))return;
        int error=Marshal.GetLastWin32Error();
        if(process.HasExited)return; // e.g. a fast -version command
        try{process.Kill(true);}catch(InvalidOperationException){}
        throw new IOException("Não foi possível proteger o processo de vídeo contra encerramento inesperado.",new Win32Exception(error));
    }
    public void Dispose()=>handle.Dispose();
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {public long ProcessTime,JobTime;public uint Flags;public nuint MinWorkingSet,MaxWorkingSet;public uint ActiveLimit;public nuint Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters {public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits {public BasicLimits Basic;public IoCounters Io;public nuint ProcessMemory,JobMemory,PeakProcess,PeakJob;}
    private sealed class JobHandle:SafeHandleZeroOrMinusOneIsInvalid
    {public JobHandle():base(true){}protected override bool ReleaseHandle()=>CloseHandle(handle);}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern JobHandle CreateJobObject(nint attributes,string? name);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(JobHandle job,int info,ref ExtendedLimits limits,uint length);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(JobHandle job,nint process);
    [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
