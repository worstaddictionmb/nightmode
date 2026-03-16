using System;
using System.Runtime.InteropServices;

namespace NightMode
{
    /// <summary>
    /// Gamma Ramp yöntemi: Donanım seviyesinde ekran rengini/parlaklığını değiştirir.
    /// Video oynatıcılar, tam ekran uygulamalar dahil tüm içeriği etkiler.
    /// Bazı monitörler/GPU sürücülerinde desteklenmeyebilir.
    /// </summary>
    public static class GammaRampDimmer
    {
        [DllImport("gdi32.dll")]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("gdi32.dll")]
        private static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [StructLayout(LayoutKind.Sequential)]
        private struct RAMP
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;
        }

        private static RAMP _originalRamp;
        private static bool _originalSaved = false;
        private static bool _supported = true;

        public static bool IsSupported => _supported;

        public static bool TrySaveOriginal()
        {
            try
            {
                IntPtr hDC = GetDC(IntPtr.Zero);
                _originalRamp = new RAMP
                {
                    Red = new ushort[256],
                    Green = new ushort[256],
                    Blue = new ushort[256]
                };
                bool ok = GetDeviceGammaRamp(hDC, ref _originalRamp);
                ReleaseDC(IntPtr.Zero, hDC);
                _originalSaved = ok;
                _supported = ok;
                return ok;
            }
            catch
            {
                _supported = false;
                return false;
            }
        }

        /// <summary>
        /// brightness: 0-100 (100 = tam parlak, 10 = en karanlık)
        /// </summary>
        public static bool SetBrightness(int brightness)
        {
            if (!_supported) return false;

            try
            {
                brightness = Math.Max(10, Math.Min(100, brightness));
                double factor = brightness / 100.0;

                RAMP ramp = new RAMP
                {
                    Red = new ushort[256],
                    Green = new ushort[256],
                    Blue = new ushort[256]
                };

                for (int i = 0; i < 256; i++)
                {
                    int val = (int)(i * 256 * factor);
                    val = Math.Max(0, Math.Min(65535, val));
                    ramp.Red[i] = ramp.Green[i] = ramp.Blue[i] = (ushort)val;
                }

                IntPtr hDC = GetDC(IntPtr.Zero);
                bool result = SetDeviceGammaRamp(hDC, ref ramp);
                ReleaseDC(IntPtr.Zero, hDC);
                return result;
            }
            catch
            {
                _supported = false;
                return false;
            }
        }

        public static void Restore()
        {
            if (!_originalSaved) return;
            try
            {
                IntPtr hDC = GetDC(IntPtr.Zero);
                SetDeviceGammaRamp(hDC, ref _originalRamp);
                ReleaseDC(IntPtr.Zero, hDC);
            }
            catch { }
        }
    }
}
