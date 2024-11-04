Shader "Custom/CircleMask"
{
    Properties
    {
        _Radius ("Radius", Range(0, 1)) = 0.5 // Propriedade para o raio
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _Radius;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 center = float2(0.5, 0.5); // Centro da máscara circular
                float dist = distance(i.uv, center);
                
                // Define a máscara de transparência sem alterar a cor
                float mask = step(dist, _Radius);

                // Apenas aplica a máscara ao canal alfa
                fixed4 col = tex2D(_MainTex, i.uv);
                col.a *= mask;

                return col;
            }
            ENDCG
        }
    }
}
