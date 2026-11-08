// PICO's environment depth as an invisible shield (owner, headset 2026-11-08: "use PICO's own depth-based
// occlusion"). PICO's own occlusion shaders are for the Built-in pipeline and must be on every material; this one
// URP pass draws, before everything else, the depth of the real world (walls, furniture and hands, from the headset's
// depth sensing) into the depth buffer, so every virtual thing behind something real is hidden while the real room
// (video seethrough) still shows through: no colour is written, alpha stays 0.
// It is drawn on a big quad just in front of the eye camera. For each pixel it follows the eye's ray, looks up the
// real depth where that ray meets PICO's depth image (a few steps, as the depth camera sits almost at the eye), and
// writes that point's depth. Where PICO has no depth it writes nothing (nothing is hidden). Real surfaces are pushed
// back a little (_Bias) so a virtual thing resting on a real table is not cut into.
// Globals from PXR_EnvironmentDepthManager: _EnvironmentDepthTexture (one slice per eye),
// _EnvironmentDepthReprojectionMatrices, _EnvironmentDepthZBufferParams.
Shader "RoomOrchestra/EnvironmentDepthOccluder"
{
    Properties
    {
        _Bias ("Push real surfaces back (m)", Float) = 0.04
        _BiasScale ("Push back per metre of distance", Float) = 0.02
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-20" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "EnvironmentDepth"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest Always
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_EnvironmentDepthTexture);
            SAMPLER(sampler_EnvironmentDepthTexture);
            float4x4 _EnvironmentDepthReprojectionMatrices[2];
            float4 _EnvironmentDepthZBufferParams;
            float _Bias;
            float _BiasScale;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input, out float depth : SV_Depth) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                uint eye = unity_StereoEyeIndex;
                float3 cam = GetCameraPositionWS();
                float3 dir = normalize(input.positionWS - cam);
                float t = 1.5;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float3 p = cam + dir * t;
                    float4 q = mul(_EnvironmentDepthReprojectionMatrices[eye], float4(p, 1.0));
                    if (q.w <= 1e-4) discard;
                    float2 uv = (q.xy / q.w + 1.0) * 0.5;
                    if (any(uv < 0.0) || any(uv > 1.0)) discard;
                    float d = SAMPLE_TEXTURE2D_ARRAY_LOD(_EnvironmentDepthTexture, sampler_EnvironmentDepthTexture, uv, eye, 0).r;
                    if (d <= 0.0 || d >= 0.9999) discard; // no depth here: hide nothing
                    float linear01 = _EnvironmentDepthZBufferParams.x / ((d * 2.0 - 1.0) + _EnvironmentDepthZBufferParams.y);
                    if (!(linear01 > 0.05 && linear01 < 20.0)) discard;
                    t = t * (linear01 / q.w); // the depth camera's distance along its view, back to the eye's ray
                }
                if (!(t > 0.05 && t < 20.0)) discard;
                t += _Bias + t * _BiasScale;
                float4 cs = TransformWorldToHClip(cam + dir * t);
                depth = cs.z / cs.w;
                return 0;
            }
            ENDHLSL
        }
    }
}
