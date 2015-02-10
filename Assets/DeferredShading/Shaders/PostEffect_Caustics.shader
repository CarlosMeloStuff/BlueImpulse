﻿Shader "Custom/PostEffect_Caustics" {
Properties {
}
SubShader {
    Tags { "RenderType"="Opaque" }
    Blend One One
    ZTest Greater
    ZWrite Off
    Cull Front

CGINCLUDE
#include "Compat.cginc"
#include "DSBuffers.cginc"
#include "noise.cginc"



struct ia_out
{
    float4 vertex : POSITION;
};

struct vs_out
{
    float4 vertex : SV_POSITION;
    float4 screen_pos : TEXCOORD1;
};

struct ps_out
{
    float4 color : COLOR0;
};


vs_out vert(ia_out v)
{
    float4 spos = mul(UNITY_MATRIX_MVP, v.vertex);
    vs_out o;
    o.vertex = spos;
    o.screen_pos = spos;
    return o;
}

ps_out frag(vs_out i)
{
    float2 coord = (i.screen_pos.xy / i.screen_pos.w + 1.0) * 0.5;
    #if UNITY_UV_STARTS_AT_TOP
        coord.y = 1.0-coord.y;
    #endif

    float4 pos = SamplePosition(coord);
    if(pos.w==0.0) discard;

    float o1 = sea_octave(pos.xzy*1.25 + float3(1.0,2.0,-1.5)*_Time.y*1.5 + sin(pos.xzy+_Time.y*8.3)*0.15, 4.0);
    float o2 = sea_octave(pos.xzy*2.50 + float3(2.0,-1.0,1.0)*_Time.y*-2.5 - sin(pos.xzy+_Time.y*6.3)*0.2, 8.0);
    o1 = (o1*0.5+0.5 -0.2) * 1.2;
    o1 *= (o2*0.5+0.5);
    o1 = pow(o1, 5.0);

    ps_out r;
    r.color = o1*float4(0.5, 0.5, 1.5, 1.0) * 1.0;
    return r;
}
ENDCG

    Pass {
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
