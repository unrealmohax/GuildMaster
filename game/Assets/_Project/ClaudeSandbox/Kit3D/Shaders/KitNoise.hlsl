#ifndef GM_KIT_NOISE_INCLUDED
#define GM_KIT_NOISE_INCLUDED

// Шум значений в трёх измерениях: гладкий, от 0 до 1, без текстуры.
float KitHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float KitValueNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(KitHash(i), KitHash(i + float3(1, 0, 0)), f.x),
                     lerp(KitHash(i + float3(0, 1, 0)), KitHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(KitHash(i + float3(0, 0, 1)), KitHash(i + float3(1, 0, 1)), f.x),
                     lerp(KitHash(i + float3(0, 1, 1)), KitHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

#endif
