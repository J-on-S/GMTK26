// Flat tint for a cut's severed piece, drawn as an overlay over the body.
//
// No plane clipping: the mesh handed to this shader is already exactly the piece the slice would
// remove, produced by the real slicer with its finite window and connectivity rules. Clipping
// against an infinite plane here would be both redundant and wrong -- an infinite plane claims
// every limb it happens to pass through, which the real cut does not.
//
// Colour is pushed per-renderer through a MaterialPropertyBlock, so one material serves every body
// in the scene.
Shader "Cutting/CutRegionHighlight"
{
    Properties
    {
        _HighlightColor ("Highlight Colour", Color) = (0, 1, 0, 0.35)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "CutRegionHighlight"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            // no depth write: this is a tint over geometry already in the buffer.
            ZWrite Off
            // the piece shares its surface with the body it was cut from, so a plain LEqual
            // z-fights. The bias pulls it in front of those exact triangles without letting it
            // punch through anything else.
            ZTest LEqual
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _HighlightColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return _HighlightColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
