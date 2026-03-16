using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace NightMode
{
    /// <summary>
    /// Overlay yöntemi: Ekran üzerine yarı saydam siyah bir pencere koyar.
    /// Gamma desteklenmediğinde veya yetersiz kaldığında devreye girer.
    /// Bu yöntemde z-order kaybı yaşanmaması için WS_EX_LAYERED + HWND_TOPMOST
    /// ve WinEventHook ile pencere takibi yapılır.
    /// </summary>
    public class OverlayDimmer : IDisposable
    {
        // Win32
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint LWA_ALPHA = 0x00000002;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private readonly Form[] _overlays;
        private IntPtr _hookHandle = IntPtr.Zero;
        private WinEventDelegate _hookDelegate; // GC'den korumak için referans tut
        private bool _disposed = false;
        private byte _currentAlpha = 0;
        private bool _active = false;

        public OverlayDimmer()
        {
            var screens = Screen.AllScreens;
            _overlays = new Form[screens.Length];

            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var form = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    BackColor = Color.Black,
                    Opacity = 0,
                    ShowInTaskbar = false,
                    TopMost = true,
                    StartPosition = FormStartPosition.Manual,
                    Bounds = screen.Bounds,
                    Cursor = Cursors.Default
                };

                // Tüm mouse/keyboard olaylarını geçir, tıklanabilir olmasın
                form.Load += (s, e) =>
                {
                    // WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE
                    int exStyle = GetWindowLong(form.Handle, -20);
                    SetWindowLong(form.Handle, -20, exStyle | 0x00000020 | 0x00080000 | 0x08000000);
                };

                _overlays[i] = form;
            }
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public void Show()
        {
            foreach (var f in _overlays)
                if (!f.Visible) f.Show();

            BringToTop();
            InstallHook();
            _active = true;
        }

        public void Hide()
        {
            _active = false;
            UninstallHook();
            foreach (var f in _overlays)
                if (f.Visible) f.Hide();
        }

        /// <summary>alpha: 0-245 (0=görünmez, 245=çok koyu)</summary>
        public void SetAlpha(byte alpha)
        {
            _currentAlpha = alpha;
            foreach (var f in _overlays)
            {
                if (f.IsHandleCreated)
                    f.Invoke((Action)(() => f.Opacity = alpha / 255.0));
            }
        }

        private void BringToTop()
        {
            foreach (var f in _overlays)
            {
                if (f.IsHandleCreated)
                {
                    SetWindowPos(f.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }

        private void InstallHook()
        {
            if (_hookHandle != IntPtr.Zero) return;
            _hookDelegate = OnWinEvent;
            _hookHandle = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _hookDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        private void UninstallHook()
        {
            if (_hookHandle == IntPtr.Zero) return;
            UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!_active) return;

            // Foreground pencere değiştiğinde overlay'i tekrar üste al
            // Kendi overlay pencereleri değilse
            bool isOwnWindow = false;
            foreach (var f in _overlays)
            {
                if (f.Handle == hwnd) { isOwnWindow = true; break; }
            }

            if (!isOwnWindow)
            {
                // Küçük gecikme ile üste al (bazı uygulamalar render sonrası z-order alıyor)
                System.Threading.Timer timer = null;
                timer = new System.Threading.Timer(_ =>
                {
                    timer?.Dispose();
                    try
                    {
                        foreach (var f in _overlays)
                        {
                            if (f.IsHandleCreated && !f.IsDisposed)
                                f.BeginInvoke((Action)BringToTop);
                        }
                    }
                    catch { }
                }, null, 50, System.Threading.Timeout.Infinite);
            }
        }

        public void UpdateScreens()
        {
            // Ekran sayısı değişirse yeniden başlatılmalı (basit versiyon)
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UninstallHook();
            foreach (var f in _overlays)
            {
                if (!f.IsDisposed)
                    f.Invoke((Action)f.Dispose);
            }
        }
    }
}
