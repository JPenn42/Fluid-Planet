Shader "Hidden/Atmosphere"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            static const float PI = 3.14159265359;
            static const float TAU = PI * 2;
            static const float maxFloat = 3.402823466e+38;

            // Remap a value from one range to another
            float remap(float v, float minOld, float maxOld, float minNew, float maxNew)
            {
                return saturate(minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld));
            }

            // Remap the components of a vector from one range to another
            float4 remap(float4 v, float minOld, float maxOld, float minNew, float maxNew)
            {
                return saturate(minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld)); //
            }

            // Remap a float value (with a known mininum and maximum) to a value between 0 and 1
            float remap01(float v, float minOld, float maxOld)
            {
                return saturate((v - minOld) / (maxOld - minOld));
            }

            // Remap a float2 value (with a known mininum and maximum) to a value between 0 and 1
            float2 remap01(float2 v, float2 minOld, float2 maxOld)
            {
                return saturate((v - minOld) / (maxOld - minOld));
            }

            // Smooth minimum of two values, controlled by smoothing factor k
            // When k = 0, this behaves identically to min(a, b)
            float smoothMin(float a, float b, float k)
            {
                k = max(0, k);
                // https://www.iquilezles.org/www/articles/smin/smin.htm
                float h = max(0, min(1, (b - a + k) / (2 * k)));
                return a * h + b * (1 - h) - k * h * (1 - h);
            }

            // Smooth maximum of two values, controlled by smoothing factor k
            // When k = 0, this behaves identically to max(a, b)
            float smoothMax(float a, float b, float k)
            {
                k = min(0, -k);
                float h = max(0, min(1, (b - a + k) / (2 * k)));
                return a * h + b * (1 - h) - k * h * (1 - h);
            }

            float Blend(float startHeight, float blendDst, float height)
            {
                return smoothstep(startHeight - blendDst / 2, startHeight + blendDst / 2, height);
            }


            // Returns vector (dstToSphere, dstThroughSphere)
            // If ray origin is inside sphere, dstToSphere = 0
            // If ray misses sphere, dstToSphere = maxValue; dstThroughSphere = 0
            float2 raySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir)
            {
                float3 offset = rayOrigin - sphereCentre;
                float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
                float b = 2 * dot(offset, rayDir);
                float c = dot(offset, offset) - sphereRadius * sphereRadius;
                float d = b * b - 4 * a * c; // Discriminant from quadratic formula

                // Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
                if (d > 0)
                {
                    float s = sqrt(d);
                    float dstToSphereNear = max(0, (-b - s) / (2 * a));
                    float dstToSphereFar = (-b + s) / (2 * a);

                    // Ignore intersections that occur behind the ray
                    if (dstToSphereFar >= 0)
                    {
                        return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
                    }
                }
                // Ray did not intersect sphere
                return float2(maxFloat, 0);
            }


            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                // Camera space matches OpenGL convention where cam forward is -z. In unity forward is positive z.
                // (https://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html)
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv.xy * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0));
                return output;
            }

            float2 squareUV(float2 uv)
            {
                float width = _ScreenParams.x;
                float height = _ScreenParams.y;
                //float minDim = min(width, height);
                float scale = 1000;
                float x = uv.x * width;
                float y = uv.y * height;
                return float2(x / scale, y / scale);
            }


            sampler2D _BlueNoise;
            sampler2D _MainTex;
            sampler2D _BakedOpticalDepth;
            sampler2D _CameraDepthTexture;
            float4 params;

            float3 dirToSun;

            float3 planetCentre;
            float atmosphereRadius;
            float oceanRadius;
            float planetRadius;

            // Paramaters
            int numInScatteringPoints;
            int numOpticalDepthPoints;
            float intensity;
            float4 scatteringCoefficients;
            float ditherStrength;
            float ditherScale;
            float densityFalloff;


            float densityAtPoint(float3 densitySamplePoint)
            {
                float heightAboveSurface = length(densitySamplePoint - planetCentre) - planetRadius;
                float height01 = heightAboveSurface / (atmosphereRadius - planetRadius);
                float localDensity = exp(-height01 * densityFalloff) * (1 - height01);
                return localDensity;
            }

            float opticalDepth(float3 rayOrigin, float3 rayDir, float rayLength)
            {
                float3 densitySamplePoint = rayOrigin;
                float stepSize = rayLength / (numOpticalDepthPoints - 1);
                float opticalDepth = 0;

                for (int i = 0; i < numOpticalDepthPoints; i++)
                {
                    float localDensity = densityAtPoint(densitySamplePoint);
                    opticalDepth += localDensity * stepSize;
                    densitySamplePoint += rayDir * stepSize;
                }
                return opticalDepth;
            }

            float opticalDepthBaked(float3 rayOrigin, float3 rayDir)
            {
                float height = length(rayOrigin - planetCentre) - planetRadius;
                float height01 = saturate(height / (atmosphereRadius - planetRadius));

                float uvX = 1 - (dot(normalize(rayOrigin - planetCentre), rayDir) * .5 + .5);
                return tex2Dlod(_BakedOpticalDepth, float4(uvX, height01, 0, 0));
            }

            float opticalDepthBaked2(float3 rayOrigin, float3 rayDir, float rayLength)
            {
                float3 endPoint = rayOrigin + rayDir * rayLength;
                float d = dot(rayDir, normalize(rayOrigin - planetCentre));
                float opticalDepth = 0;

                const float blendStrength = 1.5;
                float w = saturate(d * blendStrength + .5);

                float d1 = opticalDepthBaked(rayOrigin, rayDir) - opticalDepthBaked(endPoint, rayDir);
                float d2 = opticalDepthBaked(endPoint, -rayDir) - opticalDepthBaked(rayOrigin, -rayDir);

                opticalDepth = lerp(d2, d1, w);
                return opticalDepth;
            }

            float3 calculateLight(float3 rayOrigin, float3 rayDir, float rayLength, float3 originalCol, float2 uv)
            {
                float blueNoise = tex2Dlod(_BlueNoise, float4(squareUV(uv) * ditherScale, 0, 0));
                blueNoise = (blueNoise - 0.5) * ditherStrength;

                float3 inScatterPoint = rayOrigin;
                float stepSize = rayLength / (numInScatteringPoints - 1);
                float3 inScatteredLight = 0;
                float viewRayOpticalDepth = 0;
                float3 transmittance = 1;

                for (int i = 0; i < numInScatteringPoints; i++)
                {
                    float sunRayLength = raySphere(planetCentre, atmosphereRadius, inScatterPoint, dirToSun).y;
                    float sunRayOpticalDepth = opticalDepthBaked(inScatterPoint + dirToSun * ditherStrength, dirToSun);
                    float localDensity = densityAtPoint(inScatterPoint);
                    viewRayOpticalDepth = opticalDepthBaked2(rayOrigin, rayDir, stepSize * i);
                    transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth) * scatteringCoefficients);

                    inScatteredLight += localDensity * transmittance;
                    inScatterPoint += rayDir * stepSize;
                }
                inScatteredLight *= scatteringCoefficients * intensity * stepSize / planetRadius;
                inScatteredLight += blueNoise * 0.01;

              
             
                float3 reflectedLight = originalCol * transmittance;

                float3 finalCol = reflectedLight + inScatteredLight;

               // return originalCol * transmittance;
                return finalCol;
            }


            float4 frag(v2f i) : SV_Target
            {
                
                float4 originalCol = tex2D(_MainTex, i.uv);
                //return -originalCol.a;
                float sceneDepthNonLinear = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float sceneDepth = LinearEyeDepth(sceneDepthNonLinear) * length(i.viewVector);
                //return sceneDepth / 100;

                float fluidDst = originalCol.a < 0 ? -originalCol.a : 999999;
                sceneDepth = min(sceneDepth, fluidDst);

                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.viewVector);

                float dstToOcean = raySphere(planetCentre, oceanRadius, rayOrigin, rayDir);
                float dstToSurface = min(sceneDepth, dstToOcean);

                float2 hitInfo = raySphere(planetCentre, atmosphereRadius, rayOrigin, rayDir);
                float dstToAtmosphere = hitInfo.x;
                float dstThroughAtmosphere = min(hitInfo.y, dstToSurface - dstToAtmosphere);

                if (dstThroughAtmosphere > 0)
                {
                    const float epsilon = 0.0001;
                    float3 pointInAtmosphere = rayOrigin + rayDir * (dstToAtmosphere + epsilon);
                    float3 light = calculateLight(pointInAtmosphere, rayDir, dstThroughAtmosphere - epsilon * 2, originalCol, i.uv);
                    return float4(light, 1);
                }
                return originalCol;
            }
            ENDCG
        }
    }
}