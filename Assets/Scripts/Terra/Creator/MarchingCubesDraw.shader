Shader "Custom/MarchingCubesDraw"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		ColAmbient("Ambient", Color) = (1,1,1,1)
		ColFlat("Col Flat", Color) = (1,1,1,1)
		ColFlatDeep("Col FlatDeep", Color) = (1,1,1,1)
		ColSteep("Col Steep", Color) = (1,1,1,1)
		ColSteepDeep("Col SteepDeep", Color) = (1,1,1,1)
		FlatThreshold("Flat Threshold", Range(0,1)) = 0.5
		FlatBlend("Flat Blend", Range(0,0.5)) = 0.1
		shadePow("Shade Pow", Float) = 0.1
		noiseScale("Noise scale", Float) = 1
		noisePersistence("Noise pers", Float) = 1
		noiseLac("Noise lac", Float) = 1
		noiseOffset("Noise offset", Float) = 0
		noiseStr("Noise str", Float) = 1
		HeightShadParams("HeightShadeParams", Vector) = (1, 1, 1, 1)

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
			#include "FractalNoise.hlsl"
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 normal : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
			};
			
			
			struct appdata
			{
				uint vertexID : SV_VertexID;
			};

			struct Vertex {
				float3 position;
				float3 normal;
			};

			StructuredBuffer<Vertex> VertexBuffer;
			float4 col;
			float3 offset;

			float4 ColAmbient;
			float4 ColFlat;
			float4 ColFlatDeep;
			float4 ColSteep;
			float4 ColSteepDeep;
			float FlatThreshold;
			float FlatBlend;
			float shadePow;

			float noiseScale;
			float noisePersistence;
			float noiseLac;
			float noiseStr;
			float noiseOffset;
			float4 HeightShadParams;
			
			v2f vert (appdata v)
			{
				v2f o;
				float3 vertPos = offset + VertexBuffer[v.vertexID].position;
				float3 normal = VertexBuffer[v.vertexID].normal;
				o.vertex = UnityObjectToClipPos(float4(vertPos, 1));
				o.normal = normal;
				o.worldPos = vertPos;
				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				float3 lightDir = _WorldSpaceLightPos0;
				float noise = (continentNoise(i.worldPos * noiseScale, 8, noisePersistence, noiseLac, noiseScale) - noiseOffset) * noiseStr;

				float3 simpleNormal =  normalize(i.normal);
				float3 detailNormal = normalize(i.normal + noise);
				float4 shading = dot(lightDir, simpleNormal) * 0.5 + 0.5;
				shading += ColAmbient;
				shading = pow(shading, shadePow);

				float heightT = (length(i.worldPos) - HeightShadParams.x + noise) / (HeightShadParams.y - HeightShadParams.x);
				heightT = saturate(heightT);
				heightT = (int)(heightT * HeightShadParams.z) / HeightShadParams.z;
				//return heightT;
				//float heightShade = lerp(HeightShadParams.z, HeightShadParams.w, heightT);
				//return noise;
				
				//shading= lerp(0.2f, 1, shading);

				float flatness = dot(normalize(i.worldPos), detailNormal);
				float flatT = smoothstep(-FlatBlend, FlatBlend, flatness - FlatThreshold);

				float3 colFlat = lerp(ColFlatDeep, ColFlat, heightT);
				float3 colSteep = lerp(ColSteepDeep, ColSteep, heightT);
				float3 finalCol = lerp(colFlat, colSteep, 1-flatT) * shading * 1;
				return float4(finalCol, 1);
			}

			ENDCG
		}
	}
}
