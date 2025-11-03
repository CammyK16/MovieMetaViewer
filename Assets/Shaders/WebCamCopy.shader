// Assets/Shaders/WebCamCopy.shader
Shader "Hidden/WebCamCopy"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            // 0 or 1
            float _FlipY;
            float _FlipX;
            // 0,1,2,3 (quarters)
            float _Rotate90;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float2 rotate90(float2 uv, float steps)
            {
                // rotate around centre (0.5,0.5)
                uv -= 0.5;
                if (steps < 0.5)      uv = uv;
                else if (steps < 1.5) uv = float2(-uv.y,  uv.x);
                else if (steps < 2.5) uv = float2(-uv.x, -uv.y);
                else                  uv = float2( uv.y, -uv.x);
                return uv + 0.5;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                // mirror
                if (_FlipX > 0.5) uv.x = 1.0 - uv.x;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                // rotate
                uv = rotate90(uv, _Rotate90);
                o.uv = uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
