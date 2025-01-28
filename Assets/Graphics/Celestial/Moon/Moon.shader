Shader "Unlit/Moon"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Normal ("Normal", 2D) = "white" {}
        _Brightness ("Brightness", Float) = 1
        DetailNormalWeight ("DetailNormal", Float) = 1
    }
    SubShader
    {


        Tags
        {
            "Queue"="Background"
        }
        ZTest Off


        Pass
        {
            //ZClip Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Assets/Scripts/Shader Common/GeoMath.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 objPos : TEXCOORD0;
                float3 worldNormal : NORMAL;
            };

            sampler2D _MainTex;
            sampler2D _Normal;
            float4 _MainTex_TexelSize;
            float4 _Normal_TexelSize;
            float _Brightness;
            float DetailNormalWeight;


            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz;
                o.objPos = v.vertex;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 spherePos = normalize(i.objPos);
                float2 texCoord = pointToUV(spherePos);
                float mipLevelCol = calculateGeoMipLevel(texCoord, _MainTex_TexelSize.zw);
                float mipLevelNormal = calculateGeoMipLevel(texCoord, _Normal_TexelSize.zw);
                float3 detailNormal = tex2Dlod(_Normal, float4(texCoord, 0, mipLevelNormal)).rgb * 2 - 1;

                float3 dirToSun = _WorldSpaceLightPos0;
				
                // return float4(detailNormal * 0.5 + 0.5, 1);
                detailNormal = normalize(mul(unity_ObjectToWorld, float4(detailNormal, 0)).xyz);

                //return float4(detailNormal * 0.5 + 0.5, 1);
                float3 worldNormal = normalize(i.worldNormal + detailNormal * DetailNormalWeight);
               // worldNormal = normalize(detailNormal);
                //return float4(worldNormal, 1);

                float shading = saturate(dot(worldNormal, dirToSun));
                //return float4(i.worldNormal, 1);
                //shading = dot(worldNormal, dirToSun) * 0.5 + 0.5;
                //shading = shading * shading;
                //return shading * shading;
                //float shading = 1;
                shading = pow(shading, 1 / 2.2);

                shading *= _Brightness;
                //shading = 1;

                float4 col = tex2Dlod(_MainTex, float4(texCoord, 0, mipLevelCol)) * shading;

                return float4(col.rgb, 3); // Note: alpha used to control interaction with atmosphere (TODO: figure out better approach?)
            }
            ENDCG

        }

        /*
                
                        float3 spherePos = normalize(i.objPos);
                        float2 texCoord = pointToUV(spherePos);
        
                        float3 dirToSun = _WorldSpaceLightPos0;
                        float3 normal = tex2D(_NormalMap, texCoord) * 2 - 1;
                        //return float4(normal * 0.5 + 0.5, 1);
                        normal = mul(unity_ObjectToWorld, float4(normal, 0)).xyz;
                        //normal = normalize(lerp(normalize(normal), normalize(i.worldNormal), 1-_NormalStrength));
                        float shading = saturate(dot(normal, dirToSun));
                        //float shading = 1;
                        shading = pow(shading, 1/2.2);
                        //float3 viewDir = normalize(i.objPos - _WorldSpaceCameraPos.xyz);
                        //float r = pow(saturate(1-dot(-viewDir, normal)),2);
                        //shading *= (1.4 + r);
        
                        float4 col = tex2D(_MainTex, texCoord) * shading;
                    
                        return col;
        */

    }
    Fallback "VertexLit"
}