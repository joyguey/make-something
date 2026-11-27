// The meadow inside story mode's portal (owner, 2026-11-25: "the aesthetic that I loved from the preview image"): the
// game's own meadow pieces (vertex colours with baked AO, like LitBakedAO) in a warm, painted light with a soft haze
// that thickens with distance into the portal, swaying in the wind. Drawn only where the portal's opening marked the
// stencil (StoryPortalMask), so it is a window into another world. Opaque: it writes alpha 1, so PICO's view of the
// real room never shows through it. Its light is its own (story globals), so the sandbox's lighting is never touched.
Shader "RoomOrchestra/StoryPortalWorld"
{
    Properties
    {
        _BaseColor ("Base colour", Color) = (1, 1, 1, 1)
        _AOStrength ("Baked AO strength", Range(0, 1)) = 0.6
        _SwayAmount ("Wind sway at the top (m)", Range(0, 0.2)) = 0
        _SwayHeight ("Height of the top (m)", Float) = 1
        _Unlit ("Unlit (the sky)", Float) = 0
        _Glow ("Glow", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "PortalWorld"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Stencil
            {
                Ref 71
                Comp Equal
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _AOStrength;
                half _SwayAmount;
                half _SwayHeight;
                half _Unlit;
                half _Glow;
            CBUFFER_END

            float4 _StoryPortalCentre;   // xyz: the opening's centre
            float4 _StoryPortalInto;     // xyz: into the world (away from the player); w: haze distance (m)
            half4 _StoryHaze;            // rgb: haze colour; a: most haze
            half4 _StorySunDir;          // xyz: toward the sun
            half4 _StorySunColor;
            half4 _StoryAmbient;
            half4 _StoryBloom;           // x: how much colour the meadow has back (0 grey and faded, 1 in full colour)

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 Sway(float3 positionOS, float3 positionWS)
            {
                if (_SwayAmount <= 0.0) return positionWS;
                float h = saturate(positionOS.y / max(_SwayHeight, 0.01));
                float phase = dot(positionWS.xz, float2(3.5, 2.2));
                float wave = sin(_Time.y * 1.3 + phase) * 0.7 + sin(_Time.y * 2.1 + phase * 1.7) * 0.3;
                positionWS.xz += float2(0.8, 0.6) * (wave * h * h * _SwayAmount);
                return positionWS;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = Sway(input.positionOS.xyz, TransformObjectToWorld(input.positionOS.xyz));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half ao = saturate(input.color.a);
                half3 albedo = _BaseColor.rgb * SRGBToLinear(input.color.rgb) * lerp(1.0h, ao, _AOStrength);
                half3 col;
                if (_Unlit > 0.5h) col = albedo;
                else
                {
                    half3 n = normalize(input.normalWS);
                    half facing = dot(n, normalize(_StorySunDir.xyz));
                    half lit = smoothstep(-0.05h, 0.2h, facing);
                    // painted light: the shade side keeps some sun, a soft sky light from above
                    half sky = saturate(n.y * 0.5h + 0.5h);
                    col = albedo * (_StoryAmbient.rgb * lerp(0.7h, 1.0h, sky) * ao + _StorySunColor.rgb * lerp(0.28h, 1.0h, lit));
                }
                col += albedo * _Glow;
                // the Grumble has drained the meadow's colour; each Discord turned back into music brings some back
                half bloom = saturate(_StoryBloom.x);
                half grey = dot(col, half3(0.3h, 0.59h, 0.11h));
                col = lerp(half3(grey, grey, grey * 1.06h) * 0.6h, col, bloom);
                // haze: thicker further into the world, like the preview's misty depth
                float depth = dot(input.positionWS - _StoryPortalCentre.xyz, _StoryPortalInto.xyz);
                half haze = saturate((depth - 0.4) / max(_StoryPortalInto.w, 0.1)) * _StoryHaze.a;
                half3 hazeColour = lerp(half3(0.42h, 0.42h, 0.46h), _StoryHaze.rgb, bloom);
                col = lerp(col, hazeColour, haze * (1.0h - _Unlit));
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}
