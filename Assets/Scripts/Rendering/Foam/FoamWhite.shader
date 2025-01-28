Shader "Fluid/FoamWhite"
{
    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
        }
        //Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On

        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct FoamParticle
            {
                float3 position;
                float3 velocity;
                float lifetime;
                float scale;
            };

            StructuredBuffer<FoamParticle> Particles;
            float scale;
            float debugParam;
            int bubbleClassifyThreshold;
            int sprayClassifyThreshold;
            Texture2D<float4> ColourMap;
            SamplerState linear_clamp_sampler;
            float velocityMax;


            float Remap01(float val, float minVal, float maxVal)
            {
                return saturate((val - minVal) / (maxVal - minVal));
            }

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWorld : TEXCOORD1;
                float3 colour : TEXCOORD2;
            };

            v2f vert(appdata_base v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                FoamParticle particle = Particles[instanceID];

                // Scale particle based on age
                const float remainingLifetimeDissolveStart = 3;
                float dissolveScaleT = saturate(particle.lifetime / remainingLifetimeDissolveStart);
                float speed = length(particle.velocity);
                //float velScale = lerp(0.6, 1, Remap01(speed, 1, 3));
                float vertScale = scale * 2 * dissolveScaleT * particle.scale * 1;

                // Quad face camera
                float3 worldCentre = particle.position;
                float3 vertOffset = v.vertex * vertScale;
                float3 camUp = unity_CameraToWorld._m01_m11_m21;
                float3 camRight = unity_CameraToWorld._m00_m10_m20;
                float3 vertPosWorld = worldCentre + camRight * vertOffset.x + camUp * vertOffset.y;

                float speedT = saturate(speed / velocityMax);
                o.colour = ColourMap.SampleLevel(linear_clamp_sampler, float2(speedT, 0.5), 0);

                o.pos = mul(UNITY_MATRIX_VP, float4(vertPosWorld, 1));
                o.uv = v.texcoord;
                o.posWorld = worldCentre;

                return o;
            }

            float LinearDepthToUnityDepth(float linearDepth)
            {
                float depth01 = (linearDepth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y);
                return (1.0 - (depth01 * _ZBufferParams.y)) / (depth01 * _ZBufferParams.x);
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 centreOffset = (i.uv - 0.5) * 2;
                float sqrDst = dot(centreOffset, centreOffset);
                if (sqrDst > 1) discard;

                float dstFromCam = length(_WorldSpaceCameraPos - i.posWorld);

                return float4(i.colour.rgb, -dstFromCam);
            }
            ENDCG
        }
    }
}