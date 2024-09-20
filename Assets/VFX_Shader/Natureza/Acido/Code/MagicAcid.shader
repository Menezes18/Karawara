Shader "Custom/MagicAcid"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // Textura principal
        _Color1 ("Color 1", Color) = (0,1,0,1) // Cor verde
        _Color2 ("Color 2", Color) = (1,1,1,1) // Cor branca

        _IntersectionsGradient ("Intersections Gradient", Color) = (0,1,0,1)
        _IntersectionsEdges ("Intersections Edges", Color) = (0,0,0,1)

        _FlowSpeed ("Flow Speed", Range(0,1)) = 0.5 // Velocidade de onda
        _ExtrudeAmount ("Extrude Amount", Range(0,1)) = 0.2 // Valor da extrusão
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow

        sampler2D _MainTex;

        fixed4 _Color1;
        fixed4 _Color2;

        fixed4 _IntersectionsGradient;
        fixed4 _IntersectionsEdges;

        float _FlowSpeed;
        float _ExtrudeAmount;

        // Declaração do depth buffer
        sampler2D _CameraDepthTexture;

        struct Input
        {
            float2 uv_MainTex;
            float4 screenPos;
        };

        void vert(inout appdata_full v)
        {
            // Extrusão ao longo da normal
            v.vertex.xyz += v.normal * _ExtrudeAmount;

            // Onda com base nas coordenadas UV e tempo
            float wave = sin(v.texcoord.x * 10 + _Time.y * _FlowSpeed) * 0.1;
            v.vertex.y += wave;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            // Cores interpoladas com extrusão
            float extrudeFactor = saturate((IN.uv_MainTex.y + 1) * 0.5);
            o.Albedo = lerp(_Color1.rgb, _Color2.rgb, extrudeFactor);

            // Aplicação de textura principal
            fixed4 mainTex = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo *= mainTex.rgb;

            // Shading toon básico
            float toonShade = step(0.5, dot(normalize(o.Normal), normalize(_WorldSpaceLightPos0)));
            o.Albedo *= toonShade;

            // Verificação de interseções com o buffer de profundidade
            float sceneDepth = Linear01Depth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(IN.screenPos)).r);
            float objectDepth = IN.screenPos.z / IN.screenPos.w;

            if (sceneDepth < objectDepth)
            {
                o.Albedo = lerp(_IntersectionsEdges.rgb, _IntersectionsGradient.rgb, abs(sceneDepth - objectDepth) * 10.0);
            }
        }
        ENDCG
    }

    FallBack "Diffuse"
}
