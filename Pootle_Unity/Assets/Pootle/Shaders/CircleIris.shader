Shader "Pootle/Circle Iris"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Centre ("Centre", Vector) = (.5,.5,0,0)
        _Radius ("Radius", Float) = 2
        _Aspect ("Aspect", Float) = 1.7778
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _Centre;
            float _Radius, _Aspect;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p=(i.uv-_Centre.xy)*float2(_Aspect,1);
                float d=length(p);
                float edge=max(fwidth(d),.001);
                return fixed4(0,0,0,smoothstep(_Radius-edge,_Radius+edge,d));
            }
            ENDCG
        }
    }
}
