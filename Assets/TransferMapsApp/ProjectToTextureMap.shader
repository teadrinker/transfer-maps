Shader "teadrinker/ProjectToTextureMap"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
		Cull Off

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
				float2 uv2 : TEXCOORD1;   // _UseSourceUV
			};

			struct v2f
			{
				float3 wPos : TEXCOORD1;
				float2 uv : TEXCOORD0;     // _UseSourceUV
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;

			float4x4 _ToCameraSpace;
			float4 _FovParams;
			float4 _UDIMOffset;
			float _UseSourceUV;

			v2f vert (appdata v)
			{
				v2f o;

				o.wPos = v.vertex;  // obj format dont support transforms
				//o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				float2 uv = v.uv;
				uv -= _UDIMOffset.xy;
				o.vertex = float4(uv.x * 2.0 - 1.0, -(uv.y * 2.0 - 1.0), 1.0, 1.0);

				o.uv = v.uv2;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				if(_UseSourceUV > 0.5) // should be shader variant really, but doesn't matter, pixel transfer and png encoding/decoding will be the real bottlenecks anyway... 
					return tex2D(_MainTex, i.uv);

				float3 camSpacePos = mul(_ToCameraSpace, float4(i.wPos, 1.0));
				float2 uvProj = camSpacePos.xy * _FovParams.xy * 0.5 / camSpacePos.z + 0.5;
				return tex2D(_MainTex, uvProj);		

				//return float4(1.0, 0.5, 0.0, 1.0);
			}
			ENDCG
		}
	}
}
