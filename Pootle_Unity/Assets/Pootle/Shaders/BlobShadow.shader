Shader "Pootle/Blob Shadow"
{
 Properties { _MainTex("Body mask",2D)="white" {} _Opacity("Opacity",Range(0,1))=.6 _UseReceiverPlane("Limit to support plane",Float)=0 _ReceiverPlane("Support plane",Vector)=(0,1,0,0) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   ZTest Always
   Cull Front
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float _Opacity;
   float _UseReceiverPlane;
   float4 _ReceiverPlane;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;};
   struct Varyings {float4 positionCS:SV_POSITION;};
   Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);return o;}
   half4 frag(Varyings i):SV_Target
   {
    float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
    float depth=SampleSceneDepth(screenUV);
    #if UNITY_REVERSED_Z
    clip(depth-0.000001);
    #else
    clip(.999999-depth);
    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
    #endif
    float3 world=ComputeWorldSpacePosition(screenUV,depth,UNITY_MATRIX_I_VP);
    float3 local=TransformWorldToObject(world);
    clip(.5-abs(local));
    // Character receivers extend uphill, while the avatar above the support
    // plane is excluded. Props retain the ordinary volume-only projection.
    if(_UseReceiverPlane>.5) clip(-dot(float4(world,1),_ReceiverPlane));
    // Reject near-vertical faces; no stretched shadow down the side of a ledge.
    float3 n=cross(ddy(world),ddx(world));
    float up=abs(n.y)*rsqrt(max(dot(n,n),1e-12));
    float facing=smoothstep(.12,.3,up);
    float fade=smoothstep(-.5,-.35,local.y);
    return half4(0,0,0,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,local.xz+.5).a*_Opacity*facing*fade);
   }
   ENDHLSL
  }
 }
}
