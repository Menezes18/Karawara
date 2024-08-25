Shader "Custom/URP/FastProceduralOcean"
{
    Properties
    {
        [Header(GPU Instancing cannot be used due to Phong Tessellation)]
        [Space]
        _Phong("Phong Tessellation Strength", Range(0, 1)) = 0.5
        _EdgeLength("Tessellation Edge Length", Range(2.5, 100)) = 100

        [Space]
        [IntRange] _VertexIterations ("Vertex Iterations", Range(1, 64)) = 8
        [IntRange] _NormalIterations ("Normal Iterations", Range(1, 64)) = 32
        [Toggle] _TangentSpace ("Tangent Space Deformation", Float) = 1

        [Space]
        _WaveDensity ("Wave Density", Float) = 4.5
        _WaveHeight ("Wave Height", Range(0, 10)) = 0.5
        _WaveSharpness ("Wave Peak Strength", Range(0.01, 0.4)) = 0.16

        [Space]
        _WaveNormalStrength ("Wave Normal Strength", Range(0,2)) = 1

        [Space]
        _WaveSpeed ("Wave Speed", Range(0, 50)) = 12.5
        _WaveMovement ("Wave Movement", Vector) = (0.1, 0.1, 0, 0)

        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamPower("Foam Power", Float) = 1.125
        _FoamStrength("Foam Strength", Float) = 10

        [Space]
        _OceanColor ("Color", Color) = (0.2216981, 0.6714061, 1, 1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.8
        _Metallic ("Metallic", Range(0, 1)) = 0.0

        _Brightness("Brightness", Range(0,1)) = 0.3
        _Strength("Strength", Range(0,10)) = 0.5 
        _Color("Color",COLOR) = (1,1,1,1)
        _Detail("Detail", Range(0,1)) = 0.3

        // Adicionando propriedade de transparência
        _Transparency("Transparency", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 200

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                half3 worldNormal : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OceanColor;
                float4 _WaveMovement;
                float _WaveSpeed;
                float _WaveHeight;
                float _WaveDensity;
                float _WaveSharpness;
                float _WaveNormalStrength;
                float4 _FoamColor;
                float _FoamPower;
                float _FoamStrength;
                float _Glossiness;
                float _Metallic;
                float4 _MainTex_ST;
                int _VertexIterations;
                int _NormalIterations;
                float _Brightness;
                float _Strength;
                float4 _Color;
                float _Detail;
                float _Transparency; // Adicionando variável de transparência
            CBUFFER_END

            sampler2D _MainTex;

            // Voronoi noise function
            float hash(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float3 hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float voronoi(float3 p)
            {
                float3 b = floor(p);
                float3 f = frac(p);
                float res = 8.0;
                for (int k = -1; k <= 1; k++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            float3 r = float3(float(i), float(j), float(k));
                            float3 g = hash(b + r);
                            float d = length(r + g - f);
                            res = min(res, d);
                        }
                    }
                }
                return res;
            }

            float Toon(float3 normal, float3 lightDir) {
                float NdotL = max(0.0, dot(normalize(normal), normalize(lightDir)));
                return floor(NdotL / _Detail); ////* _Detail;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS).xyz;
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));

                // Ajusta as coordenadas UV, provavelmente...
                float2 uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                uv += _WaveMovement.xy * _Time.y * _WaveSpeed;

                float noise = voronoi(float3(uv * 0.5, _Time.y * 0.1));
                uv += noise * 0.1; // Aplica as coordenadas UV, provavelmente...

                float wave = sin(uv.x * _WaveDensity + _Time.y * _WaveSpeed) * cos(uv.y * _WaveDensity + _Time.y * _WaveSpeed);
                worldPos.y += wave * _WaveHeight;

                output.positionHCS = TransformWorldToHClip(worldPos);
                output.uv = uv;
                output.normalWS = normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(worldPos);
                output.worldPos = worldPos;
                output.worldNormal = normalWS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = input.normalWS;
                
                half3 viewDir = normalize(input.viewDirWS);

                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);

                float3 albedo = _OceanColor.rgb * tex2D(_MainTex, input.uv).rgb;
                float3 foam = _FoamColor.rgb * pow(1 - normal.y, _FoamPower) * _FoamStrength;

                half3 diffuse = albedo + foam;
                half3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * albedo;

                float nh = max(0.0, dot(normal, lightDir));
                half3 reflection = normalize(reflect(-lightDir, normal));

                float toonShade = Toon(input.worldNormal, lightDir);
                half4 toonColor = half4(toonShade * _Strength * _Color.rgb + _Brightness, 1.0);

            
                

                half4 color = half4(ambient + diffuse * nh, 1.0) * toonColor;
                color.a *= _Transparency; // Ajustando a transparência

                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
