// Mars sky: butterscotch by day, a cool blue halo around the (small) sun, dark brown in dust storms,
// stars at night. Cheap single-pass skybox; works in URP and WebGL. HazardVisuals drives the colors.
Shader "MissionTrainer/MarsSky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.48, 0.34, 0.26, 1)
        _HorizonColor ("Horizon", Color) = (0.86, 0.66, 0.48, 1)
        _GroundColor ("Below horizon", Color) = (0.45, 0.30, 0.21, 1)
        _SunColor ("Sun disc", Color) = (1, 0.95, 0.85, 1)
        _HaloColor ("Sun halo (blue on Mars)", Color) = (0.62, 0.72, 0.86, 1)
        _SunDir ("Direction to sun", Vector) = (0.5, 0.45, 0.5, 0)
        _SunSize ("Sun size (1 - cos radius)", Range(0.000005, 0.002)) = 0.00004
        _HaloStrength ("Halo strength", Range(0, 2)) = 0.35
        _HaloPower ("Halo tightness", Range(1, 256)) = 28
        _HorizonSharpness ("Horizon blend", Range(0.1, 2)) = 0.55
        _StarIntensity ("Stars", Range(0, 2)) = 0
        _Exposure ("Exposure", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _HaloColor, _SunDir;
            float _SunSize, _HaloStrength, _HaloPower, _HorizonSharpness, _StarIntensity, _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                // Vertical gradient: bright dusty horizon, deeper tan overhead.
                float up = pow(saturate(h), _HorizonSharpness);
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, up);
                col = lerp(col, _GroundColor.rgb, saturate(-h * 6.0));

                // Mars dust scatters blue light forward, so the sky right around the sun looks blue.
                float3 s = normalize(_SunDir.xyz);
                float sd = saturate(dot(d, s));
                col += _HaloColor.rgb * (pow(sd, _HaloPower) + 0.25 * pow(sd, 4.0)) * _HaloStrength;

                // Small sun (Mars is farther away: the sun looks about 2/3 the size it does from Earth).
                // Kept only slightly above the bloom threshold so the glow stays tight.
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.35, sd);
                col += _SunColor.rgb * disc * 2.5;

                // Stars (only drawn when it's dark)
                if (_StarIntensity > 0.001)
                {
                    float3 cell = floor(d * 280.0);
                    float st = hash13(cell);
                    float star = step(0.9975, st) * saturate(h * 4.0);
                    col += star * _StarIntensity * (0.4 + 0.6 * hash13(cell + 7.0));
                }

                return float4(col * _Exposure, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
