// Low-poly stylized sea for URP.
// - Swells are computed on the GPU from the globals SeaSurface.cs pushes every frame (_SeaTime, _SeaWave0..2),
//   so gameplay code can sample the exact same surface.
// - Faceted shading from screen-space derivatives (no duplicated vertices needed).
// - Vertex color RGB = water tint (deep → turquoise shallows), vertex alpha = shore foam.
// - Foam on wave crests, a stylized sun sparkle, main-light shadows and fog.
Shader "Offshore/Stylized Water"
{
    Properties
    {
        _FoamColor ("Foam Color", Color) = (0.93, 0.97, 1, 1)
        _CrestFoam ("Crest Foam Threshold", Range(0, 1)) = 0.9
        _CrestFoamStrength ("Crest Foam Strength", Range(0, 1)) = 0.22
        _Sparkle ("Sun Sparkle", Range(0, 2)) = 0.6
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FoamColor;
                half _CrestFoam;
                half _CrestFoamStrength;
                half _Sparkle;
                half _LightWrap;
            CBUFFER_END

            // Set globally by SeaSurface.cs: xy = direction, z = wavelength, w = amplitude.
            float _SeaTime;
            float4 _SeaWave0;
            float4 _SeaWave1;
            float4 _SeaWave2;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
                float crest : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            float Swell(float4 wave, float2 xz)
            {
                if (wave.w <= 0.0) return 0.0;
                float k = 6.2831853 / wave.z;
                float speed = sqrt(9.81 / k);
                return wave.w * sin(k * (dot(wave.xy, xz) - speed * _SeaTime));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float height = Swell(_SeaWave0, positionWS.xz) + Swell(_SeaWave1, positionWS.xz) + Swell(_SeaWave2, positionWS.xz);
                float total = max(_SeaWave0.w + _SeaWave1.w + _SeaWave2.w, 0.0001);
                positionWS.y += height;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.crest = saturate(height / total * 0.5 + 0.5);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Flat facet normal from screen-space derivatives, forced to face up.
                float3 normalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                normalWS *= sign(normalWS.y + 0.0001);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;

                half crestFoam = smoothstep(_CrestFoam, _CrestFoam + 0.1, input.crest) * _CrestFoamStrength;
                half foam = saturate(input.color.a + crestFoam);
                half3 albedo = lerp(input.color.rgb, _FoamColor.rgb, foam);

                half ndl = saturate((dot(normalWS, mainLight.direction) + _LightWrap) / (1.0 + _LightWrap));
                half3 ambient = SampleSH(normalWS);
                half3 color = albedo * (ambient + mainLight.color * ndl * lerp(0.45, 1.0, shadow));

                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                half sparkle = smoothstep(0.985, 0.995, dot(normalWS, halfDir)) * _Sparkle * shadow * (1.0 - foam);
                color += mainLight.color * sparkle;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
