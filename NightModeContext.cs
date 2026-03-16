using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NightMode
{
    public class NightModeContext : ApplicationContext
    {
        // ── Win32 Hotkey ──────────────────────────────────────────────
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_WIN     = 0x0008;
        private const uint VK_F9       = 0x78;
        private const uint VK_F10      = 0x79;
        private const uint VK_F11      = 0x7A;
        private const uint VK_F12      = 0x7B;

        private const int HK_DIM_MORE   = 1;
        private const int HK_DIM_LESS   = 2;
        private const int HK_BOSS       = 3;
        private const int HK_MAX_BRIGHT = 4;
        private const int WM_HOTKEY     = 0x0312;

        // ── State ──────────────────────────────────────────────────────
        private int  _brightness;          // 10–100
        private bool _enabled = true;
        private bool _useGamma = false;    // gamma mı overlay mi?

        private NotifyIcon   _trayIcon;
        private ContextMenuStrip _menu;
        private TrackBar     _slider;
        private ToolStripControlHost _sliderHost;
        private HotkeyWindow _hotkeyWindow;
        private OverlayDimmer _overlay;

        // Önceki "boss" değeri
        private int _preBossValue = 70;

        public NightModeContext(int startBrightness)
        {
            _brightness = startBrightness;

            // 1) Gamma dene
            _useGamma = GammaRampDimmer.TrySaveOriginal();

            // 2) Gamma yoksa overlay hazırla
            if (!_useGamma)
                _overlay = new OverlayDimmer();

            // 3) Tray
            BuildTray();

            // 4) Hotkey penceresi
            _hotkeyWindow = new HotkeyWindow(this);
            RegisterHotKey(_hotkeyWindow.Handle, HK_DIM_MORE,   MOD_CONTROL | MOD_WIN, VK_F11);
            RegisterHotKey(_hotkeyWindow.Handle, HK_DIM_LESS,   MOD_CONTROL | MOD_WIN, VK_F12);
            RegisterHotKey(_hotkeyWindow.Handle, HK_BOSS,       MOD_CONTROL | MOD_WIN, VK_F9);
            RegisterHotKey(_hotkeyWindow.Handle, HK_MAX_BRIGHT, MOD_CONTROL | MOD_WIN, VK_F10);

            // 5) İlk uygulama
            Apply();

            // 6) Overlay varsa göster
            if (!_useGamma)
            {
                _overlay.Show();
                ApplyOverlay();
            }
        }

        // ── Tray Menü ─────────────────────────────────────────────────
        private void BuildTray()
        {
            _menu = new ContextMenuStrip();

            // Başlık (tıklanamaz)
            var header = new ToolStripLabel("🌙 Night Mode")
            {
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 120, 200)
            };
            _menu.Items.Add(header);
            _menu.Items.Add(new ToolStripSeparator());

            // Slider
            _slider = new TrackBar
            {
                Minimum = 10,
                Maximum = 100,
                TickFrequency = 10,
                SmallChange = 5,
                LargeChange = 10,
                Value = _brightness,
                Width = 200
            };
            _slider.Scroll += (s, e) =>
            {
                _brightness = _slider.Value;
                UpdateLabel();
                Apply();
                if (!_useGamma) ApplyOverlay();
            };
            _sliderHost = new ToolStripControlHost(_slider);
            _menu.Items.Add(_sliderHost);

            // Parlaklık etiketi
            var lblItem = new ToolStripLabel(BrightnessText())
            {
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Name = "lblBrightness"
            };
            _menu.Items.Add(lblItem);
            _menu.Items.Add(new ToolStripSeparator());

            // Açık/Kapalı toggle
            var toggleItem = new ToolStripMenuItem("⏸  Devre Dışı Bırak")
            {
                Name = "toggleItem"
            };
            toggleItem.Click += ToggleEnabled;
            _menu.Items.Add(toggleItem);

            // Yöntem bilgisi
            string method = _useGamma ? "🎮 Gamma Ramp (aktif)" : "🪟 Overlay (aktif)";
            _menu.Items.Add(new ToolStripLabel(method)
            {
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.Gray,
                Name = "lblMethod"
            });

            _menu.Items.Add(new ToolStripSeparator());

            // Başlangıçta çalıştır
            var startupItem = new ToolStripMenuItem("🚀  Windows başlangıcında çalıştır")
            {
                Name = "startupItem",
                Checked = IsInStartup()
            };
            startupItem.Click += ToggleStartup;
            _menu.Items.Add(startupItem);

            _menu.Items.Add(new ToolStripSeparator());

            // Kısayol bilgisi
            _menu.Items.Add(new ToolStripLabel("Kısayollar:")
            {
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.Gray
            });
            _menu.Items.Add(new ToolStripLabel("Ctrl+Win+F11 → Karart")
            {
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.Gray
            });
            _menu.Items.Add(new ToolStripLabel("Ctrl+Win+F12 → Aydınlat")
            {
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.Gray
            });
            _menu.Items.Add(new ToolStripLabel("Ctrl+Win+F9  → Boss key (max karanlık)")
            {
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.Gray
            });
            _menu.Items.Add(new ToolStripLabel("Ctrl+Win+F10 → Max parlaklık")
            {
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.Gray
            });

            _menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("✖  Çıkış");
            exitItem.Click += (s, e) => Exit();
            _menu.Items.Add(exitItem);

            // Tray icon
            _trayIcon = new NotifyIcon
            {
                Icon  = BuildIcon(_brightness),
                Text  = $"Night Mode — %{_brightness}",
                ContextMenuStrip = _menu,
                Visible = true
            };

            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ToggleEnabled(s, e);
            };
        }

        // ── Parlaklık Uygula ─────────────────────────────────────────
        private void Apply()
        {
            int effective = _enabled ? _brightness : 100;
            _trayIcon?.Invoke(new Action(() =>
            {
                _trayIcon.Icon = BuildIcon(_enabled ? _brightness : 100);
                _trayIcon.Text = _enabled
                    ? $"Night Mode — %{_brightness}"
                    : "Night Mode — Devre dışı";
            }));

            if (_useGamma)
                GammaRampDimmer.SetBrightness(effective);
        }

        private void ApplyOverlay()
        {
            if (_overlay == null) return;
            if (!_enabled)
            {
                _overlay.SetAlpha(0);
                return;
            }
            // brightness 100 → alpha 0, brightness 10 → alpha 220
            int alpha = (int)((100 - _brightness) / 90.0 * 220);
            alpha = Math.Max(0, Math.Min(220, alpha));
            _overlay.SetAlpha((byte)alpha);
        }

        // ── Hotkey İşlemleri ─────────────────────────────────────────
        public void HandleHotkey(int id)
        {
            switch (id)
            {
                case HK_DIM_MORE:
                    SetBrightness(_brightness - 5);
                    break;
                case HK_DIM_LESS:
                    SetBrightness(_brightness + 5);
                    break;
                case HK_BOSS:
                    _preBossValue = _brightness;
                    SetBrightness(10);
                    break;
                case HK_MAX_BRIGHT:
                    SetBrightness(_preBossValue > 0 ? _preBossValue : 100);
                    break;
            }
        }

        private void SetBrightness(int value)
        {
            _brightness = Math.Max(10, Math.Min(100, value));
            if (_slider.InvokeRequired)
                _slider.Invoke((Action)(() => _slider.Value = _brightness));
            else
                _slider.Value = _brightness;

            UpdateLabel();
            Apply();
            if (!_useGamma) ApplyOverlay();
        }

        // ── Toggle Enabled ───────────────────────────────────────────
        private void ToggleEnabled(object sender, EventArgs e)
        {
            _enabled = !_enabled;
            var item = _menu.Items["toggleItem"] as ToolStripMenuItem;
            if (item != null)
                item.Text = _enabled ? "⏸  Devre Dışı Bırak" : "▶  Etkinleştir";

            Apply();
            if (!_useGamma) ApplyOverlay();
        }

        // ── Startup ──────────────────────────────────────────────────
        private void ToggleStartup(object sender, EventArgs e)
        {
            var item = _menu.Items["startupItem"] as ToolStripMenuItem;
            if (item == null) return;

            if (IsInStartup())
            {
                RemoveFromStartup();
                item.Checked = false;
            }
            else
            {
                AddToStartup();
                item.Checked = true;
            }
        }

        private static bool IsInStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue("NightMode") != null;
        }

        private static void AddToStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key?.SetValue("NightMode", $"\"{Application.ExecutablePath}\"");
        }

        private static void RemoveFromStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("NightMode", false);
        }

        // ── Label & Icon ─────────────────────────────────────────────
        private string BrightnessText() => $"Parlaklık: %{_brightness}";

        private void UpdateLabel()
        {
            var lbl = _menu.Items["lblBrightness"] as ToolStripLabel;
            if (lbl != null) lbl.Text = BrightnessText();
        }

        private static Icon BuildIcon(int brightness)
        {
            // Ay simgesi, %brightness ile orantılı doluluk
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                int shade = (int)(brightness / 100.0 * 220) + 35;
                shade = Math.Min(255, shade);
                var brush = new SolidBrush(Color.FromArgb(shade, shade, Math.Max(0, shade - 40)));

                // Ay: büyük daire eksi küçük daire
                g.FillEllipse(brush, 2, 2, 12, 12);
                g.FillEllipse(new SolidBrush(Color.FromArgb(0, 0, 0, 0)), 5, 1, 10, 10);

                brush.Dispose();
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        // ── Çıkış ────────────────────────────────────────────────────
        private void Exit()
        {
            UnregisterHotKey(_hotkeyWindow.Handle, HK_DIM_MORE);
            UnregisterHotKey(_hotkeyWindow.Handle, HK_DIM_LESS);
            UnregisterHotKey(_hotkeyWindow.Handle, HK_BOSS);
            UnregisterHotKey(_hotkeyWindow.Handle, HK_MAX_BRIGHT);

            GammaRampDimmer.Restore();
            _overlay?.Hide();
            _overlay?.Dispose();
            _trayIcon.Visible = false;
            Application.Exit();
        }
    }

    // ── Gizli hotkey penceresi ────────────────────────────────────────
    public class HotkeyWindow : NativeWindow
    {
        private readonly NightModeContext _ctx;
        private const int WM_HOTKEY = 0x0312;

        public HotkeyWindow(NightModeContext ctx)
        {
            _ctx = ctx;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
                _ctx.HandleHotkey(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }
}
