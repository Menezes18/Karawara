Shader "Custom/PosProssGrayScale"
{
    Properties 
    { 
        _MainTex ("Base (RGB)", 2D) = "white" {} 
        _Luminosity ("Luminosity", Float) = 1.0 
    }
    SubShader 
    {
        Pass 
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Luminosity;

            fixed4 frag(v2f_img i) : SV_Target 
            {
                // Obtém a cor do render target
                fixed4 renderTex = tex2D(_MainTex, i.uv);
                
                // Calcula a luminosidade (usando coeficientes para conversão em tons de cinza)
                float luminosity = 0.299 * renderTex.r + 0.587 * renderTex.g + 0.114 * renderTex.b;
                
                // Interpola entre a cor original e o tom de cinza baseado em _Luminosity
                fixed4 finalColor = lerp(renderTex, fixed4(luminosity, luminosity, luminosity, renderTex.a), _Luminosity);
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack Off
}
