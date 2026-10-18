Shader "ShadowPath/UI/Light Gameplay Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 0, 0, 1)
        _Center ("Viewport Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Viewport Radius", Vector) = (0.3, 0.4, 0, 0)
        _Feather ("Normalized Feather", Range(0.0001, 1)) = 0.15
        _OutsideDarkness ("Outside Darkness", Range(0, 1)) = 0.98

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "LightGameplayMask"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float4 _Color;
            float2 _Center;
            float2 _Radius;
            float _Feather;
            float _OutsideDarkness;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 safeRadius = max(abs(_Radius), float2(0.0001, 0.0001));
                float ellipseDistance = length((input.uv - _Center) / safeRadius);
                float feather = max(_Feather, 0.0001);
                float darkness = smoothstep(1.0 - feather, 1.0, ellipseDistance);
                float alpha = darkness * saturate(_OutsideDarkness) * _Color.a * input.color.a;
                return half4(_Color.rgb * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
