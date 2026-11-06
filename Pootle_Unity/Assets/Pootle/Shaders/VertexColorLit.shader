Shader "Pootle/Vertex Color Lit"
{
    Properties { _BaseColor ("Vertex tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Vertex Color Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                half4 color:COLOR;
                half fog:TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);
                o.color=input.color*_BaseColor; o.fog=ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half3 normal=normalize(input.normalWS);
                Light main=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lighting=SampleSH(normal)+main.color*saturate(dot(normal,main.direction))*main.shadowAttenuation;
                #ifdef _ADDITIONAL_LIGHTS
                uint count=GetAdditionalLightsCount();
                for(uint i=0;i<count;i++)
                {
                    Light light=GetAdditionalLight(i,input.positionWS);
                    lighting+=light.color*saturate(dot(normal,light.direction))*light.distanceAttenuation*light.shadowAttenuation;
                }
                #endif
                return half4(MixFog(input.color.rgb*lighting,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
