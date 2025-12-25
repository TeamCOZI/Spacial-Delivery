Shader "Unlit/Outline"
{
    Properties
    {
        _Color ("Outline Color", Color) = (1,1,1,1)
        _Thickness ("Thickness", Range(0, 0.1)) = 0.01
        _IsVisible ("Is Visible", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="Universalpipeline"}

        Pass
        {
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/Shaderlibrary/Core.hlsl"

            struct Atrributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Thickness;
                float _IsVisible;
            CBUFFER_END

            Varyings vert(Atrributes IN)
            {
                Varyings OUT;

                float finalThickness = _Thickness * _IsVisible;

                float3 positionOS = IN.positionOS.xyz + IN.normalOS * finalThickness;
                OUT.positionHCS = TransformObjectToHClip(positionOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}