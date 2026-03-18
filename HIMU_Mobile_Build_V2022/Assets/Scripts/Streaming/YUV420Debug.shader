Shader "Hidden/YUV420Debug"
{
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Width;
            float _Height;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(float4 v : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o; o.pos = UnityObjectToClipPos(v); o.uv = uv; return o;
            }

            float3 YUVtoRGB(float y, float u, float v)
            {
                u -= 0.5;
                v -= 0.5;
                float r = y + 1.402*v;
                float g = y - 0.344*u - 0.714*v;
                float b = y + 1.772*u;
                return float3(r,g,b);
            }

            float frag(v2f i) : SV_Target
            {
                float totalHeight = _Height * 1.5;
                float pixelY = i.uv.y * totalHeight;

                float yVal, uVal, vVal;

                if (pixelY < _Height)
                {
                    // Y plane
                    yVal = tex2D(_MainTex, float2(i.uv.x, i.uv.y * (_Height / totalHeight))).r;
                    uVal = 0.5; // dummy
                    vVal = 0.5;
                }
                else
                {
                    // UV plane (subsampleado)
                    float uvY = (pixelY - _Height)/(_Height*0.5);
                    float uvX = i.uv.x;

                    // U en left half, V en right half
                    float2 uvCoord;
                    if (uvX < 0.5)
                    {
                        uvCoord = float2(uvX*2.0, uvY);
                        uVal = tex2D(_MainTex, uvCoord).r;
                        vVal = 0.5;
                    }
                    else
                    {
                        uvCoord = float2((uvX-0.5)*2.0, uvY);
                        uVal = 0.5;
                        vVal = tex2D(_MainTex, uvCoord).r;
                    }

                    // Para debug, puedes usar solo U o V
                    yVal = 0.5; // dummy
                }

                float3 rgb = YUVtoRGB(yVal,uVal,vVal);
                return float4(rgb,1);
            }

            ENDHLSL
        }
    }
}