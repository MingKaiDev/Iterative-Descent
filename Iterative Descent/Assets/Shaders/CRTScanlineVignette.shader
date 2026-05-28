Shader "Custom/UI/CRTScanlineVignette"
{
    // Apply this shader to a full-panel transparent Image sitting above quiz content.
    // Blend DstColor Zero = Multiply: outputs grayscale that darkens everything beneath it.
    // White areas (1,1,1) = no change. Dark areas = darken the quiz content below.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Scanlines)]
        _ScanlineStrength ("Scanline Darkness", Range(0, 0.6)) = 0.25

        [Header(Vignette)]
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.65
        _VignetteRadius ("Vignette Radius", Range(0.3, 0.8)) = 0.5

        [Header(Noise)]
        _NoiseStrength ("Noise Flicker", Range(0, 0.08)) = 0.03

        // Required Unity UI stencil properties -- do not remove
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        ZTest    [unity_GUIZTestMode]

        // Multiply blend: output * destination = darkens what is drawn below
        Blend DstColor Zero

        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _ClipRect;

            float _ScanlineStrength;
            float _VignetteStrength;
            float _VignetteRadius;
            float _NoiseStrength;

            // Simple hash for per-frame noise -- no texture needed
            float hash(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPos = v.vertex;
                OUT.vertex   = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color    = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // ---- Scanlines ----
                // floor() to integer pixel row, alternate every 2 rows
                float pixelRow  = floor(uv.y * _ScreenParams.y);
                float scanLine  = fmod(pixelRow, 2.0) < 1.0
                                    ? (1.0 - _ScanlineStrength)
                                    : 1.0;

                // ---- Vignette ----
                // Slightly taller oval so corners darken more than sides
                float2 pos     = uv - 0.5;
                float  dist    = length(pos * float2(1.0, 1.3));
                float  vignette = 1.0 - smoothstep(
                                    _VignetteRadius - 0.15,
                                    _VignetteRadius + 0.25,
                                    dist);
                vignette = lerp(1.0, vignette, _VignetteStrength);

                // ---- Subtle per-frame noise (CRT static) ----
                // _Time.y changes every frame, creating random pixel flicker
                float noise = hash(uv * _ScreenParams.xy + _Time.y * 1000.0);
                float noiseEffect = lerp(1.0, noise, _NoiseStrength);

                // ---- Combine ----
                // All values stay <= 1.0 so multiply blend only darkens, never brightens
                float mask = scanLine * vignette * noiseEffect;
                mask = saturate(mask);

                fixed4 col = fixed4(mask, mask, mask, 1.0);

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPos.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
