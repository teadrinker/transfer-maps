Shader "teadrinker/RenderParticles"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        ZTest Off
        ZWrite Off      
        //Blend SrcAlpha OneMinusSrcAlpha
        Blend One One
        Cull Off
        LOD 100
 
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
 
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
 
            sampler2D _MainTex;
            float4 _MainTex_ST; 

            v2f vert (appdata v)
            {
                v2f o; 
                //o.vertex = UnityObjectToClipPos(v.vertex);
                //o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                //float scale = 512;
                //o.vertex = float4(v.vertex.xy + v.color.zw * 1, -1., -1.) * scale;
                //o.vertex = float4(float2(0., 0.) + v.color.zw * 0.02, 1., 1.) * scale;

                o.vertex = float4(v.vertex.xy + v.color.zw * 0.04, 0.5, 1);

                o.uv = v.color.zw;
                o.color = tex2Dlod(_MainTex, float4(v.color.xy, 0, 0));
                //o.color = float4(0, 1, 0, 1);
                return o;
            } 

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = i.color;
                //col.a *= 0.8 * max(0, 1.0 - sqrt(dot(i.uv, i.uv)));
                col *= 0.03 * max(0, 1.0 - sqrt(dot(i.uv, i.uv)));
                return col;
            }
            ENDCG
        }
    }
}
