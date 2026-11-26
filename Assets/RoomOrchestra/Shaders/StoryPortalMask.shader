// Story mode's portal opening (owner, 2026-11-25: "what happened to the aesthetic that I loved from the preview"):
// the round opening writes a mark into the stencil buffer, nothing else (no colour, no depth), before anything is drawn.
// The meadow behind the portal (StoryPortalWorld) is drawn only where that mark is, so it is seen through the opening
// like a window into another world, with real depth as the head moves, and never outside the ring.
Shader "RoomOrchestra/StoryPortalMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-20" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "PortalMask"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Cull Off
            Stencil
            {
                Ref 71
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
