#ifndef GM_PLASTER_WEAR_INPUT_INCLUDED
#define GM_PLASTER_WEAR_INPUT_INCLUDED

// Входные данные побелки с износом: всё, что у URP Lit (сама побелка — основные карты), плюс карты и числа того, что под ней.
#define KIT_MATERIAL_PROPERTIES \
    half4 _UnderColor; \
    half _UnderMetres; \
    half _UnderBumpScale; \
    half _UnderSmoothness; \
    half _WearSpacing; \
    half _WearSources; \
    half _WearRadius; \
    half _WearSoft; \
    half _WearLow; \
    half _WearShade; \
    half _WearStain; \
    half _Wear;

#include "KitLitInput.hlsl"
#include "KitNoise.hlsl"

// Карты того, что под побелкой, читаются сэмплером основной карты: свои сэмплеры не нужны, а их число у прохода ограничено.
TEXTURE2D(_UnderMap);
TEXTURE2D(_UnderBumpMap);
TEXTURE2D(_UnderMaoMap);

// Насколько глубоко точка внутри скола, в метрах (меньше 0 — снаружи). Источники осыпания лежат на плоскости стены: она поделена на
// клетки _WearSpacing, в клетке с шансом _WearSources есть источник — в своей точке, со своим наибольшим радиусом (от 0,4 до 1 от
// _WearRadius) и своим износом, с которого он начинает расти. Скол растёт от источника во все стороны; внизу стены сколы крупнее
// (брызги с земли); край рваный — расстояние растянуто шумом. У каждой стены свои источники: в номер клетки входит и плоскость стены.
float PlasterWearDepth(float3 positionWS, half3 normalWS)
{
    float2 facing = normalWS.xz;
    float facingLength = length(facing);
    facing = facingLength > 1e-3 ? facing / facingLength : float2(0.0, 1.0);
    float spacing = max(_WearSpacing, 0.1);
    float2 p = float2(dot(positionWS.xz, float2(-facing.y, facing.x)), positionWS.y) / spacing;
    float plane = floor(dot(positionWS.xz, facing) + 0.5);
    float2 cell = floor(p);
    float ragged = 0.75 + 0.5 * KitValueNoise(positionWS / 0.2);
    float low = 1.0 + 2.0 * _WearLow * saturate(1.0 - positionWS.y / 1.5);
    float depth = -1e3;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float3 c = float3(cell + float2(x, y), plane);
            float2 source = c.xy + float2(KitHash(c + float3(0.1, 0.2, 7.3)), KitHash(c + float3(0.5, 0.1, 11.7)));
            float start = KitHash(c + float3(0.0, 0.0, 19.1)) * 0.6;
            float grown = saturate((_Wear - start) / (1.0 - start));
            float radius = _WearRadius * lerp(0.4, 1.0, KitHash(c + float3(0.0, 0.0, 23.9))) * grown * low;
            float d = length(p - source) * spacing * ragged;
            bool hasSource = KitHash(c + float3(0.3, 0.7, 3.1)) < _WearSources;
            depth = max(depth, hasSource ? radius - d : -1e3);
        }
    }
    return depth;
}

// Побелка с износом (_Wear от 0 до 1): при 0 стена целая, с износом побелка темнеет пятнами грязи и осыпается сколами вокруг
// источников. Край скола местами резкий — там у края тень и грязь, — местами побелка истончается плавно, до _WearSoft метров: две
// текстуры смешиваются, и сквозь тонкую побелку проступает то, что под ней. Рваный край — и по рисунку самой побелки: тёмные поры и
// бугры откалываются раньше.
void ApplyPlasterWear(float2 uv, float3 positionWS, half3 normalWS, inout SurfaceData surface)
{
    float grain = 0.5 - dot(surface.albedo, half3(0.2126, 0.7152, 0.0722));
    float stain = KitValueNoise(positionWS / 1.7 + 51.3);
    surface.albedo *= 1.0 - _WearStain * _Wear * stain;

    float soft = smoothstep(0.35, 0.65, KitValueNoise(positionWS / 0.7 + 91.7));
    float width = lerp(0.01, max(_WearSoft, 0.01), soft);
    float depth = PlasterWearDepth(positionWS, normalWS) + grain * (0.04 + 0.6 * width);
    half hole = smoothstep(0.0, width, depth);
    half sharp = 1.0 - soft;
    half lip = (1.0 - smoothstep(width, width + 0.06, depth)) * hole * sharp;
    half rim = smoothstep(-0.05, 0.0, depth) * (1.0 - hole) * sharp;
    surface.albedo *= 1.0 - 0.15 * rim;

    float2 underUv = UNDO_TRANSFORM_TEX(uv, _BaseMap) / max(_UnderMetres, 0.05);
    half4 under = SAMPLE_TEXTURE2D(_UnderMap, sampler_BaseMap, underUv);
    half4 underMao = SAMPLE_TEXTURE2D(_UnderMaoMap, sampler_BaseMap, underUv);
    half3 underNormal = SampleNormal(underUv, TEXTURE2D_ARGS(_UnderBumpMap, sampler_BaseMap), _UnderBumpScale);
    half shade = 1.0 - _WearShade * lip;

    surface.albedo = lerp(surface.albedo, under.rgb * _UnderColor.rgb * shade, hole);
    surface.metallic = lerp(surface.metallic, 0.0, hole);
    surface.smoothness = lerp(surface.smoothness, underMao.a * _UnderSmoothness, hole);
    surface.occlusion = lerp(surface.occlusion, underMao.g * shade, hole);
    surface.normalTS = normalize(lerp(surface.normalTS, underNormal, hole));
}

// Поверхность перед освещением в проходе освещения кита.
#define KIT_APPLY_SURFACE(uv, positionWS, normalWS, surface) ApplyPlasterWear(uv, positionWS, normalWS, surface)

#endif
