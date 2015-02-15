﻿Shader "BatchRenderer/FixedBillboard Alpha Blended" {
Properties {
    _MainTex ("Texture", 2D) = "white" {}
}

Category {
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Blend SrcAlpha OneMinusSrcAlpha
    AlphaTest Greater .01
    ColorMask RGB
    Cull Off Lighting Off ZWrite Off ZTest Always 

    SubShader {
        Pass {
CGPROGRAM
#if defined(SHADER_API_OPENGL)
    #pragma glsl
#elif defined(SHADER_API_D3D9)
    #define WITHOUT_INSTANCE_COLOR
    #pragma target 3.0
#endif
    #define WITHOUT_INSTANCE_COLOR
    #define WITHOUT_INSTANCE_EMISSION
#pragma vertex vert_fixed
#pragma fragment frag

#include "UnityCG.cginc"
#include "BatchRenderer.cginc"
#include "Billboard.cginc"
ENDCG
        }
    }
}
}
