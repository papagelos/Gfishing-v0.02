Shader "GFishing/SpriteShadowSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Color ("Tint (Alpha = strength)", Color) = (0,0,0,0.2)

        // NEW: edge controls
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.02
        _Softness ("Edge Softness", Range(0,0.25)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Cutoff;
            float _Softness;

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float a = tex2D(_MainTex, IN.texcoord).a;

                // Feather the silhouette edge (tiny "blur")
                float soft = max(_Softness, 1e-5);
                a = smoothstep(_Cutoff - soft, _Cutoff + soft, a);

                a *= _Color.a;                 // strength driven by your script
                return fixed4(0,0,0,a);        // pure black silhouette
            }
            ENDCG
        }
    }
}
