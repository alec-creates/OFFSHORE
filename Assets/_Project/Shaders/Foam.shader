// Soft-edged transparent foam for wake trails (TrailRenderer supplies the color/alpha gradient).
Shader "Offshore/Foam"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.3
        _Breakup ("Breakup", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Foam"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _EdgeSoftness;
                half _Breakup;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half edge = smoothstep(0.0, _EdgeSoftness, input.uv.y) * smoothstep(1.0, 1.0 - _EdgeSoftness, input.uv.y);
                float2 p = input.positionWS.xz * 0.9;
                half pattern = 0.5 + 0.25 * sin(p.x * 2.1 + sin(p.y * 1.7)) + 0.25 * sin(p.y * 2.6 + sin(p.x * 1.3));
                half breakup = lerp(1.0, smoothstep(0.25, 0.65, pattern), _Breakup);
                half alpha = input.color.a * _Color.a * edge * breakup;
                return half4(input.color.rgb * _Color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
