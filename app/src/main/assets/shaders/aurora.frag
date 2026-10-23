#version 300 es

precision highp float;

uniform vec3 uResolution;
uniform float uTime;

out vec4 fragColor;

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

    float x = p.x * 2.2 + seed * 3.7;
    float drift = t * 0.035 + fbm(vec2(x * 0.15 + seed, t * 0.02)) * 1.5;
    x += drift;

    float curtain = sin(x * 4.0 + fbm(vec2(x * 0.6, t * 0.05 + seed)) * 5.0);

    float bandCenter = 0.36
        + 0.08 * sin(x * 0.7 + seed * 5.0)
        + 0.07 * fbm(vec2(x * 0.25, t * 0.03));

    float band = exp(-pow((p.y - bandCenter - curtain * 0.06) * 7.0, 2.0));

    float rays = 0.5 + 0.5 * fbm(vec2(p.x * 30.0 + t * 0.08, p.y * 4.0 - t * 0.02));

    float verticalFade = smoothstep(0.02, 0.18, p.y)
        * (1.0 - smoothstep(0.45, 0.90, p.y));

    float intensity = band * rays * verticalFade;

    intensity *= 0.75 + 0.25 * fbm(vec2(p.x * 3.0 + t * 0.02, p.y * 2.0 + seed));

    return max(intensity, 0.0);
}

vec3 skyColor(vec2 p, float t) {
    float horizon = -0.18;

    vec3 sky = mix(
        vec3(0.004, 0.007, 0.014),
        vec3(0.000, 0.012, 0.022),
        smoothstep(horizon, 0.85, p.y)
    );

    vec2 starCell = floor(p * 350.0);
    float starRandom = hash(starCell);
    float star = smoothstep(0.996, 1.0, starRandom);
    star *= smoothstep(horizon, 0.10, p.y);

    float twinkle = 0.75 + 0.25 * sin(t * 2.0 + starRandom * 100.0);
    vec3 starColor = vec3(0.85, 0.92, 1.0) * star * twinkle * 0.35;

    vec2 moonPos = vec2(0.52, 0.38);
    float moonDist = length(p - moonPos);

    float moonDisk = smoothstep(0.075, 0.070, moonDist);
    float moonGlow = exp(-moonDist * 6.0) * 0.16;
    float moonSurface = 0.88 + 0.12 * fbm(p * 80.0);

    vec3 moonColor = vec3(0.93, 0.96, 1.0)
        * (moonDisk * moonSurface * 1.35 + moonGlow);

    float a1 = auroraLayer(p, t, 0.0);
    float a2 = auroraLayer(p * 1.35 + vec2(1.7, 0.0), t * 1.17, 2.3);
    float a3 = auroraLayer(p * 0.80 + vec2(-2.2, 0.0), t * 0.83, 5.1);

    vec3 aurora = vec3(0.0);

    aurora += vec3(0.10, 0.85, 0.42) * a1 * 1.15;
    aurora += vec3(0.16, 0.55, 0.52) * a2 * 0.60;
    aurora += vec3(0.46, 0.18, 0.72) * a3 * 0.34;
    aurora += vec3(0.85, 0.22, 0.35) * pow(max(a2, 0.0), 3.0) * 0.18;

    aurora *= 0.85 + 0.15 * fbm(vec2(p.x * 1.5 + t * 0.015, p.y * 2.5));

    return sky + starColor + moonColor + aurora;
}

void main() {
    vec2 p = (2.0 * gl_FragCoord.xy - uResolution.xy) / uResolution.y;
    float t = uTime;
    float horizon = -0.18;

    vec3 color = vec3(0.0);

    if (p.y > horizon) {
        color = skyColor(p, t);
    } else {
        float ripple = (noise(vec2(p.x * 32.0 + t * 0.8, p.y * 22.0 - t * 1.1)) - 0.5) * 0.035;

        vec2 reflected = vec2(
            p.x + ripple,
            horizon + (horizon - p.y) * 0.97
        );

        vec3 reflection = skyColor(reflected, t);

        float waterFade = smoothstep(horizon - 0.55, horizon, p.y);

        vec3 waterBase = vec3(0.002, 0.007, 0.011);
        color = waterBase + reflection * mix(0.35, 0.78, waterFade);

        vec2 moonPos = vec2(0.52, 0.38);

        float moonStreak = exp(-abs(p.x - moonPos.x) * 14.0);
        moonStreak *= smoothstep(horizon - 0.42, horizon, p.y);

        float nearHorizon = smoothstep(horizon - 0.035, horizon, p.y);

        color += vec3(0.72, 0.78, 0.85) * moonStreak * nearHorizon * 0.075;
    }

    color = 1.0 - exp(-color * 1.75);
    color = pow(color, vec3(0.4545));

    float dither = hash(gl_FragCoord.xy + vec2(fract(t) * 61.7)) / 255.0;
    color += dither;

    fragColor = vec4(color, 1.0);
}
