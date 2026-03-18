Shader "Hidden/RGBToYUV420"
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
            float2 _TexelSize;
            float _Height;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(float4 v : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v);
                o.uv = uv;
                return o;
            }

            float RGBtoY(float3 rgb)
            {
                return dot(rgb, float3(0.299, 0.587, 0.114));
            }

            float2 RGBtoUV(float3 rgb)
            {
                float u = dot(rgb, float3(-0.169, -0.331, 0.5)) + 0.5;
                float v = dot(rgb, float3(0.5, -0.419, -0.081)) + 0.5;
                return float2(u, v);
            }

            float frag(v2f i) : SV_Target
            {
                float yPixel = i.uv.y * (_Height * 1.5);

                // -------- Y plane --------
                if (yPixel < _Height)
                {
                    float3 rgb = tex2D(_MainTex, i.uv).rgb;
                    return RGBtoY(rgb);
                }

                // -------- UV PLANE --------
                float2 uv = i.uv;
                
                // reescalar UV area 
                uv.y = (yPixel - _Height) / (_Height * 0.5);

                float2 texel = _TexelSize * 2;

                float3 c0 = tex2D(_MainTex, uv).rgb;
                float3 c1 = tex2D(_MainTex, uv + float2(texel.x, 0)).rgb;
                float3 c2 = tex2D(_MainTex, uv + float2(0, texel.y)).rgb;
                float3 c3 = tex2D(_MainTex, uv + texel).rgb;
                float2 uvAvg = (RGBtoUV(c0) + RGBtoUV(c1) + RGBtoUV(c2) + RGBtoUV(c3)) * 0.25;
                
                // izquierda = U, derecha = V
                if (uv.x < 0.5)
                    return uvAvg.x;
                else 
                    return uvAvg.y;
            }

            ENDHLSL
        }
    }
}