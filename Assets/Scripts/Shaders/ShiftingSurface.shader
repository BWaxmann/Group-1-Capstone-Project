Shader "Custom/ShiftingSurface"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Albedo (RGB)", 2D) = "white" {}
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
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 2.5

        struct Input
        {
            float2 uv_MainTex;
        };

        sampler2D _MainTex;
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

        void vert (inout appdata_full v)
        {
            float vertProj = dot(normalize(_Direction), v.vertex.xyz);
            float vertProj2 = dot(normalize(_Direction2), v.vertex.xyz);
            float wave1 = _Magnitude * sin(_Time.y * _Speed * vertProj);
            float wave2 = _Magnitude2 * sin(_Time.y * _Speed2 * vertProj2);
            v.vertex.xyz += v.normal * (wave1 + wave2);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D (_MainTex, frac(IN.uv_MainTex + _Time.y * _TexSpeed * normalize(_TexDirect)));
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
