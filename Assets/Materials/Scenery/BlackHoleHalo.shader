Shader "GravityRoguelike/Black Hole Halo"
{
    Properties
    {
        _Color ("Halo Colour", Color) = (0.42, 0.12, 0.7, 0.45)
        _ShimmerSpeed ("Slow Shimmer Speed", Float) = 0.25
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.75
        _HaloAlpha ("Halo Alpha", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _Color;
            float _ShimmerSpeed;
            float _GlowIntensity;
            float _HaloAlpha;
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float radius = length((input.uv - 0.5) * 2);
                float glow = pow(saturate(1 - radius), 2);
                float shimmer = 0.94 + 0.06 * sin(_Time.y * _ShimmerSpeed);
                return float4(_Color.rgb * _GlowIntensity, glow * shimmer * _HaloAlpha);
            }
            ENDHLSL
        }
    }
}
