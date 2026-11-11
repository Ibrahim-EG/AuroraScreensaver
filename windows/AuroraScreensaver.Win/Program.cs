using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace AuroraScreensaver.Win
{
    internal static class Program
    {
        /// <summary>
        /// Entry point.
        ///   .exe              -> normal preview window (fullscreen, Esc/click to quit)
        ///   .scr / /s         -> real Windows screensaver mode (fullscreen scene)
        ///   /c                -> configuration dialog (placeholder)
        ///   /p                -> small preview embed is not supported; runs fullscreen briefly
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (mode.StartsWith("/c"))
            {
                MessageBox.Show(
                    "Aurora renders a live procedural night sky; there is nothing to configure.",
                    "Aurora Screensaver",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            bool isScr = Process.GetCurrentProcess().MainModule != null &&
                         Process.GetCurrentProcess().MainModule.FileName
                             .EndsWith(".scr", StringComparison.OrdinalIgnoreCase);

            // When launched by Windows as a screensaver it arrives with /s (or just the .scr name).
            if (mode.StartsWith("/s") || (isScr && string.IsNullOrEmpty(mode)))
            {
                Application.Run(new MainForm());
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
