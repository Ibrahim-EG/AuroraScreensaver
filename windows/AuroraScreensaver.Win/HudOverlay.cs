using System;
using System.Windows.Forms;
using SharpDX;
using D2D = SharpDX.Direct2D1;
using DWrite = SharpDX.DirectWrite;

namespace AuroraScreensaver.Win
{
    /// <summary>
    /// Clock + battery HUD drawn with Direct2D/DirectWrite directly onto the
    /// DXGI backbuffer (the same surface the D3D scene presents to). Mirrors
    /// the Android AuroraHudView: thin clock, small battery line, soft glow.
    /// </summary>
    internal sealed class HudOverlay : IDisposable
    {
        private readonly D2D.Factory d2dFactory;
        private readonly DWrite.Factory dwriteFactory;
        private readonly RenderTarget renderTarget;
        private readonly D2D.Device d2dDevice;

        private DWrite.TextFormat clockFormat;
        private DWrite.TextFormat statusFormat;
        private D2D.SolidColorBrush clockBrush;
        private D2D.SolidColorBrush statusBrush;

        private int width;
        private int height;

        private string clockText = "--:--";
        private string statusText = "";
        private DateTime lastClockCheck = DateTime.MinValue;

        private bool disposed;

        public HudOverlay(SharpDX.Direct3D11.Device d3dDevice, IntPtr windowHandle, int width, int height)
        {
            this.width = Math.Max(1, width);
            this.height = Math.Max(1, height);

            d2dFactory = new D2D.Factory(FactoryType.SingleThreaded);
            dwriteFactory = new DWrite.Factory();

            // Share the D3D device so D2D can draw onto the swapchain backbuffer.
            using (var dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device>())
            {
                d2dDevice = new D2D.Device(dxgiDevice);
            }

            renderTarget = new D2D.WindowRenderTarget(d2dFactory,
                new D2D.RenderTargetProperties(
                    new D2D.PixelFormat(SharpDX.DXGI.Format.Unknown, D2D.AlphaMode.Unknown)),
                new D2D.WindowPresentParameters
                {
                    Handle = windowHandle,
                    PresentOptions = D2D.PresentOptions.Immediately,
                    ColorContext = new D2D.ColorContext { ColorSpace = D2D.ColorSpace.SrgbGamma }
                });

            CreateTextResources();
            UpdateStatus();
        }

        private void CreateTextResources()
        {
            float minSide = Math.Min(width, height);

            clockFormat?.Dispose();
            statusFormat?.Dispose();
            clockBrush?.Dispose();
            statusBrush?.Dispose();

            // Segoe UI Light matches the Windows 10 look-and-feel.
            clockFormat = new DWrite.TextFormat(dwriteFactory, "Segoe UI Light",
                DWrite.FontWeight.Light, DWrite.FontStyle.Normal, minSide * 0.055f);
            clockFormat.TextAlignment = DWrite.TextAlignment.Leading;

            statusFormat = new DWrite.TextFormat(dwriteFactory, "Segoe UI Semilight",
                DWrite.FontWeight.SemiLight, DWrite.FontStyle.Normal, minSide * 0.021f);
            statusFormat.TextAlignment = DWrite.TextAlignment.Leading;

            // Brushes are tied to the render target; recreate them after resize too.
            clockBrush = new D2D.SolidColorBrush(renderTarget,
                new RawColor4(228f / 255f, 240f / 255f, 255f / 255f, 0.92f));
            statusBrush = new D2D.SolidColorBrush(renderTarget,
                new RawColor4(168f / 255f, 220f / 255f, 1f, 0.78f));
        }

        public void Resize(int newWidth, int newHeight)
        {
            width = Math.Max(1, newWidth);
            height = Math.Max(1, newHeight);

            try
            {
                renderTarget.Resize(new RawSize2D(width, height));
            }
            catch (SharpDXException)
            {
                // Zero-size window during transition; next Draw will retry implicitly.
            }

            CreateTextResources();
        }

        public void Draw()
        {
            if (disposed) return;

            TickClock();

            renderTarget.BeginDraw();
            try
            {
                float minSide = Math.Min(width, height);
                float padX = minSide * 0.045f;
                float padY = minSide * 0.040f;

                var clockRect = new RawRectangleF(padX, padY, width - padX, padY + minSide * 0.09f);
                var statusRect = new RawRectangleF(padX, clockRect.Bottom + minSide * 0.006f,
                    width - padX, clockRect.Bottom + minSide * 0.04f);

                renderTarget.DrawText(clockText, clockFormat, clockRect, clockBrush,
                    D2D.DrawTextOptions.NoSnapGridlines | D2D.DrawTextOptions.EnableColorFont,
                    D2D.ParityRotation.Default);

                if (!string.IsNullOrEmpty(statusText))
                {
                    renderTarget.DrawText(statusText, statusFormat, statusRect, statusBrush,
                        D2D.DrawTextOptions.EnableColorFont,
                        D2D.ParityRotation.Default);
                }
            }
            finally
            {
                renderTarget.EndDraw();
            }
        }

        private void TickClock()
        {
            var now = DateTime.Now;
            if (now.Second == lastClockCheck.Second &&
                now.Hour == lastClockCheck.Hour &&
                now.Minute == lastClockCheck.Minute)
            {
                return;
            }

            lastClockCheck = now;
            clockText = now.ToString("HH:mm");
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var power = SystemInformation.PowerStatus;
            int percent = (int)Math.Round(power.BatteryLifePercent * 100f);

            bool onAc = power.PowerLineStatus == PowerLineStatus.Online;
            bool noBattery = percent < 0 || percent > 100;

            if (noBattery)
            {
                statusText = DateTime.Now.ToString("dddd, MMMM d");
            }
            else if (onAc)
            {
                statusText = percent >= 100 ? "Charged" : $"Charging \u2022 {percent}%";
            }
            else
            {
                statusText = $"{percent}%";
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            clockFormat?.Dispose();
            statusFormat?.Dispose();
            clockBrush?.Dispose();
            statusBrush?.Dispose();
            renderTarget?.Dispose();
            d2dDevice?.Dispose();
            dwriteFactory?.Dispose();
            d2dFactory?.Dispose();
        }
    }
}
