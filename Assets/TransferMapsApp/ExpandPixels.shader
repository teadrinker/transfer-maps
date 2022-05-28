Shader "teadrinker/ExpandPixels"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

			#define MISSING_PIXEL_COL float4(1.0, 0.0, 1.0, 0.0)

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            bool sampleAndCheck(float2 uv, int x, int y, out float4 col) {
				col = tex2D(_MainTex, uv + _MainTex_TexelSize.xy * float2(x, y));
				return abs(dot(col - MISSING_PIXEL_COL, 1.0)) > 0.00001;
			}

            fixed4 frag (v2f i) : SV_Target
            {
				float4 col;

				if(sampleAndCheck(i.uv,  0, 0, col)) return col;

				if(sampleAndCheck(i.uv, -1,  0, col)) return col;
				if(sampleAndCheck(i.uv,  1,  0, col)) return col;
				if(sampleAndCheck(i.uv,  0, -1, col)) return col;
				if(sampleAndCheck(i.uv,  0,  1, col)) return col;

				if(sampleAndCheck(i.uv, -1, -1, col)) return col;
				if(sampleAndCheck(i.uv,  1, -1, col)) return col;
				if(sampleAndCheck(i.uv, -1,  1, col)) return col;
				if(sampleAndCheck(i.uv,  1,  1, col)) return col;

				if(sampleAndCheck(i.uv, -2,  0, col)) return col;
				if(sampleAndCheck(i.uv,  2,  0, col)) return col;
				if(sampleAndCheck(i.uv,  0, -2, col)) return col;
				if(sampleAndCheck(i.uv,  0,  2, col)) return col;
/*
				if(sampleAndCheck(i.uv, -1, -2, col)) return col;
				if(sampleAndCheck(i.uv,  1, -2, col)) return col;

				if(sampleAndCheck(i.uv, -1,  2, col)) return col;
				if(sampleAndCheck(i.uv,  1,  2, col)) return col;
				
				if(sampleAndCheck(i.uv, -2, -1, col)) return col;
				if(sampleAndCheck(i.uv, -2,  1, col)) return col;

				if(sampleAndCheck(i.uv,  2, -1, col)) return col;
				if(sampleAndCheck(i.uv,  2,  1, col)) return col;
*/
                return MISSING_PIXEL_COL;
            }
            ENDCG
        }
    }
}
