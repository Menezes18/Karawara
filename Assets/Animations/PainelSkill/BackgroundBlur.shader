Shader "UI/BlurredBackground"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 10)) = 1.0
        _Opacity ("Opacity", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _BlurSize;
            float _Opacity;

            // Função de desfoque
            fixed4 ApplyBlur(float2 uv)
            {
                float2 offsets[9] = {
                    float2(-1, -1), float2(0, -1), float2(1, -1),
                    float2(-1,  0), float2(0,  0), float2(1,  0),
                    float2(-1,  1), float2(0,  1), float2(1,  1)
                };

                float kernel[9] = {
                    1.0/16, 2.0/16, 1.0/16,
                    2.0/16, 4.0/16, 2.0/16,
                    1.0/16, 2.0/16, 1.0/16
                };

                fixed4 col = fixed4(0, 0, 0, 0);
                float blurAmount = _BlurSize / _ScreenParams.y;

                for (int i = 0; i < 9; i++)
                {
                    col += tex2D(_MainTex, uv + offsets[i] * blurAmount) * kernel[i];
                }

                return col;
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                // Aplica o desfoque
                fixed4 blurredCol = ApplyBlur(uv);

                // Aplica a opacidade ao fundo desfocado
                blurredCol.a *= _Opacity;

                return blurredCol;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
