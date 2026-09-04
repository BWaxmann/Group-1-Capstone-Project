Shader "Custom/ShiftingSurface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Direction ("Wave 1 Direction", Vector) = (0,1,0,0)
        _Magnitude ("Wave 1 Size", Float) = 0.01
        _Speed ("Wave 1 Speed", Float) = 2
        _Direction2 ("Wave 2 Direction", Vector) = (1,0,1,0)
        _Magnitude2 ("Wave 2 Size", Float) = 0.005
        _Speed2 ("Wave 2 Speed", Float) = 1
        _TexDirect ("Texture Shift Direction", Vector) = (0,1,0,0)
        _TexSpeed ("Texture Shift Speed", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert

        #pragma target 2.5

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float3 _Direction;
        float _Magnitude;
        float _Speed;
        float3 _Direction2;
        float _Magnitude2;
        float _Speed2;
        float2 _TexDirect;
        float _TexSpeed;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert (inout appdata_base v)
        {
            float along = _Time.y * dot(normalize(_Direction), v.vertex.xyz);
            float wave1 = _Magnitude * sin(_Speed * along);
            float wave2 = _Magnitude2 * sin(_Speed2 * along);
            v.vertex.xyz += v.normal * (wave1 + wave2);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, frac(IN.uv_MainTex + _Time.y * _TexSpeed * normalize(_TexDirect))) * _Color;
            
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
