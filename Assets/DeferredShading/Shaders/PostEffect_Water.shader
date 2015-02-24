﻿Shader "DeferredShading/PostEffect/Water" {
Properties {
}
SubShader {
    Tags { "RenderType"="Opaque" }
    Blend Off
    ZTest Less
    ZWrite Off
    Cull Back

CGINCLUDE
#include "Compat.cginc"
#include "DSBuffers.cginc"
#include "noise.cginc"

float g_speed;
float g_refraction;
float g_reflection_intensity;
float g_fresnel;
float g_raymarch_step;
float g_attenuation_by_distance;

struct ia_out
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};

struct vs_out
{
    float4 vertex : SV_POSITION;
    float4 screen_pos : TEXCOORD0;
    float4 world_pos : TEXCOORD1;
    float3 normal : TEXCOORD2;
    float4 tangent : TEXCOORD3;
    float3 binormal : TEXCOORD4;
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
    o.world_pos = mul(_Object2World, v.vertex);
    o.normal = normalize(mul(_Object2World, float4(v.normal.xyz, 0.0)).xyz);
    o.tangent = float4(normalize(mul(_Object2World, float4(v.tangent.xyz,0.0)).xyz), v.tangent.w);
    o.binormal = normalize(cross(o.normal, o.tangent) * v.tangent.w);
    return o;
}




float compute_octave(float3 pos, float scale)
{
    float time = _Time.y*g_speed;
    float o1 = sea_octave(pos.xzy*1.25*scale + float3(1.0,2.0,-1.5)*time*1.25 + sin(pos.xzy+time*8.3)*0.15, 4.0);
    float o2 = sea_octave(pos.xzy*2.50*scale + float3(2.0,-1.0,1.0)*time*-2.0 - sin(pos.xzy+time*6.3)*0.2, 8.0);
    return o1 * o2;
}

float3 guess_normal(float3 p, float scale)
{
    const float d = 0.02;
    float o = 1.0-(compute_octave(p, scale)*0.5+0.5);
    return normalize( float3(
        compute_octave(p+float3(  d,0.0,0.0), scale)-compute_octave(p+float3( -d,0.0,0.0), scale),
        compute_octave(p+float3(0.0,0.0,  d), scale)-compute_octave(p+float3(0.0,0.0, -d), scale),
        0.02*o ));
}

float jitter(float3 p)
{
    float v = dot(p,1.0)+_Time.y;
    return frac(sin(v)*43758.5453);
}

ps_out frag(vs_out i)
{
    float2 coord = (i.screen_pos.xy / i.screen_pos.w + 1.0) * 0.5;
    #if UNITY_UV_STARTS_AT_TOP
        coord.y = 1.0-coord.y;
    #endif

    float4 pos = SamplePosition(coord);
    float d = 0.0;
    if(d!=0.0) {
        d = length(pos.xyz - i.world_pos.xyz);
    }
    else {
        float2 offsets[8] = {
            float2( 0.02, 0.00), float2(-0.02,  0.00),
            float2( 0.00, 0.02), float2( 0.00, -0.02),
            float2( 0.01, 0.01), float2(-0.01,  0.01),
            float2( 0.01,-0.01), float2(-0.01, -0.01),
        };
        for(int oi=0; oi<8; ++oi) {
            float4 p = pos = SamplePosition(coord+offsets[oi]);
            if(pos.w!=0.0) {
                d = max(d, length(p.xyz - i.world_pos.xyz));
                break;
            }
        }
    }

    float3 n = guess_normal(i.world_pos.xyz, 1.0);
    float3x3 tbn = float3x3( i.tangent.xyz, i.binormal, i.normal.xyz);
    n = normalize(mul(n, tbn));


    float pd = length(i.world_pos.xyz - _WorldSpaceCameraPos.xyz);
    float fade = max(1.0-pd*0.05, 0.0);

    float3 cam_dir = normalize(i.world_pos - _WorldSpaceCameraPos);

    ps_out r;
    {
        float3 eye = normalize(_WorldSpaceCameraPos.xyz-i.world_pos.xyz);
        float2 tcoord = 0.0;

        int MaxMarch = 16;
        float adv = g_raymarch_step * jitter(i.world_pos.xyz);
        float3 refdir = normalize(-eye + -reflect(-eye, n.xyz)*g_refraction);
        float4 reffragpos = 0.0;
        for(int k=0; k<MaxMarch; ++k) {
            adv = adv + g_raymarch_step;
            float4 tpos = mul(UNITY_MATRIX_VP, float4((i.world_pos+refdir*adv), 1.0) );
            tcoord = (tpos.xy / tpos.w + 1.0) * 0.5;
            #if UNITY_UV_STARTS_AT_TOP
                tcoord.y = 1.0-tcoord.y;
            #endif
            reffragpos = SamplePosition(tcoord);
            if(reffragpos.w!=0 && reffragpos.w<tpos.z) {
                break;
            }
        }

        float f1 = max(1.0-abs(dot(n, eye))-0.5, 0.0)*2.0;
        float f2 = 1.0-abs(dot(i.normal, eye));

        if(reffragpos.y<0.0 || reffragpos.w==0.0) {
            r.color = SampleFrame(tcoord);
        }
        else {
            r.color = SampleFrame(coord);
        }
        r.color *= 0.9;
        r.color = r.color * max(1.0-adv*g_attenuation_by_distance, 0.0);
        r.color += (f1 * f2) * g_fresnel * fade;
    }
    {
        float _RayMarchDistance = 1.0;
        float3 ref_dir = reflect(cam_dir, normalize(i.normal.xyz+n.xyz*0.2));
        float4 tpos = mul(UNITY_MATRIX_VP, float4(i.world_pos.xyz + ref_dir*_RayMarchDistance, 1.0) );
        float2 tcoord = (tpos.xy / tpos.w + 1.0) * 0.5;
        #if UNITY_UV_STARTS_AT_TOP
            tcoord.y = 1.0-tcoord.y;
        #endif
        r.color.xyz += tex2D(g_frame_buffer, tcoord).xyz * g_reflection_intensity * fade;
    }
    //r.color.rgb = pow(n*0.5+0.5, 4.0);
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
