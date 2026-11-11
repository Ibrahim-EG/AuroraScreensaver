using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace AuroraScreensaver.Win
{
    /// <summary>
    /// Fullscreen borderless window that hosts the Direct3D 11 aurora scene and
    /// draws the clock/battery HUD with Direct2D/DirectWrite on top of it.
    /// Any key press, mouse move or click exits the preview (Windows itself
    /// also exits a real screensaver on input).
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly SwapChain swapChain;
        private readonly AuroraRenderer renderer;
        private readonly HudOverlay hud;
        private readonly Timer renderTimer;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        public MainForm()
        {
            try { SetProcessDPIAware(); } catch { }

            Text = "Aurora Screensaver";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
            DoubleBuffered = false;

            Bounds = Screen.FromPoint(Cursor.Position).Bounds;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            // Flip-discard swap chain: lowest latency, no tearing with vsync on.
            var present = new PresentParameters
            {
                WindowHandle = Handle,
                SwapEffect = SwapEffect.FlipDiscard,
                ModeDescription = new ModeDescription(0, 0, new Rational(60, 1),
                    SharpDX.DXGI.Format.R8G8B8A8_UNorm),
                SampleDescription = new SampleDescription(1, 0),
                BufferCount = 2,
                Usage = SharpDX.DXGI.Usage.RenderTargetOutput,
                PresentationFlags = SharpDX.DXGI.SwapChainPresentationFlag.None,
                Options = SharpDX.DXGI.SwapChainOptions.DisableAltToEnterFullScreen
            };

            var device = CreateDevice(out swapChain, present);
            renderer = new AuroraRenderer(swapChain);
            hud = new HudOverlay(device, Handle, ClientSize.Width, ClientSize.Height);

            renderTimer = new Timer { Interval = 8 }; // ~120 Hz tick; renderer paces to 60 fps
            renderTimer.Tick += (s, e) => RenderOnce();
            renderTimer.Start();
        }

        private static Device CreateDevice(out SwapChain swapChain, PresentParameters present)
        {
            DeviceCreationFlags creationFlags = DeviceCreationFlags.BgraSupport;

            FeatureLevel[] levels =
            {
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0
            };

            foreach (var level in levels)
            {
                try
                {
                    var device = new Device(DriverType.Hardware, level, creationFlags);
                    swapChain = new SwapChain(device.Factory, device, present);
                    return device;
                }
                catch (SharpDXException)
                {
                    // Try the next feature level down.
                }
            }

            // Last resort for VMs / very old drivers.
            var fallback = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            swapChain = new SwapChain(fallback.Factory, fallback, present);
            return fallback;
        }

        private void RenderOnce()
        {
            try
            {
                renderer.RenderFrame();
                hud.Draw();
            }
            catch (SharpDXException ex) when (ex.ResultCode.Failure)
            {
                // GPU reset/driver crash: close gracefully instead of hanging.
                Close();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!Visible) return;

            int w = Math.Max(1, ClientSize.Width);
            int h = Math.Max(1, ClientSize.Height);

            renderer.Resize(w, h);
            hud.Resize(w, h);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Escape quits the preview; Windows quits the real screensaver on any input.
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseDown(MouseEventArgs e) => Close();

        protected override void OnMouseMove(MouseEventArgs e)
        {
            // Ignore the tiny movement generated at startup; anything deliberate exits.
            if (e.Button != MouseButtons.None)
            {
                Close();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                renderTimer?.Dispose();
                hud?.Dispose();
                renderer?.Dispose();
                swapChain?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
