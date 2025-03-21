Shader "Custom/ColorMatrixShader" 
{
    Properties 
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [Toggle] _UseDefaultShape ("Use Default Shape", Float) = 1
        _CircleRadius ("Circle Radius", Range(0,1)) = 0.5
    }
    SubShader 
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100

        Pass 
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f 
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _UseDefaultShape;
            float _CircleRadius;
            
            // Check if a texture is empty/null or white
            bool IsDefaultTexture(sampler2D tex, float2 uv) {
                fixed4 c = tex2D(tex, uv);
                // Check if it's approximately white (default texture)
                return dot(c.rgb, float3(1,1,1)) > 2.9;
            }
            
            // Generate a circle shape
            fixed4 Circle(float2 uv, float radius, fixed4 color) {
                float dist = distance(uv, float2(0.5, 0.5));
                float circle = step(dist, radius);
                return fixed4(color.rgb, color.a * circle);
            }

            v2f vert (appdata v) 
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target 
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                
                // If we should use default shape and texture is white/default
                if (_UseDefaultShape > 0.5 && IsDefaultTexture(_MainTex, i.uv)) {
                    // Generate a circle with the given color
                    return Circle(i.uv, _CircleRadius, _Color);
                }
                else {
                    // Use texture * color
                    fixed4 finalColor = texColor * _Color;
                    return finalColor;
                }
            }
            ENDCG
        }
    }
}
