Shader "Unlit/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _Thickness ("Thickness", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _Thickness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Uniform object-space expansion avoids hard-edge artifacts on cube meshes.
                float3 expandedOS = IN.positionOS.xyz * (1.0 + _Thickness);
                OUT.positionHCS = TransformObjectToHClip(expandedOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
