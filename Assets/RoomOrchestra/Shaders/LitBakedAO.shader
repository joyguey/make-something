// Lit with baked ambient occlusion (V3 review: "Consider using Blender to bake ambient occlusion on your objects
// individually for greater visuals"). Blender bakes each object's ambient occlusion (and, for the meadow, its colours)
// into the mesh's vertex colours (ArtSource/*/bake_ao.py); this shader multiplies them into the base colour and also
// darkens the ambient light by the occlusion, as URP's Lit does with an occlusion map. Otherwise URP's own PBR
// lighting, shadows, fog and single-pass stereo. Only meshes exported with vertex colours use it.
Shader "RoomOrchestra/LitBakedAO"
{
    Properties
    {
        _BaseColor ("Base colour", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _AOStrength ("Baked AO strength on direct light", Range(0, 1)) = 0.65
        _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _RimAmount ("Rim light (characters and furniture)", Range(0, 1)) = 0
        _SwayAmount ("Wind sway at the top (m)", Range(0, 0.2)) = 0
        _SwayHeight ("Height of the top (m)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _AOStrength;
                half4 _EmissionColor;
                half _RimAmount;
                half _SwayAmount;
                half _SwayHeight;
            CBUFFER_END

            // A gentle wind (owner, Fable refinement 2026-11-11: "add the gentle swaying"): a shape bends from its base,
            // more toward its top, in slow waves that travel across the meadow. The same in every pass, so its shadow and
            // depth move with it. Nothing moves where _SwayAmount is 0 (everything but grass, flowers, bushes and trees).
            float3 Sway(float3 positionOS, float3 positionWS)
            {
                if (_SwayAmount <= 0.0) return positionWS;
                float h = saturate(positionOS.y / max(_SwayHeight, 0.01));
                float phase = dot(positionWS.xz, float2(0.35, 0.22));
                float wave = sin(_Time.y * 1.3 + phase) * 0.7 + sin(_Time.y * 2.1 + phase * 1.7) * 0.3;
                positionWS.xz += float2(0.8, 0.6) * (wave * h * h * _SwayAmount);
                return positionWS;
            }

            // The game's look, set once for every material (LookSettings, owner-approved Fable refinement): 0 is URP's
            // smooth lighting, 1 is painted light (a lit side and a shade side with a soft edge between); a rim of
            // light round each shape's edge; both cheap, per pixel, no post-processing.
            half _RO_Toon;
            half _RO_Rim;
            half4 _RO_RimColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = Sway(input.positionOS.xyz, TransformObjectToWorld(input.positionOS.xyz));
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normal.normalWS;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half ao = saturate(input.color.a);           // the baked occlusion lives in alpha
                half3 tint = SRGBToLinear(input.color.rgb);   // the baked colour, painted as screen colours (white where the material sets it)

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BaseColor.rgb * tint * lerp(1.0h, ao, _AOStrength);
                surface.metallic = 0;
                surface.specular = 0;
                surface.smoothness = _Smoothness;
                surface.occlusion = ao;
                surface.emission = _EmissionColor.rgb;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                Light sun = GetMainLight(inputData.shadowCoord);
                half facing = dot(inputData.normalWS, sun.direction);
                half lit = smoothstep(0.0h, 0.08h, facing) * lerp(1.0h, sun.shadowAttenuation, 0.8h);
                if (_RO_Toon > 0.001h)
                {
                    // painted light: the shade side keeps a third of the sun, the lit side its full colour, and a small
                    // crisp highlight on the shiny shapes
                    half3 painted = surface.albedo * (inputData.bakedGI * ao + sun.color * lerp(0.32h, 0.95h, lit));
                    half3 halfway = SafeNormalize(sun.direction + inputData.viewDirectionWS);
                    half shine = smoothstep(0.965h, 0.98h, dot(inputData.normalWS, halfway)) * _Smoothness * lit;
                    painted += sun.color * shine * 0.35h + surface.emission;
                    color.rgb = lerp(color.rgb, painted, _RO_Toon);
                }
                if (_RO_Rim * _RimAmount > 0.001h) // characters and furniture only: never the ground
                {
                    half edge = pow(1.0h - saturate(dot(inputData.normalWS, inputData.viewDirectionWS)), 3.0h);
                    color.rgb += _RO_RimColor.rgb * edge * _RO_Rim * _RimAmount * (0.35h + 0.65h * lit) * ao;
                }
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // Its own shadow and depth passes with the same material buffer, so the shader stays SRP-batcher compatible
        // (borrowing URP Lit's passes brought a different buffer and turned batching off for the whole shader).
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _AOStrength;
                half4 _EmissionColor;
                half _RimAmount;
                half _SwayAmount;
                half _SwayHeight;
            CBUFFER_END

            // A gentle wind (owner, Fable refinement 2026-11-11: "add the gentle swaying"): a shape bends from its base,
            // more toward its top, in slow waves that travel across the meadow. The same in every pass, so its shadow and
            // depth move with it. Nothing moves where _SwayAmount is 0 (everything but grass, flowers, bushes and trees).
            float3 Sway(float3 positionOS, float3 positionWS)
            {
                if (_SwayAmount <= 0.0) return positionWS;
                float h = saturate(positionOS.y / max(_SwayHeight, 0.01));
                float phase = dot(positionWS.xz, float2(0.35, 0.22));
                float wave = sin(_Time.y * 1.3 + phase) * 0.7 + sin(_Time.y * 2.1 + phase * 1.7) * 0.3;
                positionWS.xz += float2(0.8, 0.6) * (wave * h * h * _SwayAmount);
                return positionWS;
            }

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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = Sway(input.positionOS.xyz, TransformObjectToWorld(input.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return 0; }
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _AOStrength;
                half4 _EmissionColor;
                half _RimAmount;
                half _SwayAmount;
                half _SwayHeight;
            CBUFFER_END

            // A gentle wind (owner, Fable refinement 2026-11-11: "add the gentle swaying"): a shape bends from its base,
            // more toward its top, in slow waves that travel across the meadow. The same in every pass, so its shadow and
            // depth move with it. Nothing moves where _SwayAmount is 0 (everything but grass, flowers, bushes and trees).
            float3 Sway(float3 positionOS, float3 positionWS)
            {
                if (_SwayAmount <= 0.0) return positionWS;
                float h = saturate(positionOS.y / max(_SwayHeight, 0.01));
                float phase = dot(positionWS.xz, float2(0.35, 0.22));
                float wave = sin(_Time.y * 1.3 + phase) * 0.7 + sin(_Time.y * 2.1 + phase * 1.7) * 0.3;
                positionWS.xz += float2(0.8, 0.6) * (wave * h * h * _SwayAmount);
                return positionWS;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformWorldToHClip(Sway(input.positionOS.xyz, TransformObjectToWorld(input.positionOS.xyz)));
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
