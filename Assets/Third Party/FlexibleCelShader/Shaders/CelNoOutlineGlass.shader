
Shader "FlexibleCelShader/Cel No Outline Glass"
{
    Properties
    {
        _Color("Global Color Modifier", Color) = (1, 1, 1, 1)
        _Opacity("Opacity", Range(0, 1)) = 1
        _MainTex("Texture", 2D) = "white" {}
        _NormalTex("Normal", 2D) = "bump" {}
        _EmmisTex("Emission", 2D) = "black" {}

        _RampLevels("Ramp Levels", Range(2, 50)) = 2
        _LightScalar("Light Scalar", Range(0, 10)) = 1

        _HighColor("High Light Color", Color) = (1, 1, 1, 1)
        _HighIntensity("High Light Intensity", Range(0, 10)) = 1.5

        _LowColor("Low Light Color", Color) = (1, 1, 1, 1)
        _LowIntensity("Low Light Intensity", Range(0, 10)) = 1

        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineSize("Outline Size", float) = 10

        _RimColor("Hard Edge Light Color", Color) = (1, 1, 1, 1)
        _RimAlpha("Hard Edge Light Brightness", Range(0, 1)) = 0
        _RimPower("Hard Edge Light Size", Range(0, 1)) = 0
        _RimDropOff("Hard Edge Light Dropoff", Range(0, 1)) = 0

        _FresnelColor("Soft Edge Light Color", Color) = (1, 1, 1, 1)
        _FresnelBrightness("Soft Edge Light Brightness", Range(0, 1)) = 0
        _FresnelPower("Soft Edge Light Size", Range(0, 1)) = 0
        _FresnelShadowDropoff("Soft Edge Light Dropoff", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        // Main lighting pass.
        // Handles the primary per-object light and applicable shadows.
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                float3 worldPos : TEXCOORD4;
                LIGHTING_COORDS(5, 6)
                float4 pos : SV_POSITION;
            };

            float4 _Color;
            float _Opacity;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _NormalTex;
            float4 _NormalTex_ST;

            sampler2D _EmmisTex;
            float4 _EmmisTex_ST;

            float _RampLevels;
            float _LightScalar;

            float _HighIntensity;
            float4 _HighColor;

            float _LowIntensity;
            float4 _LowColor;

            float _RimPower;
            float _RimAlpha;
            float4 _RimColor;
            float _RimDropOff;

            float _FresnelBrightness;
            float _FresnelPower;
            float4 _FresnelColor;
            float _FresnelShadowDropoff;

            v2f vert(appdata_tan v)
            {
                v2f o;

                o.uv = v.texcoord;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);

                o.worldBitangent =
                    cross(o.worldNormal, o.worldTangent) * v.tangent.w;

                TRANSFER_VERTEX_TO_FRAGMENT(o);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rampLevels = max(_RampLevels - 1.0, 1.0);

                // Correctly calculate direction for directional,
                // point and spot lights.
                float3 lightVector =
                    _WorldSpaceLightPos0.xyz -
                    i.worldPos * _WorldSpaceLightPos0.w;

                float3 lightDirection = normalize(lightVector);

                float3 viewDirection = normalize(
                    _WorldSpaceCameraPos.xyz - i.worldPos
                );

                fixed4 col = tex2D(
                    _MainTex,
                    i.uv * _MainTex_ST.xy + _MainTex_ST.zw
                );

                fixed3 tangentNormal = tex2D(
                    _NormalTex,
                    i.uv * _NormalTex_ST.xy + _NormalTex_ST.zw
                ).rgb * 2 - 1;

                fixed4 emission = tex2D(
                    _EmmisTex,
                    i.uv * _EmmisTex_ST.xy + _EmmisTex_ST.zw
                );

                float3 worldNormal = normalize(
                    i.worldTangent * tangentNormal.r +
                    i.worldBitangent * tangentNormal.g +
                    i.worldNormal * tangentNormal.b
                );

                // Fresnel and rim lighting.
                float factor = dot(viewDirection, worldNormal);

                float fresnelFactor = 1 - min(
                    pow(max(1 - factor, 0),
                    (1 - _FresnelPower) * 10), 1
                );

                // Unity handles light attenuation and applicable shadows.
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

                float intensity = saturate(
                    dot(worldNormal, lightDirection) * _LightScalar
                );

                intensity *= atten;

                // Cel-shading ramp.
                float rampLevel = round(intensity * rampLevels);

                float lightMultiplier = _LowIntensity +
                    ((_HighIntensity - _LowIntensity) / rampLevels)
                    * rampLevel;

                float4 highColor =
                    (rampLevel / rampLevels) * _HighColor;

                float4 lowColor =
                    ((rampLevels - rampLevel) / rampLevels) * _LowColor;

                float4 mixColor = (highColor + lowColor) / 2;

                col *= lightMultiplier;
                col *= _Color * mixColor;

                // Soft Fresnel lighting.
                float rampPercentSoftFresnel = 1 -
                    ((1 - rampLevel / rampLevels) *
                    (1 - _FresnelShadowDropoff));

                col.rgb += _FresnelColor.rgb *
                    (_FresnelBrightness * 10 -
                    fresnelFactor * _FresnelBrightness * 10)
                    * rampPercentSoftFresnel;

                // Hard rim lighting.
                float rimAlpha = _RimAlpha *
                    (1 - ((1 - rampLevel / rampLevels) *
                    (1 - _RimDropOff)));

                if (factor <= _RimPower)
                {
                    col.rgb = _RimColor.rgb * rimAlpha +
                              col.rgb * (1 - rimAlpha);
                }

                // Emission.
                half emissionIntensity = max(
                    emission.r, max(emission.g, emission.b)
                );

                col.rgb = emission.rgb * emissionIntensity +
                          col.rgb * (1 - emissionIntensity);

                // Preserve texture alpha and apply material tint and opacity.
                col.a *= _Color.a * _Opacity;

                return col;
            }
            ENDCG
        }

        // Additional light pass.
        // Supports additional point lights and spotlights.
        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }

            Blend One One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vertAdd
            #pragma fragment fragAdd
            #pragma multi_compile_fwdadd_fullshadows

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct v2fAdd
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                float3 worldPos : TEXCOORD4;
                LIGHTING_COORDS(5, 6)
                float4 pos : SV_POSITION;
            };

            float4 _Color;
            float _Opacity;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _NormalTex;
            float4 _NormalTex_ST;

            float _RampLevels;
            float _LightScalar;

            float _HighIntensity;
            float4 _HighColor;

            float _LowIntensity;
            float4 _LowColor;

            v2fAdd vertAdd(appdata_tan v)
            {
                v2fAdd o;

                o.uv = v.texcoord;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);

                o.worldBitangent =
                    cross(o.worldNormal, o.worldTangent) * v.tangent.w;

                TRANSFER_VERTEX_TO_FRAGMENT(o);

                return o;
            }

            fixed4 fragAdd(v2fAdd i) : SV_Target
            {
                float rampLevels = max(_RampLevels - 1.0, 1.0);

                // Direction to the current additional light.
                float3 lightVector =
                    _WorldSpaceLightPos0.xyz -
                    i.worldPos * _WorldSpaceLightPos0.w;

                float3 lightDirection = normalize(lightVector);

                fixed4 col = tex2D(
                    _MainTex,
                    i.uv * _MainTex_ST.xy + _MainTex_ST.zw
                );

                fixed3 tangentNormal = tex2D(
                    _NormalTex,
                    i.uv * _NormalTex_ST.xy + _NormalTex_ST.zw
                ).rgb * 2 - 1;

                float3 worldNormal = normalize(
                    i.worldTangent * tangentNormal.r +
                    i.worldBitangent * tangentNormal.g +
                    i.worldNormal * tangentNormal.b
                );

                // Includes point-light distance falloff,
                // spotlight cone attenuation and supported shadows.
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

                float intensity = saturate(
                    dot(worldNormal, lightDirection) * _LightScalar
                );

                intensity *= atten;

                // Use the same cel-shading ramp as the base pass.
                float rampLevel = round(intensity * rampLevels);

                float lightMultiplier = _LowIntensity +
                    ((_HighIntensity - _LowIntensity) / rampLevels)
                    * rampLevel;

                float4 highColor =
                    (rampLevel / rampLevels) * _HighColor;

                float4 lowColor =
                    ((rampLevels - rampLevel) / rampLevels) * _LowColor;

                float4 mixColor = (highColor + lowColor) / 2;

                col *= lightMultiplier;
                col *= _Color * mixColor;

                // Add the current light's color and attenuation.
                col.rgb *= _LightColor0.rgb * atten;

                // Match the transparent surface's alpha in the additive pass.
                col.rgb *= _Opacity * _Color.a;

                // Additive passes contribute RGB only.
                col.a = 0;

                return col;
            }
            ENDCG
        }

        // No outline pass in this shader.

        // Shadow casting.
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }

    FallBack "VertexLit"
}