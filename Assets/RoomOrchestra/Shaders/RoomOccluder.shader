// The real room as an invisible shield (PICO XR mode, owner 2026-11-07: "occlusion needs to be much better"): the scanned
// room's surface is drawn into depth only, before everything else, so a virtual object behind a real wall or piece of
// furniture is hidden while the real room (video seethrough) still shows through (no colour, alpha stays 0).
// Owner, headset 2026-11-08: PICO's depth image was not accurate enough; this scanned mesh (the same shapes the game picks
// furniture from) is now the only occluder. It is drawn a little further away than it is (_PushBack, metres along the
// view ray) so what sits on a real object (its note bands and labels) is never hidden by that object's own surface.
Shader "RoomOrchestra/RoomOccluder"
{
    Properties
    {
        _PushBack ("Push back (m)", Float) = 0.03
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Occluder"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            CBUFFER_START(UnityPerMaterial)
                float _PushBack;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 view = TransformWorldToView(world);
                view -= normalize(view) * _PushBack; // along the eye's ray, away from the eye (view space looks down -z)
                output.positionCS = TransformWViewToHClip(view);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
