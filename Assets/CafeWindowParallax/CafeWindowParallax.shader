Shader "Cafe/Window Parallax"
{
    Properties
    {
        [NoScaleOffset] _SkyTex ("Sky", 2D) = "black" {}
        [NoScaleOffset] _BackgroundTex ("Background", 2D) = "black" {}
        [NoScaleOffset] _MiddleTex ("Middle Ground", 2D) = "black" {}
        [NoScaleOffset] _ForegroundTex ("Foreground", 2D) = "black" {}
        [NoScaleOffset] _FrontTex ("Front Ground", 2D) = "black" {}
        _BackdropColor ("Backdrop Colour", Color) = (0.06, 0.025, 0.12, 1)
        _DepthStrength ("Parallax Strength", Range(0, 3)) = 1
        _SkyDepth ("Sky Depth (Metres)", Range(0, 1)) = 0.30
        _BackgroundDepth ("Background Depth (Metres)", Range(0, 1)) = 0.22
        _MiddleDepth ("Middle Depth (Metres)", Range(0, 1)) = 0.12
        _ForegroundDepth ("Foreground Depth (Metres)", Range(0, 1)) = 0.05
        _FrontDepth ("Front Depth (Metres)", Range(0, 1)) = 0.015
        _Overscan ("Artwork Overscan", Range(1, 2)) = 1.08
        _UVScale ("Artwork UV Scale", Vector) = (1, 1, 0, 0)
        _UVOffset ("Artwork UV Offset", Vector) = (0, 0, 0, 0)
        _AlphaGain ("Layer Alpha Gain", Range(1, 1.1)) = 1.015
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _SkyTex;
            sampler2D _BackgroundTex;
            sampler2D _MiddleTex;
            sampler2D _ForegroundTex;
            sampler2D _FrontTex;
            float4 _SkyTex_TexelSize;
            float4 _BackgroundTex_TexelSize;
            float4 _MiddleTex_TexelSize;
            float4 _ForegroundTex_TexelSize;
            float4 _FrontTex_TexelSize;
            half4 _BackdropColor;
            float _DepthStrength;
            float _SkyDepth;
            float _BackgroundDepth;
            float _MiddleDepth;
            float _ForegroundDepth;
            float _FrontDepth;
            float _Overscan;
            float4 _UVScale;
            float4 _UVOffset;
            half _AlphaGain;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                return o;
            }

            float2 LayerUV(float2 baseUV, float2 viewSlope, float depth, float4 texelSize)
            {
                // Trace away from the eye to a virtual layer behind the window.
                float2 shift = -viewSlope * depth * _DepthStrength;
                float shiftLength = length(shift);
                shift *= min(1.0, 0.075 / max(shiftLength, 0.000001));

                // Keep the existing textures unchanged, even if their import mode repeats.
                float2 inset = max(abs(texelSize.xy) * 0.5, float2(0.000001, 0.000001));
                return clamp(baseUV + shift, inset, 1.0 - inset);
            }

            half3 Over(half3 under, half4 layer)
            {
                // PNGs use straight alpha; gain closes their almost-opaque interiors.
                return lerp(under, layer.rgb, saturate(layer.a * _AlphaGain));
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 artworkUV = i.uv * _UVScale.xy + _UVOffset.xy;
                float2 baseUV = (artworkUV - 0.5) / max(_Overscan, 1.0) + 0.5;

                // World-to-UV gradients preserve mirrored UVs, object scaling and batching.
                float3 dpdx = ddx(i.worldPos);
                float3 dpdy = ddy(i.worldPos);
                float2 duvdx = ddx(baseUV);
                float2 duvdy = ddy(baseUV);
                float3 normal = cross(dpdx, dpdy);
                normal *= rsqrt(max(dot(normal, normal), 1.0e-20));
                float3 reciprocalX = cross(dpdy, normal);
                float3 reciprocalY = cross(normal, dpdx);
                float inverseDeterminant = rcp(max(dot(dpdx, reciprocalX), 1.0e-12));
                float3 gradientU = (duvdx.x * reciprocalX + duvdy.x * reciprocalY) * inverseDeterminant;
                float3 gradientV = (duvdx.y * reciprocalX + duvdy.y * reciprocalY) * inverseDeterminant;

                // Unity supplies the correct eye position in multi-pass and single-pass VR.
                float3 view = UnityWorldSpaceViewDir(i.worldPos);
                view *= rsqrt(max(dot(view, view), 1.0e-12));
                float normalView = max(abs(dot(view, normal)), 0.18);
                float2 viewSlope = float2(dot(gradientU, view), dot(gradientV, view)) / normalView;

                half3 colour = _BackdropColor.rgb;
                colour = Over(colour, tex2D(_SkyTex, LayerUV(baseUV, viewSlope, _SkyDepth, _SkyTex_TexelSize)));
                colour = Over(colour, tex2D(_BackgroundTex, LayerUV(baseUV, viewSlope, _BackgroundDepth, _BackgroundTex_TexelSize)));
                colour = Over(colour, tex2D(_MiddleTex, LayerUV(baseUV, viewSlope, _MiddleDepth, _MiddleTex_TexelSize)));
                colour = Over(colour, tex2D(_ForegroundTex, LayerUV(baseUV, viewSlope, _ForegroundDepth, _ForegroundTex_TexelSize)));
                colour = Over(colour, tex2D(_FrontTex, LayerUV(baseUV, viewSlope, _FrontDepth, _FrontTex_TexelSize)));
                return half4(colour, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
