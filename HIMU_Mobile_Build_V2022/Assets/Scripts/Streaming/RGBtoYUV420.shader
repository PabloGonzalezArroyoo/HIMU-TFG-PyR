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
            float _Width;

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

            float RGBtoU(float3 rgb)
            {
                return dot(rgb, float3(-0.169, -0.331, 0.5)) + 0.5;
            }

            float RGBtoV(float3 rgb)
            {
                return dot(rgb, float3(0.5, -0.419, -0.081)) + 0.5;
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

                // -------- U/V plane --------
                float2 uv = i.uv;

                // Calculamos coordenadas dentro del plano U/V
                float uvPlaneY = (yPixel - _Height) / (_Height * 0.25); // porque cada plano tiene height/4
                float uvPlaneX = uv.x * _Width;

                // Calculamos el pixel de 2x2 para downsampling
                int px = (int)uvPlaneX * 2;
                int py = (int)uvPlaneY * 2;

                float3 c0 = tex2D(_MainTex, (float2(px, py) + 0.5) * _TexelSize).rgb;
                float3 c1 = tex2D(_MainTex, (float2(px+1, py) + 0.5) * _TexelSize).rgb;
                float3 c2 = tex2D(_MainTex, (float2(px, py+1) + 0.5) * _TexelSize).rgb;
                float3 c3 = tex2D(_MainTex, (float2(px+1, py+1) + 0.5) * _TexelSize).rgb;

                float uAvg = (RGBtoU(c0) + RGBtoU(c1) + RGBtoU(c2) + RGBtoU(c3)) * 0.25;
                float vAvg = (RGBtoV(c0) + RGBtoV(c1) + RGBtoV(c2) + RGBtoV(c3)) * 0.25;

                // Definimos offset para que U y V estén en planas separadas
                if (yPixel < _Height + _Height/4)
                    return uAvg; // primer cuarto del área UV -> U
                else
                    return vAvg; // segundo cuarto -> V
            }

            ENDHLSL
        }
    }
}