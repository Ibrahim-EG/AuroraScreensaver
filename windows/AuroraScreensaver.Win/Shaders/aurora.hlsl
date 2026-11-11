// Aurora night scene – HLSL port of the Android GLSL shader (aurora.frag).
// Rendered at half resolution into an HDR (R16G16B16A16_FLOAT) target, then
// tonemapped + upscaled in the composite pass for a soft, filmic look.

cbuffer SceneConstants : register(b0)
{
    float2 uResolution;   // HDR target size (pixels)
    float  uTime;
    float  uPad;
};

static const float HORIZON = -0.18;
static const float2 MOON_POS = float2(0.52, 0.38);

float hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = hash(i);
    float b = hash(i + float2(1.0, 0.0));
    float c = hash(i + float2(0.0, 1.0));
    float d = hash(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;

    [unroll]
    for (int i = 0; i < 5; ++i)
    {
        v += a * noise(p);
        p = p * 2.03 + float2(17.13, 9.71);
        a *= 0.5;
    }

    return v;
}

float auroraLayer(float2 p, float t, float seed)
{
    if (p.y < 0.02 || p.y > 0.95)
    {
        return 0.0;
    }

    float x = p.x * 2.1 + seed * 3.7;

    float flow = fbm(float2(x * 0.13 + seed, t * 0.075)) * 1.8;
    x += t * 0.085 + flow;

    float wave = fbm(float2(x * 0.45, t * 0.11 + seed)) * 6.0;
    float curtain = sin(x * 3.4 + wave);

    float bandCenter = 0.34
        + 0.09 * sin(x * 0.52 + seed * 4.0 + t * 0.045)
        + 0.08 * fbm(float2(x * 0.22, t * 0.06 + seed));

    float d = (p.y - bandCenter - curtain * 0.075) * 6.5;
    float band = exp(-d * d);

    float rays = fbm(float2(p.x * 42.0 + t * 0.22, p.y * 7.0 - t * 0.09 + seed));
    rays = 0.45 + 0.55 * rays;

    float verticalFade = smoothstep(0.02, 0.16, p.y)
        * (1.0 - smoothstep(0.44, 0.92, p.y));

    float pulse = 0.68 + 0.32 * fbm(float2(p.x * 2.2 - t * 0.085, p.y * 3.1 + t * 0.06 + seed));

    return max(band * rays * verticalFade * pulse, 0.0);
}

float3 auroraColor(float2 p, float t)
{
    float a1 = auroraLayer(p, t, 0.0);
    float a2 = auroraLayer(p * 1.32 + float2(1.7, 0.0), t * 1.13, 2.3);
    float a3 = auroraLayer(p * 0.82 + float2(-2.2, 0.0), t * 0.86, 5.1);

    float3 aurora = float3(0.0, 0.0, 0.0);

    aurora += float3(0.08, 0.85, 0.42) * a1 * 1.35;
    aurora += float3(0.14, 0.56, 0.55) * a2 * 0.72;
    aurora += float3(0.42, 0.16, 0.72) * a3 * 0.42;
    aurora += float3(0.85, 0.22, 0.36) * pow(max(a2, 0.0), 3.0) * 0.22;

    aurora *= 0.82 + 0.18 * fbm(float2(p.x * 1.7 + t * 0.02, p.y * 2.6));

    return aurora;
}

float3 skyBase(float2 p, float t)
{
    float3 sky = lerp(
        float3(0.003, 0.006, 0.013),
        float3(0.001, 0.014, 0.024),
        smoothstep(HORIZON, 0.90, p.y)
    );

    float haze = fbm(float2(p.x * 1.7 + t * 0.012, p.y * 3.5 - t * 0.005));

    sky += float3(0.010, 0.028, 0.035)
        * smoothstep(0.45, 0.80, haze)
        * smoothstep(HORIZON, 0.25, p.y)
        * 0.22;

    float2 starCell = floor(p * 380.0);
    float starRandom = hash(starCell);
    float star = smoothstep(0.995, 1.0, starRandom);
    star *= smoothstep(HORIZON, 0.08, p.y);

    float twinkle = 0.70 + 0.30 * sin(t * 3.0 + starRandom * 123.0);

    float3 stars = float3(0.82, 0.92, 1.0) * star * twinkle * 0.38;

    float moonDist = length(p - MOON_POS);
    float moonDisk = smoothstep(0.075, 0.070, moonDist);
    float moonGlow = exp(-moonDist * 5.5) * 0.18;
    float moonSurface = 0.86 + 0.14 * fbm(p * 90.0);

    float3 moon = float3(0.94, 0.97, 1.0)
        * (moonDisk * moonSurface * 1.45 + moonGlow);

    return sky + stars + moon;
}

