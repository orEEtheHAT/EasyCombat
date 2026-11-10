Shader "EasyCombat/Samples/AttackGallery"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Grid ("Ground grid", Float) = 0
        _VertexColor ("Use line color", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float3 normal:NORMAL; fixed4 color:COLOR; };
            struct Varyings { float4 pos:SV_POSITION; float3 world:TEXCOORD0; fixed4 color:COLOR; };
            fixed4 _Color;
            float _Grid, _VertexColor;
            Varyings vert(Input v)
            {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                float shade = .62 + .38 * saturate(dot(UnityObjectToWorldNormal(v.normal), normalize(float3(-.4,1,-.6))));
                o.color = lerp(_Color * shade, v.color, _VertexColor);
                return o;
            }
            fixed4 frag(Varyings i):SV_Target
            {
                float2 uv = i.world.xz;
                float2 edge = abs(frac(uv + .5) - .5) / max(fwidth(uv), .0001);
                float grid = 1 - saturate(min(edge.x, edge.y));
                return fixed4(i.color.rgb + grid * _Grid * .07, 1);
            }
            ENDCG
        }
    }
}
