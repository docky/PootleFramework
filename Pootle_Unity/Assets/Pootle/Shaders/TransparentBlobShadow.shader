Shader "Pootle/Transparent Blob Shadow"
{
 Properties { _MainTex("Shadow mask",2D)="white" {} _Opacity("Opacity",Range(0,1))=.45 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   ZTest LEqual
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float _Opacity;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0;};
   Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
   half4 frag(Varyings i):SV_Target {return half4(0,0,0,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a*_Opacity);}
   ENDHLSL
  }
 }
}
