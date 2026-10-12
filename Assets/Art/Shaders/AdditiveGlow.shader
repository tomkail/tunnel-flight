// Additive, unlit glow for trails, jet flames and sparks. Vertex colour x _Color x _Intensity,
// with an optional soft falloff from the UVs so plain quads and ribbons don't read as hard rectangles.
Shader "TunnelFlight/Additive Glow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
        [Enum(None, 0, Radial, 1, Across V, 2, Along U, 3)] _Falloff ("Falloff", Float) = 0
        _FalloffPower ("Falloff Power", Float) = 1.5
        [Toggle] _Facing ("Fade Silhouette Edges", Float) = 0
        _FacingPower ("Edge Fade Power", Float) = 1.5
        _FacingMin ("Edge Fade Floor", Range(0, 1)) = 0.35
        _FadeStart ("Distance Fade Start", Float) = 0
        _FadeEnd ("Distance Fade End (0 = off)", Float) = 0
        _ProximityBoost ("Brighten Near Ship", Float) = 0
        [Toggle] _WallReact ("React To Nearest Wall Spot", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test (Always draws over everything, for screen overlays)", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest [_ZTest]
        Cull Off
        // Pull slightly toward the camera so a glow layer can sit on coplanar geometry, like the tunnel grid.
        Offset -1, -1

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_ST;
                half _Intensity;
                half _Falloff;
                half _FalloffPower;
                half _Facing;
                half _FacingPower;
                half _FacingMin;
                float _FadeStart;
                float _FadeEnd;
                half _ProximityBoost;
                half _WallReact;
            CBUFFER_END

            // Set globally by the ship: xyz position; radius and strength of the glow it casts on the walls.
            float4 _TF_ShipPosition;
            float4 _TF_ShipGlow;
            // Set by WallReaction: xyz the wall point nearest the ship, w the spot radius (0 = off);
            // rgb the reaction colour (HDR), a its strength; x ripple frequency (rings per metre), y ripple speed (m/s),
            // z how much of the colour fills the gaps between grid lines.
            float4 _TF_WallSpot;
            float4 _TF_WallSpotColor;
            float4 _TF_WallSpotRipple;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                output.positionWS = positionWS;
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half falloff = 1;
                if (_Falloff > 2.5) falloff = 1 - input.uv.x;
                else if (_Falloff > 1.5) falloff = 1 - abs(input.uv.y * 2 - 1);
                else if (_Falloff > 0.5) falloff = 1 - length(input.uv * 2 - 1);
                falloff = pow(saturate(falloff), _FalloffPower);
                if (_Facing > 0.5)
                {
                    // Soft silhouette: fade where the surface turns edge-on to the camera.
                    half facing = abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                    falloff *= lerp(_FacingMin, 1, pow(facing, _FacingPower));
                }

                half4 color = input.color * _Color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * _MainTex_ST.xy + _MainTex_ST.zw);
                color.rgb *= _Intensity;
                if (_FadeEnd > 0)
                {
                    // Fade with distance so depth and the tunnel's shape ahead read.
                    float d = length(input.viewDirWS);
                    falloff *= saturate((_FadeEnd - d) / max(_FadeEnd - _FadeStart, 0.001));
                }
                if (_ProximityBoost > 0.5 && _TF_ShipGlow.x > 0)
                {
                    // Walls brighten around the ship, so a close wall is obvious.
                    float near = saturate(1 - distance(input.positionWS, _TF_ShipPosition.xyz) / _TF_ShipGlow.x);
                    color.rgb *= 1 + _TF_ShipGlow.y * near * near;
                }
                color.a = saturate(color.a * falloff);
                if (_WallReact > 0.5 && _TF_WallSpot.w > 0 && _TF_WallSpotColor.a > 0)
                {
                    // The wall reacts where the ship is closest: grid lines take the reaction colour and a soft,
                    // rippling glow fills the cells around the spot.
                    float d = distance(input.positionWS, _TF_WallSpot.xyz);
                    float spot = saturate(1 - d / _TF_WallSpot.w);
                    spot *= spot * _TF_WallSpotColor.a;
                    half ripple = 0.65 + 0.35 * sin(6.2831853 * (d * _TF_WallSpotRipple.x - _Time.y * _TF_WallSpotRipple.y * _TF_WallSpotRipple.x));
                    half3 lines = color.rgb * color.a;
                    half3 tinted = lerp(lines, _TF_WallSpotColor.rgb * max(max(lines.r, lines.g), lines.b), spot);
                    half3 fill = _TF_WallSpotColor.rgb * spot * ripple * _TF_WallSpotRipple.z;
                    return half4(tinted + fill, 1);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
