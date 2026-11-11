using System;
using System.IO;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.D3DCompiler;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace AuroraScreensaver.Win
{
    /// <summary>
    /// Direct3D 11 renderer for the aurora scene.
    /// The heavy procedural shader runs at half resolution into an HDR float
    /// target (soft, filmic look and cheap on integrated GPUs), then a light
    /// composite pass applies gamma + dither and upscales to the backbuffer.
    /// </summary>
    internal sealed class AuroraRenderer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SceneConstants
        {
            public Vector2 Resolution;
            public float Time;
            public float Pad;
        }

        private readonly SwapChain swapChain;
        private readonly DeviceContext context;
        private RenderTargetView backbufferView;

        private Texture2D hdrTexture;
        private RenderTargetView hdrView;
        private ShaderResourceView hdrShaderResource;
        private int hdrWidth;
        private int hdrHeight;

        private VertexShader fullscreenVertexShader;
        private PixelShader scenePixelShader;
        private PixelShader compositePixelShader;
        private Buffer constantBuffer;
        private SamplerState linearSampler;

        private readonly System.Diagnostics.Stopwatch stopWatch = System.Diagnostics.Stopwatch.StartNew();
        private long lastFrameTicks;
        // Cap at ~60 fps: smoother visuals than an uncapped loop, less heat on laptops.
        private static readonly long FrameIntervalTicks = System.Diagnostics.Stopwatch.Frequency / 60;

        private bool disposed;

        public AuroraRenderer(SwapChain swapChain)
        {
            this.swapChain = swapChain;
            context = swapChain.Device.ImmediateContext;

            CompileShaders(swapChain.Device);

            using (var surface = swapChain.GetSurface(0))
            {
                Resize(surface.Description.Width, surface.Description.Height);
            }
        }

        private static string ShaderDirectory =>
            Path.Combine(AppContext.BaseDirectory, "Shaders");

        private static byte[] LoadBytecode(string csoName, ShaderVersion version, string entryPoint)
        {
            string csoPath = Path.Combine(ShaderDirectory, csoName);
            if (File.Exists(csoPath))
            {
                return File.ReadAllBytes(csoPath);
            }

            // Safety net: compile from source at runtime if precompiled blobs are absent.
            string source = File.ReadAllText(Path.Combine(ShaderDirectory, "aurora.hlsl"));
            using (var bytecode = ShaderBytecode.Compile(source, entryPoint, version.ToString()))
            {
                return bytecode.Data;
            }
        }

        private void CompileShaders(Device device)
        {
            var vs = LoadBytecode("aurora_fullscreen_vs.cso", ShaderVersion.VS_5_0, "VSFullscreen");
            var psScene = LoadBytecode("aurora_scene_ps.cso", ShaderVersion.PS_5_0, "PSMain");
            var psComposite = LoadBytecode("aurora_composite_ps.cso", ShaderVersion.PS_5_0, "PSComposite");

            fullscreenVertexShader = new VertexShader(device, vs);
            scenePixelShader = new PixelShader(device, psScene);
            compositePixelShader = new PixelShader(device, psComposite);

            constantBuffer = new Buffer(device, Utilities.SizeOf<SceneConstants>(),
                ResourceUsage.Default, BindFlags.ConstantBuffer, CPUAccessFlags.None, 0);

            linearSampler = new SamplerState(device, new SamplerStateDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            });
        }

        /// <summary>Recreates the backbuffer view and HDR target for a new client size.</summary>
        public void Resize(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            backbufferView?.Dispose();
            using (var backbuffer = swapChain.GetBackBuffer<Texture2D>(0))
            {
                backbufferView = new RenderTargetView(context.Device, backbuffer);
            }

            // Quarter the pixel count (half in each dimension) for the heavy pass.
            int w = Math.Max(1, width / 2);
            int h = Math.Max(1, height / 2);

            if (w != hdrWidth || h != hdrHeight)
            {
                hdrWidth = w;
                hdrHeight = h;

                hdrTexture?.Dispose();
                hdrView?.Dispose();
                hdrShaderResource?.Dispose();

                hdrTexture = new Texture2D(context.Device, new Texture2DDescription
                {
                    Width = w,
                    Height = h,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = SharpDX.DXGI.Format.R16G16B16A16_Float,
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                    SampleDescription = new SampleDescription(1, 0)
                });

                hdrView = new RenderTargetView(context.Device, hdrTexture);
                hdrShaderResource = new ShaderResourceView(context.Device, hdrTexture);
            }
        }

        public void RenderFrame()
        {
            if (disposed) return;

            // Soft frame pacing: skip if we are ahead of the 60 fps budget.
            long now = stopWatch.ElapsedTicks;
            if (now - lastFrameTicks < FrameIntervalTicks)
            {
                return;
            }
            lastFrameTicks = now;

            float time = (float)stopWatch.Elapsed.TotalSeconds;
            var constants = new SceneConstants
            {
                Resolution = new Vector2(hdrWidth, hdrHeight),
                Time = time,
                Pad = 0f
            };
            context.UpdateBuffer(constantBuffer, 0, ref constants);

            // Pass 1: procedural aurora into half-res HDR target.
            context.OutputMerger.SetRenderTargets(hdrView);
            context.Rasterizer.State = null;
            context.ClearRenderTargetView(hdrView, new Color4(0, 0, 0, 1));
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.VertexShader.Set(fullscreenVertexShader);
            context.VertexShader.SetConstantBuffer(0, constantBuffer);
            context.PixelShader.Set(scenePixelShader);
            context.PixelShader.SetConstantBuffer(0, constantBuffer);
            context.Draw(3, 0);

            // Pass 2: tonemap/gamma/dither + bilinear upscale to the backbuffer.
            context.OutputMerger.SetRenderTargets(backbufferView);
            context.ClearRenderTargetView(backbufferView, new Color4(0, 0, 0, 1));
            context.PixelShader.Set(compositePixelShader);
            context.PixelShader.SetShaderResource(0, hdrShaderResource);
            context.PixelShader.SetSampler(0, linearSampler);
            context.Draw(3, 0);

            swapChain.Present(0, PresentFlags.None);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            hdrTexture?.Dispose();
            hdrView?.Dispose();
            hdrShaderResource?.Dispose();
            fullscreenVertexShader?.Dispose();
            scenePixelShader?.Dispose();
            compositePixelShader?.Dispose();
            constantBuffer?.Dispose();
            linearSampler?.Dispose();
            backbufferView?.Dispose();
        }
    }
}
