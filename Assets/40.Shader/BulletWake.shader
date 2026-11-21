// 총알 자국 (공기 왜곡). 총알이 지나간 자리의 공기가 밀려나 렌즈처럼 뒤 풍경을 휘게 보이게 한다. (영화 매트릭스의 총알 궤적 느낌)
//   GunEffects 의 LineRenderer 가 이 머티리얼로 그린다.
//
//   어떻게 휘게 보이나?
//     URP 는 불투명한 물체를 다 그린 뒤 화면을 그림 한 장(_CameraOpaqueTexture)으로 복사해 둘 수 있다.
//     이 셰이더는 그 그림을 "자기 자리에서 조금 어긋난 곳" 에서 읽어 와 다시 칠한다 → 뒤 풍경이 휘어 보인다.
//     (돋보기를 대면 바로 뒤가 아니라 옆에 있던 것이 보이는 것과 같다)
//     복사는 비싸서 평소엔 꺼 두고, 자국이 보이는 동안만 GunEffects 가 카메라에 켜 달라고 한다.
//
//   LineRenderer 가 넘겨주는 값
//     uv.x  = 총구에서 몇 m 왔나 (Texture Mode = Tile, GunEffects 가 맞춘다)
//     uv.y  = 줄의 폭 방향 0 ~ 1 (가운데 0.5)
//     color.a = 자국 세기 1 → 0 (GunEffects 가 시간이 지나며 줄인다)
Shader "Game/BulletWake"
{
    Properties
    {
        _Distortion ("왜곡 세기 (화면 비율)", Range(0, 0.1)) = 0.03
        _RingCount ("고리 수 (1m 당)", Float) = 5
        _RingSpeed ("고리가 흐르는 빠르기", Float) = 6
        _RingDepth ("고리 진하기 (0 = 고리 없음)", Range(0, 1)) = 0.6
        _CoreColor ("가운데 심 색 (알파 = 진하기)", Color) = (1, 1, 1, 0.15)
        _CoreWidth ("가운데 심 굵기", Range(0, 0.5)) = 0.06
    }

    SubShader
    {
        // 반투명 차례에 그린다. 화면 복사(_CameraOpaqueTexture)는 불투명을 다 그린 뒤에 만들어지기 때문
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "BulletWake"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha   // 줄 가장자리를 부드럽게 섞는다
            ZWrite Off                        // 반투명이라 깊이는 남기지 않는다
            Cull Off                          // 앞뒷면 모두 그린다

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing   // VR 두 눈을 한 번에 그리기(Single Pass Instanced)에 필요

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"   // SampleSceneColor

            // 메시(LineRenderer)가 꼭짓점마다 넘겨주는 값
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 꼭짓점 → 픽셀로 넘기는 값
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Properties 와 이름을 똑같이 (SRP Batcher 를 쓰려면 UnityPerMaterial 안에 둔다)
            CBUFFER_START(UnityPerMaterial)
                float _Distortion;
                float _RingCount;
                float _RingSpeed;
                float _RingDepth;
                float4 _CoreColor;
                float _CoreWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);   // 왼눈 / 오른눈 중 어느 쪽인지 넘긴다

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);   // 화면 복사를 읽기 전에 어느 눈인지 맞춘다

                // ① 폭 방향 위치 : 가운데 0, 양 끝 -1 / +1
                float across = input.uv.y * 2.0 - 1.0;

                // ② 렌즈 모양 : 가운데와 가장자리는 0, 그 사이가 가장 많이 휜다
                //    across × (1 - across²) → 0 에서 0, ±0.58 에서 가장 크고, ±1 에서 다시 0
                //    가장자리가 0 이라 자국과 바깥 풍경의 경계가 끊겨 보이지 않는다
                float lens = across * (1.0 - across * across);

                // ③ 고리 : 길이 방향으로 sin 물결. 시간이 갈수록 총구 반대쪽으로 흐른다
                //    1 ± _RingDepth 사이를 오르내린다 → 진한 고리, 옅은 고리가 번갈아 생긴다
                float ring = 1.0 + _RingDepth * sin(input.uv.x * _RingCount * 6.2832 - _Time.y * _RingSpeed);

                // ④ 화면에서 "폭 방향" 이 어느 쪽인가
                //    ddx / ddy = 옆 픽셀로 갈 때 uv.y 가 얼마나 바뀌나 → 그 방향이 화면에서 줄을 가로지르는 방향
                //    (1e-6 은 0 으로 나누지 않게 더한 아주 작은 값)
                float2 acrossDir = normalize(float2(ddx(input.uv.y), ddy(input.uv.y)) + 1e-6);

                // ⑤ 자기 자리(screenUV)에서 가운데 쪽으로 당겨진 곳을 읽는다 → 돋보기처럼 부풀어 보인다
                float strength = input.color.a;   // 자국 세기 (GunEffects 가 1 → 0 으로 줄인다)
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 sampleUV = screenUV - acrossDir * lens * ring * _Distortion * strength;
                float3 sceneColor = SampleSceneColor(sampleUV);

                // ⑥ 가운데 아주 얇은 심을 살짝 밝게 (총알이 지나간 선)
                float core = 1.0 - smoothstep(0.0, _CoreWidth, abs(across));
                float3 color = lerp(sceneColor, _CoreColor.rgb, core * _CoreColor.a * strength);

                // ⑦ 줄 양 끝은 투명하게 섞는다 (가장자리 0 → 안쪽 0.3 부터 불투명)
                float alpha = saturate((1.0 - abs(across)) / 0.3);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
