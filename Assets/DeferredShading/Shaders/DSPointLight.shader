﻿Shader "DeferredShading/DSPointLight" {

Properties {
    g_normal_buffer ("Normal", 2D) = "white" {}
    g_position_buffer ("Position", 2D) = "white" {}
    _ColorBuffer ("Color", 2D) = "white" {}
    g_glow_buffer ("Glow", 2D) = "white" {}
    _SpecularStrength ("Specular Strength", Float) = 10.0
}
SubShader {
    CGINCLUDE
    #include "Compat.cginc"
    #include "DS.cginc"

    sampler2D g_normal_buffer;
    sampler2D g_position_buffer;
    sampler2D _ColorBuffer;
    sampler2D g_glow_buffer;
    float4 _LightColor;
    float4 _LightPosition;
    float4 _LightRange; // [0]: range, [1]: 1.0/range
    float4 _ShadowParams; // [0]: 0=disabled, [1]: steps
    float _SpecularStrength;


    struct ia_out
    {
        float4 vertex : POSITION;
    };

    struct vs_out
    {
        float4 vertex : SV_POSITION;
        float4 screen_pos : TEXCOORD0;
        float4 lightpos_mvp : TEXCOORD1;
    };

    struct ps_out
    {
        float4 color : COLOR0;
    };


    vs_out vert(ia_out v)
    {
        vs_out o;
        float4 vertex = mul(UNITY_MATRIX_MVP, float4(v.vertex.xyz * (_LightRange.x*2.0), 1.0) );
        o.vertex = vertex;
        o.screen_pos = vertex;
        o.lightpos_mvp = mul(UNITY_MATRIX_VP, float4(_LightPosition.xyz, 1.0f));
        return o;
    }


    ps_out frag (vs_out i)
    {
        float2 coord = (i.screen_pos.xy / i.screen_pos.w + 1.0) * 0.5;
        // see: http://docs.unity3d.com/Manual/SL-PlatformDifferences.html
        #if UNITY_UV_STARTS_AT_TOP
            coord.y = 1.0-coord.y;
        #endif

        float4 FragPos4		= tex2D(g_position_buffer, coord);
        if(FragPos4.w==0.0) { discard; }
        float4 AS		= tex2D(_ColorBuffer, coord);
        float4 NS		= tex2D(g_normal_buffer, coord);

        float3 FragPos		= FragPos4.xyz;
        float3 LightColor	= _LightColor.rgb;
        float3 LightPos		= _LightPosition.xyz;
        float3 LightDiff	= _LightPosition.xyz - FragPos.xyz;
        float LightDistSq	= dot(LightDiff, LightDiff);
        float LightDist		= sqrt(LightDistSq);
        float3  LightDir	= LightDiff / LightDist;
        float LightAttenuation	= max(_LightRange.x-LightDist, 0.0)*_LightRange.y;
        if(LightAttenuation==0.0) { discard; }

        if(_ShadowParams[0]!=0.0f) {
            float4 LightPositionMVP = i.lightpos_mvp;
            float2 lcoord = (LightPositionMVP.xy/LightPositionMVP.w + 1.0) * 0.5;
            #if UNITY_UV_STARTS_AT_TOP
                lcoord.y = 1.0-lcoord.y;
            #endif
            const int Div = (int)_ShadowParams[1];
            float2 D2 = (coord - lcoord) / Div;
            float3 D3 = (FragPos - _LightPosition.xyz) / Div;
            float attr = 1.0 / (Div*0.5);
            for(int i=1; i<Div; ++i) {
                float4 RayPos = mul(UNITY_MATRIX_VP, float4(_LightPosition.xyz + (D3*i), 1.0));
                float RayZ = RayPos.z;
                float4 RayFrag = tex2D(g_position_buffer, lcoord + (D2*i));
                if(RayFrag.w!=0.0 && RayZ > RayFrag.w) {
                    LightAttenuation -= attr;
                }
            }
        }
        if(LightAttenuation<=0.0) { discard; }

        float3 Albedo	= AS.rgb;
        float Shininess	= AS.a;
        float3 Normal	= NS.xyz;
        float Gloss = NS.w;
        float3 EyePos	= _WorldSpaceCameraPos.xyz;
        float3 EyeDir	= normalize(EyePos - FragPos);

        float3 h		= normalize(EyeDir + LightDir);
        float nh		= max(dot(Normal, h), 0.0);
        float Specular	= pow(nh, Shininess);
        float Intensity	= max(dot(Normal, LightDir), 0.0);

        float4 Result	= float4(0.0, 0.0, 0.0, 1.0);
        Result.rgb += LightColor * (Albedo * Intensity) * LightAttenuation;
        Result.rgb += LightColor * _SpecularStrength * Albedo * Specular * LightAttenuation;


        ps_out r = {Result};
        return r;
    }
    ENDCG

    Pass {
        Blend One One
        ZTest Greater
        ZWrite Off
        Cull Front

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0
        #ifdef SHADER_API_OPENGL 
            #pragma glsl
        #endif
        ENDCG
    }
}
}
