Shader "Unlit/Grid"
{
    Properties
    {
        _GridColor("Grid Color", Color) = (1, 1, 1, 0.5)
        _GridSize("Grid Size", Float) = 99
        _LineWidth("Line Width", Range(0, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset 1, 1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _GridColor;
            float _GridSize;
            float _LineWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 scaledUV = i.uv * _GridSize;

                float2 derivative = fwidth(scaledUV);
                float2 cellUV = frac(scaledUV);
                float2 dist = min(cellUV, 1.0 - cellUV);
                float2 antiAliasedLine = 1.0 - smoothstep(_LineWidth, _LineWidth + derivative, dist);

                float finalAlpha = saturate(max(antiAliasedLine.x, antiAliasedLine.y));
                return fixed4(_GridColor.rgb, _GridColor.a * finalAlpha);
            }
            ENDCG
        }
    }
}
