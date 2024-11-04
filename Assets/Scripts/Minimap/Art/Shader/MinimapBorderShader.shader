Shader "Custom/MinimapBorderShader"
{
    Properties
    {
        _BorderTexture("Border Texture", 2D) = "white" {}
        _BorderThickness("Border Thickness", Range(0.0, 1.0)) = 0.1
        _MainColor("Main Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BorderTexture;
            float _BorderThickness;
            fixed4 _MainColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Converte UV para coordenadas polares
                float2 centeredUV = i.uv * 2.0 - 1.0; // Mapeia UV de (0,0)-(1,1) para (-1,-1)-(1,1)
                float dist = length(centeredUV); // Distância do centro

                // Cria uma máscara circular para a borda
                float borderMask = smoothstep(1.0 - _BorderThickness, 1.0, dist);

                // Amostra a textura da borda e multiplica pela máscara
                fixed4 borderColor = tex2D(_BorderTexture, i.uv) * borderMask;

                // Cor final (combina a cor principal com a borda)
                return lerp(_MainColor, borderColor, borderMask);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
