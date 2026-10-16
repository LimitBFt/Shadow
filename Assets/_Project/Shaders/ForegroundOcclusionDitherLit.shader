Shader "ShadowPath/Foreground Occlusion Dither Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5

        [Toggle(_NORMALMAP)] _NormalMapEnabled("Use Normal Map", Float) = 0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1

        [Toggle(_OCCLUSIONMAP)] _OcclusionMapEnabled("Use Occlusion Map", Float) = 0
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1

        [Toggle(_EMISSION)] _EmissionEnabled("Use Emission", Float) = 0
        _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)

        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

        [HideInInspector] _OcclusionAmount("Occlusion Amount", Range(0, 1)) = 0
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _SpecColor("Specular Color", Color) = (0.2, 0.2, 0.2, 1)
        [HideInInspector] _Parallax("Parallax", Float) = 0.005
        [HideInInspector] _DetailAlbedoMapScale("Detail Albedo Scale", Float) = 1
        [HideInInspector] _DetailNormalMapScale("Detail Normal Scale", Float) = 1
        [HideInInspector] _ClearCoatMask("Clear Coat Mask", Float) = 0
        [HideInInspector] _ClearCoatSmoothness("Clear Coat Smoothness", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    float4 _OcclusionCenter;
    float _InnerRadius;
    float _OuterRadius;
    float _OcclusionAspect;

    UNITY_INSTANCING_BUFFER_START(ShadowPathOcclusionProperties)
        UNITY_DEFINE_INSTANCED_PROP(float, _OcclusionAmount)
    UNITY_INSTANCING_BUFFER_END(ShadowPathOcclusionProperties)

    float ShadowPathBayer4x4(float2 positionSS)
    {
        float2 cell = fmod(floor(positionSS), 4.0);
        float row0 = cell.x < 1.0 ? 0.0 : (cell.x < 2.0 ? 8.0 : (cell.x < 3.0 ? 2.0 : 10.0));
        float row1 = cell.x < 1.0 ? 12.0 : (cell.x < 2.0 ? 4.0 : (cell.x < 3.0 ? 14.0 : 6.0));
        float row2 = cell.x < 1.0 ? 3.0 : (cell.x < 2.0 ? 11.0 : (cell.x < 3.0 ? 1.0 : 9.0));
        float row3 = cell.x < 1.0 ? 15.0 : (cell.x < 2.0 ? 7.0 : (cell.x < 3.0 ? 13.0 : 5.0));
        float index = cell.y < 1.0 ? row0 : (cell.y < 2.0 ? row1 : (cell.y < 3.0 ? row2 : row3));
        return (index + 0.5) / 16.0;
    }

    void ShadowPathApplyOcclusionDither(float4 positionCS)
    {
        float occlusionAmount = saturate(
            UNITY_ACCESS_INSTANCED_PROP(ShadowPathOcclusionProperties, _OcclusionAmount));
        float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
        float2 centerDelta = screenUV - _OcclusionCenter.xy;
        centerDelta.x *= max(_OcclusionAspect, 0.0001);

        float innerRadius = max(_InnerRadius, 0.0);
        float outerRadius = max(_OuterRadius, innerRadius + 0.00001);
        float holeMask = 1.0 - smoothstep(innerRadius, outerRadius, length(centerDelta));
        float clipStrength = saturate(holeMask * occlusionAmount);
        float threshold = ShadowPathBayer4x4(positionCS.xy);
        clip(threshold - clipStrength);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One Zero
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment ShadowPathLitPassFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

            #define LitPassFragment ShadowPathOriginalLitPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #undef LitPassFragment

            void ShadowPathLitPassFragment(
                Varyings input,
                out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ShadowPathApplyOcclusionDither(input.positionCS);

            #ifdef _WRITE_RENDERING_LAYERS
                ShadowPathOriginalLitPassFragment(input, outColor, outRenderingLayers);
            #else
                ShadowPathOriginalLitPassFragment(input, outColor);
            #endif
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment ShadowPathDepthOnlyFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

            #define DepthOnlyFragment ShadowPathOriginalDepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            #undef DepthOnlyFragment

            half ShadowPathDepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ShadowPathApplyOcclusionDither(input.positionCS);
                return ShadowPathOriginalDepthOnlyFragment(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment ShadowPathDepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

            #define DepthNormalsFragment ShadowPathOriginalDepthNormalsFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            #undef DepthNormalsFragment

            void ShadowPathDepthNormalsFragment(
                Varyings input,
                out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ShadowPathApplyOcclusionDither(input.positionCS);

            #ifdef _WRITE_RENDERING_LAYERS
                ShadowPathOriginalDepthNormalsFragment(input, outNormalWS, outRenderingLayers);
            #else
                ShadowPathOriginalDepthNormalsFragment(input, outNormalWS);
            #endif
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/Meta"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
