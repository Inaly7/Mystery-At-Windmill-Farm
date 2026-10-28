// 动态云的卡片材质（球面弯曲 + 地平线淡出版）。
//
// 【为什么弧度必须在顶点阶段做】
//   法线贴图只影响受光计算，像素着色器也改不了几何轮廓——卡片一大，轮廓就是
//   一条直线，怎么调贴图都露馅。所以：网格细分成 20×20 → 顶点着色器把每个顶点
//   投到球面上 → 轮廓真的弯了，云与云之间也沿弧线衔接，天空才像一个球。
//
// 【弯曲算法：把顶点径向投影到"以玩家为球心、过卡片中心"的球面】
//   1) 卡片中心 = 物体原点（网格以 0 为中心）。由它到玩家的距离得出这张卡片
//      所在球的半径 R —— 半径是自动算出来的，不需要每层单独传参数；
//   2) 每个顶点沿"玩家 → 顶点"方向推到半径 R 上；
//   3) _Bend 在原始位置和投影位置之间插值：0 = 平的，1 = 严格贴球面（物理正确），
//      >1 = 夸张（比真球面弯得更狠，想要更强球体感时用）。
//
// 【地平线淡出】
//   按卡片中心相对玩家的仰角做衰减：仰角低于 _HorizonFadeStart 开始变透明，
//   到 _HorizonFadeEnd 降到 _HorizonMinOpacity。两个阈值和最低透明度都放在
//   UnityPerMaterial 里，可由每朵云的组件用 MaterialPropertyBlock 单独下发，
//   所以每朵云可以有自己的淡出区间。
//
// 关雾说明：本项目的雾是 FlatKit 的 Renderer Feature（全屏 blit，在透明物体之后），
// 这里关不关雾对结果没有影响——雾是盖在整张画面上的。
Shader "WindmillFarm/Sky/CloudCard"
{
    Properties
    {
        [MainTexture] _BaseMap ("Cloud Texture (RGBA)", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _AlphaScale ("Alpha Scale", Range(0, 1)) = 1
        [Header(Bend)]
        _Bend ("Bend Amount", Range(0, 2)) = 1
        [Header(Horizon Fade)]
        _HorizonFadeStart ("Fade Start Angle (deg)", Range(-5, 60)) = 8
        _HorizonFadeEnd ("Fade End Angle (deg)", Range(0, 90)) = 26
        _HorizonMinOpacity ("Horizon Min Opacity", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _AlphaScale;
                half _Bend;
                half _HorizonFadeStart;
                half _HorizonFadeEnd;
                half _HorizonMinOpacity;
            CBUFFER_END

            // 玩家（相机）位置，由天空云场组件每帧用 Shader.SetGlobalVector 下发。
            // 全局量必须放在 UnityPerMaterial 之外。
            float4 _SkyCenter;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half horizonFade : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // 卡片中心（物体原点）到玩家的距离 = 这张卡片要在的球半径
                float3 centreWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 toCentre = centreWS - _SkyCenter.xyz;
                float radius = max(length(toCentre), 1.0);

                // 顶点径向投到该球面上
                float3 toVert = posWS - _SkyCenter.xyz;
                float len = max(length(toVert), 0.0001);
                float3 projected = _SkyCenter.xyz + (toVert / len) * radius;

                posWS = lerp(posWS, projected, _Bend);

                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                // 地平线淡出系数：卡片中心相对玩家的仰角（度）
                float sinElev = clamp(toCentre.y / radius, -1.0, 1.0);
                float elevDeg = asin(sinElev) * 57.2957795;
                output.horizonFade = (_HorizonFadeEnd > _HorizonFadeStart)
                    ? smoothstep(_HorizonFadeStart, _HorizonFadeEnd, elevDeg)
                    : step(_HorizonFadeStart, elevDeg);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half horizon = lerp(_HorizonMinOpacity, 1.0, saturate(input.horizonFade));
                half alpha = tex.a * _AlphaScale * _Tint.a * horizon;
                half3 rgb = tex.rgb * _Tint.rgb;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
