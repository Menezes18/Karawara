Shader "Custom/URP_CrystalBallShaderWithSmoke"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _NoiseMap("Noise Map (Smoke)", 2D) = "white" {}
        _DistortionStrength("Distortion Strength", Range(0, 0.5)) = 0.2
        _SmokeIntensity("Smoke Intensity", Range(0, 1)) = 0.5
        _RotationX("Rotation X", Range(-180, 180)) = 0
        _RotationY("Rotation Y", Range(-180, 180)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 200

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            float _DistortionStrength;
            float _SmokeIntensity;
            float _RotationX;
            float _RotationY;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Aplica rotação nos eixos X e Y
                float angleX = radians(_RotationX);
                float angleY = radians(_RotationY);
                float cosX = cos(angleX);
                float sinX = sin(angleX);
                float cosY = cos(angleY);
                float sinY = sin(angleY);

                uv -= 0.5;
                float2 rotatedUV;
                rotatedUV.x = uv.x * cosX - uv.y * sinX;
                rotatedUV.y = uv.x * sinY + uv.y * cosY;
                rotatedUV += 0.5;

                // Aplica uma distorção esférica mais suave
                float2 distortedUV = (rotatedUV - 0.5) * (1.0 - _DistortionStrength) + 0.5;

                // Obtenha a cor da textura base
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, distortedUV);

                // Obtenha a textura de ruído para o efeito de fumaça
                half4 noiseColor = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv);
                float smoke = noiseColor.r * _SmokeIntensity;

                // Mistura a fumaça com a cor base
                half4 finalColor = lerp(baseColor, half4(1, 1, 1, 1), smoke);
                finalColor.a = baseColor.a; // Preserva a transparência original

                return finalColor;
            }
            ENDHLSL
        }
    }
}
