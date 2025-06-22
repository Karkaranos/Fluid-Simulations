Shader "Instanced/Particle2D" {
	Properties {
		
	}
	SubShader {

		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off

		Pass {

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5

			#include "UnityCG.cginc"
			
			StructuredBuffer<float2> Positions2D;
			StructuredBuffer<float2> Velocities;
			StructuredBuffer<float2> DensityData;
			float scale;
			float4 colA;
			Texture2D<float4> ColourMap;
			SamplerState linear_clamp_sampler;
			float velocityMax;
			//float2 CenterOfHighO2;
			//float2 CenterOfLowO2 = float2(-5.0, -5.0);

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 colour : TEXCOORD1;
			};

			v2f vert (appdata_full v, uint instanceID : SV_InstanceID)
			{
				float2 CenterOfHighO2 = float2(-9, -2);
				/*float speed = length(Velocities[instanceID]);
				float speedT = saturate(speed / velocityMax);
				float colT = speedT;*/
				float distFromHigh = length(CenterOfHighO2-(Positions2D[instanceID]));
				//float distFromLow = length((Positions2D[instanceID]) -CenterOfLowO2);
				float colT = /*(distFromHigh > distFromLow ? distFromHigh : distFromLow);*/ distFromHigh;
				colT = saturate(colT / 20);

				
				float3 centreWorld = float3(Positions2D[instanceID], 0);
				float3 worldVertPos = centreWorld + mul(unity_ObjectToWorld, v.vertex * scale);
				float3 objectVertPos = mul(unity_WorldToObject, float4(worldVertPos.xyz, 1));

				v2f o;
				o.uv = v.texcoord;
				o.pos = UnityObjectToClipPos(objectVertPos);
				o.colour = ColourMap.SampleLevel(linear_clamp_sampler, float2(colT, 0.5), 0);

				return o;
			}


			float4 frag (v2f i) : SV_Target
			{
				float2 centreOffset = (i.uv.xy - 0.5) * 2;
				float sqrDst = dot(centreOffset, centreOffset);
				float delta = fwidth(sqrt(sqrDst));
				float alpha = 1 - smoothstep(1 - delta, 1 + delta, sqrDst);

				float3 colour = i.colour;
				return float4(colour, alpha);
			}

			ENDCG
		}
	}
}