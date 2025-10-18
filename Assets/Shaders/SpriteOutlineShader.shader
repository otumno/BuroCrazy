// Название: SpriteOutlineShader (Single Pass, Corrected)
Shader "Sprites/OutlineSinglePass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 1
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 mainColor = tex2D(_MainTex, i.uv) * i.color;

                if (mainColor.a > 0.1)
                {
                    return mainColor;
                }

                float maxNeighborAlpha = 0;
                float2 texelSize = _MainTex_TexelSize.xy * _OutlineWidth;
                
                // --- НАЧАЛО ИСПРАВЛЕНИЯ ---
                maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, i.uv + float2(0, texelSize.y)).a);
                maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, i.uv - float2(0, texelSize.y)).a);
                maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, i.uv + float2(texelSize.x, 0)).a);
                maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, i.uv - float2(texelSize.x, 0)).a);
                maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, i.uv + float2(texelSize.x, texelSize.y)).a);
                maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, i.uv - float2(texelSize.x, texelSize.y)).a);
                maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, i.uv + float2(texelSize.x, -texelSize.y)).a);
                maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, i.uv + float2(-texelSize.x, texelSize.y)).a); // Исправлена логика и убрана лишняя скобка
                // --- КОНЕЦ ИСПРАВЛЕНИЯ ---

                if (maxNeighborAlpha > 0.1)
                {
                    return fixed4(_OutlineColor.rgb, _OutlineColor.a * maxNeighborAlpha);
                }

                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}