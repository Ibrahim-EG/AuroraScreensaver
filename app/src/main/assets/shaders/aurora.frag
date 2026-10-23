#version 300 es

precision highp float;

uniform vec3 uResolution;
uniform float uTime;

out vec4 fragColor;

const float HORIZON = -0.18;
const vec2 MOON_POS = vec2(0.52, 0.38);

float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);

    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;

    for (int i = 0; i < 5; ++i) {
        v += a * noise(p);
        p = p * 2.03 + vec2(17.13, 9.71);
        a *= 0.5;
    }

    return v;
}

float auroraLayer(vec2 p, float t, float seed) {
    if (p.y < 0.02 || p.y > 0.95) {
        return 0.0;
    }

    float x = p.x * 2.1 + seed * 3.7;

    float flow = fbm(vec2(x * 0.13 + seed, t * 0.075)) * 1.8;
    x += t * 0.085 + flow;

    float wave = fbm(vec2(x * 0.45, t * 0.11 + seed)) * 6.0;
    float curtain = sin(x * 3.4 + wave);

    float bandCenter = 0.34
        + 0.09 * sin(x * 0.52 + seed * 4.0 + t * 0.045)
        + 0.08 * fbm(vec2(x * 0.22, t * 0.06 + seed));

    float d = (p.y - bandCenter - curtain * 0.075) * 6.5;
    float band = exp(-d * d);

    float rays = fbm(vec2(p.x * 42.0 + t * 0.22, p.y * 7.0 - t * 0.09 + seed));
    rays = 0.45 + 0.55 * rays;

    float verticalFade = smoothstep(0.02, 0.16, p.y)
        * (1.0 - smoothstep(0.44, 0.92, p.y));

    float pulse = 0.68 + 0.32 * fbm(vec2(p.x * 2.2 - t * 0.085, p.y * 3.1 + t * 0.06 + seed));

    return max(band * rays * verticalFade * pulse, 0.0);
}

vec3 auroraColor(vec2 p, float t) {
    float a1 = auroraLayer(p, t, 0.0);
    float a2 = auroraLayer(p * 1.32 + vec2(1.7, 0.0), t * 1.13, 2.3);
    float a3 = auroraLayer(p * 0.82 + vec2(-2.2, 0.0), t * 0.86, 5.1);

    vec3 aurora = vec3(0.0);

    aurora += vec3(0.08, 0.85, 0.42) * a1 * 1.35;
    aurora += vec3(0.14, 0.56, 0.55) * a2 * 0.72;
    aurora += vec3(0.42, 0.16, 0.72) * a3 * 0.42;
    aurora += vec3(0.85, 0.22, 0.36) * pow(max(a2, 0.0), 3.0) * 0.22;

    aurora *= 0.82 + 0.18 * fbm(vec2(p.x * 1.7 + t * 0.02, p.y * 2.6));

    return aurora;
}

vec3 skyBase(vec2 p, float t) {
    vec3 sky = mix(
        vec3(0.003, 0.006, 0.013),
        vec3(0.001, 0.014, 0.024),
        smoothstep(HORIZON, 0.90, p.y)
    );

    float haze = fbm(vec2(p.x * 1.7 + t * 0.012, p.y * 3.5 - t * 0.005));

    sky += vec3(0.010, 0.028, 0.035)
        * smoothstep(0.45, 0.80, haze)
        * smoothstep(HORIZON, 0.25, p.y)
        * 0.22;

    vec2 starCell = floor(p * 380.0);
    float starRandom = hash(starCell);
    float star = smoothstep(0.995, 1.0, starRandom);
    star *= smoothstep(HORIZON, 0.08, p.y);

    float twinkle = 0.70 + 0.30 * sin(t * 3.0 + starRandom * 123.0);

    vec3 stars = vec3(0.82, 0.92, 1.0) * star * twinkle * 0.38;

    float moonDist = length(p - MOON_POS);
    float moonDisk = smoothstep(0.075, 0.070, moonDist);
    float moonGlow = exp(-moonDist * 5.5) * 0.18;
    float moonSurface = 0.86 + 0.14 * fbm(p * 90.0);

    vec3 moon = vec3(0.94, 0.97, 1.0)
        * (moonDisk * moonSurface * 1.45 + moonGlow);

    return sky + stars + moon;
}

vec3 skyColor(vec2 p, float t) {
    return skyBase(p, t) + auroraColor(p, t);
}

float ridgeHeight(float x) {
    float large = fbm(vec2(x * 1.7, 11.7));
    float detail = fbm(vec2(x * 7.3, 4.2));

    return 0.020 + 0.038 * large + 0.014 * detail;
}

vec3 sceneColor(vec2 p, float t) {
    vec3 col = skyColor(p, t);

    float ridgeY = HORIZON + ridgeHeight(p.x);

    if (p.y < ridgeY) {
        float edge = 1.0 - smoothstep(ridgeY - 0.004, ridgeY, p.y);

        vec3 base = vec3(0.001, 0.003, 0.005);

        vec3 rim = auroraColor(vec2(p.x, 0.28), t) * 0.24;
        rim += vec3(0.62, 0.74, 0.86) * exp(-length(p - MOON_POS) * 5.0) * 0.05;

        float snow = fbm(vec2(p.x * 60.0, p.y * 120.0)) * 0.02;

        col = mix(col, base + rim + vec3(snow), edge);
    }

    return col;
}

void main() {
    vec2 raw = (2.0 * gl_FragCoord.xy - uResolution.xy) / uResolution.y;
    float t = uTime;

    vec2 cam = vec2(
        sin(t * 0.021) * 0.020,
        cos(t * 0.017) * 0.012
    );

    vec2 p = raw + cam;

    vec3 color = vec3(0.0);

    if (p.y > HORIZON) {
        color = sceneColor(p, t);
    } else {
        float depth = clamp((HORIZON - p.y) / 0.7, 0.0, 1.0);
        float rippleScale = mix(0.018, 0.050, depth);

        float n = noise(vec2(p.x * 36.0 + t * 0.85, p.y * 28.0 - t * 1.25));
        float n2 = noise(vec2(p.x * 92.0 - t * 0.55, p.y * 64.0 + t * 0.70));

        vec2 reflected = vec2(
            p.x + (n - 0.5) * rippleScale + (n2 - 0.5) * 0.008,
            HORIZON + (HORIZON - p.y) * (0.97 + 0.02 * n2)
        );

        vec3 reflection = sceneColor(reflected, t);

        float waterFade = smoothstep(HORIZON - 0.60, HORIZON, p.y);

        vec3 waterBase = mix(
            vec3(0.001, 0.004, 0.007),
            vec3(0.003, 0.010, 0.015),
            waterFade
        );

        color = waterBase + reflection * mix(0.30, 0.86, waterFade);

        float moonStreak = exp(-abs(p.x - MOON_POS.x) * 12.0);
        moonStreak *= smoothstep(HORIZON - 0.50, HORIZON, p.y);

        float nearHorizon = smoothstep(HORIZON - 0.045, HORIZON, p.y);

        color += vec3(0.72, 0.78, 0.85) * moonStreak * nearHorizon * 0.085;
    }

    float vignette = 1.0 - smoothstep(0.35, 1.55, length(raw));
    color *= 0.90 + 0.10 * vignette;

    color = 1.0 - exp(-color * 1.85);
    color = pow(color, vec3(0.4545));

    float dither = hash(gl_FragCoord.xy + vec2(fract(t) * 61.7)) / 255.0;
    color += dither;

    fragColor = vec4(color, 1.0);
}
