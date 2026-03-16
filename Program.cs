using System;
using System.Windows.Forms;

namespace NightMode
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Parse startup brightness argument (e.g. nightmode.exe 30)
            int startupBrightness = 70; // default %70 brightness
            if (args.Length > 0 && int.TryParse(args[0], out int val))
                startupBrightness = Math.Max(10, Math.Min(100, val));

            Application.Run(new NightModeContext(startupBrightness));
        }
    }
}
