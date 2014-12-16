﻿Shader "MassParticle/ParticleGBufferCS" {

Properties {
	_BaseColor ("BaseColor", Vector) = (0.15, 0.15, 0.2, 5.0)
	_GlowColor ("GlowColor", Vector) = (0.0, 0.0, 0.0, 0.0)
	_HeatColor ("HeatColor", Vector) = (0.25, 0.05, 0.025, 0.0)
	_HeatThreshold ("HeatThreshold", Float) = 2.5
	_HeatIntensity ("HeatIntensity", Float) = 1.0
	_Scale ("Scale", Float) = 1.0
	_FadeTime ("FadeTime", Float) = 0.1
}
SubShader {
	Tags { "RenderType"="Opaque" }

	CGINCLUDE
	#include "Compat.cginc"
	#include "UnityCG.cginc"
	#include "ParticleDataType.cginc"

	float4 _BaseColor;
	float4 _GlowColor;
	float4 _HeatColor;
	float _HeatThreshold;
	float _HeatIntensity;
	float _Scale;
	float _FadeTime;

	struct Vertex
	{
		float3 position;
		float3 normal;
	};
	StructuredBuffer<Particle> particles;
	StructuredBuffer<Vertex> vertices;

	struct ia_out {
		uint vertexID : SV_VertexID;
		uint instanceID : SV_InstanceID;
	};

	struct vs_out {
		float4 vertex : SV_POSITION;
		float4 screen_pos : TEXCOORD0;
		float4 position : TEXCOORD1;
		float4 normal : TEXCOORD2;
		float4 emission : TEXCOORD3;
	};

	struct ps_out
	{
		float4 normal : COLOR0;
		float4 position : COLOR1;
		float4 color : COLOR2;
		float4 glow : COLOR3;
	};

	vs_out vert(ia_out io)
	{
		float lifetime = particles[io.instanceID].lifetime;
		float scale = _Scale * min(lifetime/_FadeTime, 1.0);

		float3 ipos = particles[io.instanceID].position;
		float4 v = float4(vertices[io.vertexID].position*scale+ipos, 1.0);
		float4 n = float4(vertices[io.vertexID].normal, 0.0);
		float4 vp = mul(UNITY_MATRIX_VP, v);

		vs_out o;
		o.vertex = vp;
		o.screen_pos = vp;
		o.position = v;
		o.normal.xyz = normalize(n.xyz);
		o.normal.w = 1.0;

		float speed = particles[io.instanceID].speed;
		float heat = max(speed-_HeatThreshold, 0.0) * _HeatIntensity;
		o.emission = _GlowColor + _HeatColor * heat;
		o.emission.w = lifetime;
		return o;
	}

	ps_out frag(vs_out vo)
	{
		if(vo.emission.w==0.0) {
			discard;
		}
		ps_out o;
		o.normal = vo.normal;
		o.position = float4(vo.position.xyz, vo.screen_pos.z);
		o.color = _BaseColor;
		o.glow = vo.emission;
		return o;
	}

	ENDCG

	Pass {
		Cull Back
		ZWrite On
		ZTest LEqual

		CGPROGRAM
		#pragma target 5.0
		#pragma vertex vert
		#pragma fragment frag 
		ENDCG
	}
	Pass {
		Name "DepthPrePass"
		ColorMask 0
		ZWrite On
		ZTest Less

		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 5.0
		#ifdef SHADER_API_OPENGL 
			#pragma glsl
		#endif
		ENDCG
	}
	Pass {
		Name "Shading"
		Cull Back
		ZWrite Off
		ZTest Equal

		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 5.0
		#ifdef SHADER_API_OPENGL 
			#pragma glsl
		#endif
		ENDCG
	}
}
Fallback Off

}
