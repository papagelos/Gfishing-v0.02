Shader "Hidden/GF/SeparableBlur"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float2 _BlurDir;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                float2 d = _BlurDir;

                fixed4 c = tex2D(_MainTex, uv) * 0.227027;
                c += tex2D(_MainTex, uv + d * 1.384615) * 0.316216;
                c += tex2D(_MainTex, uv - d * 1.384615) * 0.316216;
                c += tex2D(_MainTex, uv + d * 3.230769) * 0.070270;
                c += tex2D(_MainTex, uv - d * 3.230769) * 0.070270;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
