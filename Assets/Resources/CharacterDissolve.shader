// ตัวละครสลายแบบมอนสเตอร์ใน Genshin: ตัวมืดลงเป็นเงา ขอบเรืองแสง แล้วค่อย ๆ หายเป็นรูตาม Noise 3 มิติ
// ใช้ Noise ในพื้นที่โลก (ไม่พึ่ง UV เพราะโมเดล Guardian เป็นสีเรียบ ไม่มี texture) ไล่จากบนลงล่างได้ด้วย _Direction
// CharacterFX สลับวัสดุของโมเดลมาเป็นตัวนี้ตอนตาย คัดลอกสี / BaseMap / Emissive จากวัสดุ Toon เดิม แล้วเลื่อน _Dissolve 0 -> 1
// แสงแบบ Toon ง่าย ๆ (แสงหลัก 2 ระดับ + Ambient) พอให้ตัวยังดูเหมือนเดิมก่อนมืดลง
Shader "Ascendia/CharacterDissolve"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMap ("Base Map", 2D) = "white" {}
        _ShadeColor ("Shade Color", Color) = (0.7,0.7,0.75,1)
        [HDR] _EmissiveColor ("Emissive Color", Color) = (0,0,0,1)
        _EmissiveMap ("Emissive Map", 2D) = "white" {}

        _Dissolve ("Dissolve", Range(0, 1.1)) = 0
        _Darken ("Darken", Range(0, 1)) = 0
        _DarkColor ("Dark Color", Color) = (0.06, 0.02, 0.1, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (4, 1.4, 2.2, 1)
        _EdgeWidth ("Edge Width", Range(0.001, 0.4)) = 0.08
        [HDR] _RimColor ("Rim Color", Color) = (2, 0.6, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _NoiseScale ("Noise Scale (per world unit)", Float) = 1
        _Direction ("Top-down Bias", Range(0, 1)) = 0.3
        _BoundsY ("World Y min / max", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float4 _ShadeColor;
            float4 _EmissiveColor;
            float4 _EmissiveMap_ST;
            float _Dissolve;
            float _Darken;
            float4 _DarkColor;
            float4 _EdgeColor;
            float _EdgeWidth;
            float4 _RimColor;
            float _RimPower;
            float _NoiseScale;
            float _Direction;
            float4 _BoundsY;
        CBUFFER_END

        // Noise 3 มิติ (value noise + fbm) CharacterFX.DissolveValue คำนวณแบบเดียวกันบน CPU เพื่อปล่อยประกายตรงจุดที่กำลังหาย
        float Hash31(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float ValueNoise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            float a = lerp(Hash31(i + float3(0,0,0)), Hash31(i + float3(1,0,0)), f.x);
            float b = lerp(Hash31(i + float3(0,1,0)), Hash31(i + float3(1,1,0)), f.x);
            float c = lerp(Hash31(i + float3(0,0,1)), Hash31(i + float3(1,0,1)), f.x);
            float d = lerp(Hash31(i + float3(0,1,1)), Hash31(i + float3(1,1,1)), f.x);
            return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
        }

        // 0..1 ค่าน้อยหายก่อน
        float DissolveValue(float3 positionWS)
        {
            float3 p = positionWS * _NoiseScale;
            float n = ValueNoise(p) * 0.55 + ValueNoise(p * 2.03 + 17.1) * 0.3 + ValueNoise(p * 4.11 + 31.7) * 0.15;
            n = saturate((n - 0.2) / 0.6);
            float h = saturate((positionWS.y - _BoundsY.x) / max(0.001, _BoundsY.y - _BoundsY.x));
            return lerp(n, 1.0 - h, _Direction);
        }

        void ClipDissolve(float3 positionWS)
        {
            clip(DissolveValue(positionWS) - _Dissolve);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissiveMap); SAMPLER(sampler_EmissiveMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float value = DissolveValue(input.positionWS);
                clip(value - _Dissolve);

                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(input.uv, _BaseMap)) * _BaseColor;
                half3 shade = albedo.rgb * (_ShadeColor.rgb / max(_BaseColor.rgb, 0.001));

                // Toon สองระดับจากแสงหลัก
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float lit = smoothstep(0.0, 0.08, dot(normalWS, light.direction)) * light.shadowAttenuation;
                half3 color = lerp(shade, albedo.rgb, lit) * light.color;
                color += albedo.rgb * SampleSH(normalWS) * 0.35;

                // มืดลงเป็นเงา + ขอบตัวเรือง (Fresnel)
                color = lerp(color, _DarkColor.rgb, _Darken);
                float rim = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower);
                color += _RimColor.rgb * rim * _Darken;

                // แสงเดิมของวัสดุ (ตา / อาวุธเรือง)
                color += SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, TRANSFORM_TEX(input.uv, _EmissiveMap)).rgb * _EmissiveColor.rgb * (1.0 - _Darken * 0.5);

                // ขอบรอยสลาย
                float edge = _Dissolve > 0.001 ? 1.0 - saturate((value - _Dissolve) / _EdgeWidth) : 0.0;
                color += _EdgeColor.rgb * edge * edge * 1.5;

                color = MixFog(color, input.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.positionWS = positionWS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                ClipDissolve(input.positionWS);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                ClipDissolve(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFrag(Varyings input) : SV_Target
            {
                ClipDissolve(input.positionWS);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
