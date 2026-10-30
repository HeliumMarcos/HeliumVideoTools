using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) return 2;
            if (args[0] == "--stamp-shortcut") { Stamp(args[1]); return 0; }
            if (args[0] != "--close") return 2;
            var expected = Path.GetFullPath(args[1]);
            if (!expected.EndsWith(Path.Combine("HeliumVideoTools", "app", "HeliumVideoTools.exe"), StringComparison.OrdinalIgnoreCase)) return 3;
            foreach (var process in Process.GetProcessesByName("HeliumVideoTools"))
            {
                using (process)
                {
                    string? image;
                    try { image = process.MainModule?.FileName; } catch { return 4; }
                    if (!string.Equals(image, expected, StringComparison.OrdinalIgnoreCase)) continue;
                    process.CloseMainWindow();
                    if (!process.WaitForExit(15000))
                    {
                        // Somente a instalação cujo caminho foi conferido pode ser encerrada.
                        if (!string.Equals(process.MainModule?.FileName, expected, StringComparison.OrdinalIgnoreCase)) return 5;
                        process.Kill(true);
                        if (!process.WaitForExit(5000)) return 6;
                    }
                }
            }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }

    private static void Stamp(string path)
    {
        if (!File.Exists(path) || !path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) throw new IOException("Atalho inválido");
        var shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
        try
        {
            var file = (IPersistFile)shell;
            file.Load(path, 2);
            var store = (IPropertyStore)shell;
            var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
            var value = new PropVariant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni("Helium.HeliumVideoTools") };
            try { Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref value)); Marshal.ThrowExceptionForHR(store.Commit()); file.Save(path, true); }
            finally { Marshal.FreeCoTaskMem(value.Pointer); }
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
}
