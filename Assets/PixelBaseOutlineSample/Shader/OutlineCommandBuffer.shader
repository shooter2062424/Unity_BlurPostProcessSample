Shader "Hidden/OutlineCommandBuffer"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		//commad buffer used.
		Pass
		{
			Name "Mask"
			Cull Off ZWrite Off ZTest Always
			Blend SrcAlpha OneMinusSrcAlpha
			Lighting Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag			
			#include "UnityCG.cginc"

			sampler2D _MainTex,_MaskTex;

			fixed4 _MainTex_ST, _OutlineColor;
			fixed2 _MainTex_TexelSize;
			fixed _Intensity = 1;

			struct v2f
			{
				fixed2 uv : TEXCOORD0;
				fixed4 vertex : SV_POSITION;
			};
			
			v2f vert (appdata_base v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
	
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed c = tex2D(_MainTex, i.uv).r;
				fixed m = tex2D(_MaskTex, i.uv).r;
				clip(-m);

				return  _OutlineColor * _Intensity * c;
			}
			ENDCG
		}


		Pass
		{
			Name "Mask"
			Cull Off ZWrite Off ZTest Always
			Blend SrcAlpha OneMinusSrcAlpha
			Lighting Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag			
			#include "UnityCG.cginc"

			sampler2D _MainTex,_MaskTex;

			fixed4 _MainTex_ST, _OutlineColor;
			fixed2 _MainTex_TexelSize;
			fixed _Intensity = 1;

			struct v2f
			{
				fixed2 uv : TEXCOORD0;
				fixed4 vertex : SV_POSITION;
				fixed2 map : TEXCOORD1;

			};
			
			v2f vert (appdata_base v)
			{
				v2f o;
				o.vertex = v.vertex;
				o.uv = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
				#if UNITY_UV_STARTS_AT_TOP
					//if (_MainTex_TexelSize.y < 0)
						o.uv.y = 1-o.uv.y;
				#endif

				o.map.x = tex2Dlod(_MainTex, fixed4(o.uv.x,o.uv.y,0,0)).r;
				o.map.y = tex2Dlod(_MaskTex, fixed4(o.uv.x,o.uv.y,0,0)).r;

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				clip(-i.map.y);
				return  _OutlineColor * _Intensity * i.map.x;
			}
			ENDCG
		}

	}
	Fallback Off
}
