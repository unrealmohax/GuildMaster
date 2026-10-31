#ifndef GM_ROOF_MOSS_INPUT_INCLUDED
#define GM_ROOF_MOSS_INPUT_INCLUDED

// Входные данные крыши с мхом: всё, что у URP Lit, плюс карты и числа мха.
#define KIT_MATERIAL_PROPERTIES \
    half4 _MossColor; \
    half _MossMetres; \
    half _MossBumpScale; \
    half _MossSmoothness; \
    half _MossSpacing; \
    half _MossSeedChance; \
    half _MossRadius; \
    half _MossOpacity; \
    half _MossMinUp; \
    half _MossDamp; \
    half _Growth;

#include "KitLitInput.hlsl"
#include "KitNoise.hlsl"

TEXTURE2D(_MossMap);            SAMPLER(sampler_MossMap);
TEXTURE2D(_MossBumpMap);        SAMPLER(sampler_MossBumpMap);
TEXTURE2D(_MossMaoMap);         SAMPLER(sampler_MossMaoMap);

// Густота мха от зародышей. Мир по XZ поделён на клетки _MossSpacing; в клетке с шансом _MossSeedChance есть зародыш — в своей
// точке, со своим наибольшим радиусом (от половины до _MossRadius) и своим возрастом, с которого он начинает расти. Пятно растёт от
// зародыша во все стороны; край рваный — расстояние растянуто шумом. Густота 1 у зародыша и до 0 к краю пятна.
float RoofMossDensity(float3 positionWS)
{
    float spacing = max(_MossSpacing, 0.1);
    float2 p = positionWS.xz / spacing;
    float2 cell = floor(p);
    float ragged = 0.7 + 0.6 * KitValueNoise(positionWS / 0.35);
    float density = 0.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float3 c = float3(cell + float2(x, y), 0.0);
            float hasSeed = step(KitHash(c + float3(0.0, 0.0, 3.1)), _MossSeedChance);
            float2 seed = c.xy + float2(KitHash(c + float3(0.0, 0.0, 7.3)), KitHash(c + float3(0.0, 0.0, 11.7)));
            float start = KitHash(c + float3(0.0, 0.0, 19.1)) * 0.5;
            float grown = saturate((_Growth - start) / (1.0 - start));
            float radius = _MossRadius * lerp(0.5, 1.0, KitHash(c + float3(0.0, 0.0, 23.9))) * grown;
            float d = length(p - seed) * spacing * ragged;
            density = max(density, hasSeed * saturate((radius - d) / max(radius * 0.7, 1e-4)));
        }
    }
    return smoothstep(0.0, 1.0, density);
}

// Мох поверх черепицы. Растёт на гранях, смотрящих вверх (торцы плиток и нижние грани голые), редкими пятнами вокруг зародышей,
// которые с возрастом (_Growth от 0 до 1) расползаются: при 0 мха нет, при 1 пятна наибольшие, но крыша вся не зарастает. Внутри
// пятна мох лежит клочками по рисунку мха и мелкому шуму, сквозь них видна плитка; у зародыша клочков больше. Под пятном плитка
// темнее — сырая.
void ApplyRoofMoss(float2 uv, float3 positionWS, half3 normalWS, inout SurfaceData surface)
{
    float2 baseUv = UNDO_TRANSFORM_TEX(uv, _BaseMap);
    float2 mossUv = baseUv / max(_MossMetres, 0.05);
    half4 moss = SAMPLE_TEXTURE2D(_MossMap, sampler_MossMap, mossUv);
    half4 mossMao = SAMPLE_TEXTURE2D(_MossMaoMap, sampler_MossMaoMap, mossUv);
    half3 mossNormal = SampleNormal(mossUv, TEXTURE2D_ARGS(_MossBumpMap, sampler_MossBumpMap), _MossBumpScale);

    half up = smoothstep(_MossMinUp, _MossMinUp + 0.3, normalWS.y);
    float density = RoofMossDensity(positionWS) * up;
    // Клочки: мелкий шум и тёмные места мха. Чем гуще пятно, тем ниже порог и тем больше клочков; даже у зародыша часть плитки видна.
    float mottle = 0.55 * KitValueNoise(positionWS / 0.12) + 0.45 * (1.0 - dot(moss.rgb, half3(0.2126, 0.7152, 0.0722)));
    float threshold = lerp(1.0, 0.45, density);
    half cover = smoothstep(threshold, threshold + 0.2, mottle) * _MossOpacity;
    half damp = density * (1.0 - cover);

    surface.albedo *= 1.0 - _MossDamp * damp;
    surface.smoothness *= 1.0 - 0.5 * damp;
    surface.albedo = lerp(surface.albedo, moss.rgb * _MossColor.rgb, cover);
    surface.metallic = lerp(surface.metallic, 0.0, cover);
    surface.smoothness = lerp(surface.smoothness, mossMao.a * _MossSmoothness, cover);
    surface.occlusion = lerp(surface.occlusion, mossMao.g, cover);
    surface.normalTS = normalize(lerp(surface.normalTS, mossNormal, cover * 0.6));
}

// Поверхность перед освещением в проходе освещения кита.
#define KIT_APPLY_SURFACE(uv, positionWS, normalWS, surface) ApplyRoofMoss(uv, positionWS, normalWS, surface)

#endif