float ridgeHeight(float x)
{
    float large = fbm(float2(x * 1.7, 11.7));
    float detail = fbm(float2(x * 7.3, 4.2));

    return 0.020 + 0.038 * large + 0.014 * detail;
}

float3 sceneColor(float2 p, float t)
{
    float3 col = skyBase(p, t) + auroraColor(p, t);

    float ridgeY = HORIZON + ridgeHeight(p.x);

    if (p.y < ridgeY)
    {
        float edge = 1.0 - smoothstep(ridgeY - 0.004, ridgeY, p.y);

        float3 base = float3(0.001, 0.003, 0.005);

        float3 rim = auroraColor(float2(p.x, 0.28), t) * 0.24;
        rim += float3(0.62, 0.74, 0.86) * exp(-length(p - MOON_POS) * 5.0) * 0.05;

        float snow = fbm(float2(p.x * 60.0, p.y * 120.0)) * 0.02;

        col = lerp(col, base + rim + float3(snow, snow, snow), edge);
    }

    return col;
}

// Fullscreen triangle shared by both passes (no vertex buffer needed).
struct VSOut
{
    float4 pos : SV_Position;
    float2 uv : TEXCOORD0;
};

VSOut VSFullscreen(uint id : SV_VertexID)
{
    VSOut o;
    float2 uv = float2((id << 1) & 2, id & 2);
    o.pos = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    o.uv = uv;
    return o;
}

Texture2D hdrTex : register(t0);
SamplerState linearSampler : register(s0);

float4 PSMain(VSOut input) : SV_Target
{
    float2 raw = (2.0 * input.pos.xy - uResolution.xy) / uResolution.y;
    float t = uTime;

    float2 cam = float2(
        sin(t * 0.021) * 0.020,
        cos(t * 0.017) * 0.012
    );

    float2 p = raw + cam;

    float3 color = float3(0.0, 0.0, 0.0);

    if (p.y > HORIZON)
    {
        color = sceneColor(p, t);
    }
    else
    {
        float depth = saturate((HORIZON - p.y) / 0.7);
        float rippleScale = lerp(0.018, 0.050, depth);

        float n = noise(float2(p.x * 36.0 + t * 0.85, p.y * 28.0 - t * 1.25));
        float n2 = noise(float2(p.x * 92.0 - t * 0.55, p.y * 64.0 + t * 0.70));

        float2 reflected = float2(
            p.x + (n - 0.5) * rippleScale + (n2 - 0.5) * 0.008,
            HORIZON + (HORIZON - p.y) * (0.97 + 0.02 * n2)
        );

        float3 reflection = sceneColor(reflected, t);

        float waterFade = smoothstep(HORIZON - 0.60, HORIZON, p.y);

        float3 waterBase = lerp(
            float3(0.001, 0.004, 0.007),
            float3(0.003, 0.010, 0.015),
            waterFade
        );

        color = waterBase + reflection * lerp(0.30, 0.86, waterFade);

        float moonStreak = exp(-abs(p.x - MOON_POS.x) * 12.0);
        moonStreak *= smoothstep(HORIZON - 0.50, HORIZON, p.y);

        float nearHorizon = smoothstep(HORIZON - 0.045, HORIZON, p.y);

        color += float3(0.72, 0.78, 0.85) * moonStreak * nearHorizon * 0.085;
    }

    // Vignette
    float vignette = 1.0 - smoothstep(0.35, 1.55, length(raw));
    color *= 0.90 + 0.10 * vignette;

    // Filmic exposure (stays in linear space here; gamma is applied in composite)
    color = 1.0 - exp(-color * 1.85);

    return float4(color, 1.0);
}

float4 PSComposite(VSOut input) : SV_Target
{
    // Slight bilinear upscale from the half-res HDR buffer.
    float3 color = hdrTex.Sample(linearSampler, input.uv).rgb;

    // Gamma encode to sRGB-ish output.
    color = pow(saturate(color), float3(1.0 / 2.2, 1.0 / 2.2, 1.0 / 2.2));

    // Subtle ordered dither to kill banding on dark gradients.
    float2 fc = input.pos.xy;
    float dither = (frac(sin(dot(fc, float2(12.9898, 78.233))) * 43758.5453) - 0.5) / 255.0;
    color += dither;

    return float4(color, 1.0);
}
