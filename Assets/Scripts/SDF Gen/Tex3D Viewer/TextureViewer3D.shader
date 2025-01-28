Shader "Example/TextureViewer3D"
{
    Properties {}
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            Texture3D<float4> DisplayTexture;
            SamplerState samplerDisplayTexture;
            float sliceDepth;
            float2 remapRange;

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


            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float remap(float val, float minOld, float maxOld, float minNew, float maxNew)
            {
                float t = (val - minOld) / (maxOld - minOld);
                return minNew + (maxNew - minNew) * t;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 uv3 = float3(i.uv.xy, sliceDepth);
                float rawVal = DisplayTexture.SampleLevel(samplerDisplayTexture, uv3, 0).r;
                //float val = remap(rawVal, remapRange.x, remapRange.y, 0, 1);
                float val = rawVal / remapRange.y;

                return val < 0 ? float4(-val, 0, 0, 0) : val;
            }
            ENDCG
        }
    }
}