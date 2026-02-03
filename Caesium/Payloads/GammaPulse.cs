using System.Runtime.InteropServices;

namespace Caesium.Payloads;

public abstract partial class GammaPulse : IPayload
{
    [LibraryImport("user32.dll")]
    private static partial IntPtr GetDC(IntPtr hWnd);
    
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDeviceGammaRamp(IntPtr hDC, in ushort lpRamp);

// This would be called in a background thread
    public static void Execute()
    {
        var hdc = GetDC(IntPtr.Zero);
        var ramp = new ushort[3 * 256];
        float t = 0;

        while (true)
        {
            t += 0.1f;
            var pulse = (MathF.Sin(t) + 1.0f) / 2.0f; // 0.0 to 1.0

            for (var i = 0; i < 256; i++)
            {
                // Red and Green stay low
                ramp[i] = (ushort)(i * 128); 
                ramp[i + 256] = (ushort)(i * 128);
                // Blue pulses aggressively
                ramp[i + 512] = (ushort)(i * (256 + (pulse * 255)));
            }
            SetDeviceGammaRamp(hdc, ramp[0]);
            Thread.Sleep(30);
        }
    }
}