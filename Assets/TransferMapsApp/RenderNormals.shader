Shader "teadrinker/RenderNormals"
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
				float3 normal : NORMAL;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				float3 viewDir = normalize(ObjSpaceViewDir(v.vertex));
				float3 up = mul((float3x3)unity_WorldToObject, float3(0,1,0)); // needs normalize if non uniform scale?
				float3 left = normalize(cross(up, viewDir));
				up = normalize(cross(viewDir, left));
				o.normal = mul(float3x3(left, up, viewDir), v.normal);

				return o;
			}

			fixed4 frag(v2f i) : COLOR
			{
				return float4(normalize(i.normal), 1.0);
			}

            ENDCG
        }
    }
}
