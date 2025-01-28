Shader "Fluid/PlanetInstancedUnlit" {
	Properties {
		
	}
	SubShader {

		Tags {"Queue"="Geometry" }
		//Cull Off
		ZTest Always

		Pass {

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5
			

			#include "UnityCG.cginc"

			        struct CelestialBody
        {
            float3 pos;
            float3 vel;
            float mass;
            float radius;
            float4 col;
        };
			
			StructuredBuffer<CelestialBody> Bodies;
			Texture2D<float4> ColourMap;
			SamplerState linear_clamp_sampler;
			float velocityMax;
			const float radiusOffset;

			float scale;
			float3 colour;

			float4x4 localToWorld;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 colour : TEXCOORD1;
				float3 normal : NORMAL;
			};

			float3 dirToSun;
			float shadingPow;

			v2f vert (appdata_full v, uint instanceID : SV_InstanceID)
			{
				v2f o;
				o.uv = v.texcoord;
				o.normal = mul(unity_ObjectToWorld, float4(v.normal.xyz, 0));
				
				float3 centreWorld = Bodies[instanceID].pos;
				float3 objectVertPos = v.vertex * (Bodies[instanceID].radius + radiusOffset);
				float4 viewPos = mul(UNITY_MATRIX_V, float4(centreWorld, 1)) + float4(objectVertPos, 0);
				//o.pos = mul(UNITY_MATRIX_P, viewPos);
				o.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(objectVertPos + centreWorld, 1)));


				//float speed = length(Velocities[instanceID]);
				//float speedT = saturate(speed / velocityMax);
				//float colT = speedT;
				o.colour = Bodies[instanceID].col;

				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				//return float4(i.normal * 0.5 + 0.5, 1);
				//if (length(i.uv-0.5) * 2 > 1) discard;
				
				float shading = pow(saturate(dot(dirToSun, i.normal)), shadingPow);
				//shading = (shading + 0.6) / 1.4;
				return float4(i.colour * shading, 1);
			}

			ENDCG
		}
	}
}