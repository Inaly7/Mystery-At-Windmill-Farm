// Wind gust streak: a translucent white ribbon drawn on a LineRenderer.
//
// Why no texture: the Ghibli look comes from stroke weight variation, tapered
// ends and uneven pressure. All of that is cheaper and far more tweakable in
// math than in an authored texture, and it never stretches when curves differ
// in length (a texture-based stroke smears as soon as the ribbon gets longer).
//
// UV convention (LineRenderer textureMode = Stretch):
//   uv.x  0 = start of stroke, 1 = end of stroke, evenly spaced along arc length
//   uv.y  0 / 1 = side edges, 0.5 = centre line
// uv.x is only evenly spaced because WindGustRibbon resamples the curve by arc
// length. Without that, points bunch up in bends and the flow band visibly
// speeds up and slows down.
//
// Flow: a sin band travels along uv.x instead of scrolling a texture. The line
// itself stays put, the "air" moves through it -- which is how wind streaks read
// in the reference art.
//
// Render state: Transparent must NEVER write depth. Writing depth from a
// transparent pass pollutes the depth buffer and breaks sorting against
// post-processing and other transparents.
Shader "WindmillFarm/VFX/WindGust"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.98, 0.94, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.6

        [Header(Brush)]
        _CoreSoftness ("Core Softness", Range(0.5, 8)) = 2.2
        _WobbleAmp ("Brush Wobble", Range(0, 1)) = 0.35
        _WobbleFreq ("Wobble Frequency", Range(0.5, 20)) = 4.5
        _HeadFade ("Head Fade", Range(0.001, 0.5)) = 0.05
        _TailFade ("Tail Fade", Range(0.001, 0.5)) = 0.14

        [Header(Draw On)]
        _DrawProgress ("Draw Progress", Range(0, 1.2)) = 1.2
        _DrawSoftness ("Draw Softness", Range(0.005, 0.4)) = 0.05

        [Header(Flow)]
        _FlowTiling ("Flow Tiling", Range(0.5, 8)) = 2
        _FlowSpeed ("Flow Speed", Float) = 0.3
        _FlowContrast ("Flow Contrast", Range(0, 1)) = 0.35
        _Phase ("Phase Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Opacity;
                float  _CoreSoftness;
                float  _WobbleAmp;
                float  _WobbleFreq;
                float  _HeadFade;
                float  _TailFade;
                float  _DrawProgress;
                float  _DrawSoftness;
                float  _FlowTiling;
                float  _FlowSpeed;
                float  _FlowContrast;
                float  _Phase;
            CBUFFER_END

            #define TAU 6.28318530718

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Cross-section: solid at the centre, falling off to the edges.
                float across = abs(uv.y * 2.0 - 1.0);
                float core = pow(saturate(1.0 - across), _CoreSoftness);

                // Hand-drawn feel: low frequency pressure variation along the
                // stroke so it does not read as a uniform plastic tube.
                float wobble = sin(uv.x * _WobbleFreq * TAU + _Phase) * 0.5 + 0.5;
                core *= lerp(1.0, wobble, _WobbleAmp);

                // Lift the pen at both ends.
                float head = smoothstep(0.0, max(_HeadFade, 1e-4), uv.x);
                float tail = smoothstep(0.0, max(_TailFade, 1e-4), 1.0 - uv.x);

                // Draw-on: the stroke is painted up to _DrawProgress.
                float softness = max(_DrawSoftness, 1e-4);
                float drawn = 1.0 - smoothstep(_DrawProgress - softness, _DrawProgress, uv.x);

                // Travelling highlight band = the airflow moving through the line.
                float flow = sin((uv.x * _FlowTiling - _Time.y * _FlowSpeed + _Phase) * TAU) * 0.5 + 0.5;
                flow = lerp(1.0, flow, saturate(_FlowContrast));

                float alpha = core * head * tail * drawn * flow * _Opacity;
                return half4(_Color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
