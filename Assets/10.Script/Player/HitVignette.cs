using System;
using System.Collections;
using UnityEngine;

namespace Game
{
    // 피격 비네팅 (기획서 5장 · 9장 : 총에 맞아도 시점을 쓰러뜨리지 않고 화면을 붉게 페이드).
    //   Main Camera 의 자식 Quad 에 붙인다. 눈앞에 붙은 반투명 판의 "가장자리만 붉게" 칠해 두고, 맞으면 잠깐 진하게 했다가 흐려진다.
    //   포스트 프로세싱(Volume 의 Vignette)을 쓰지 않는 이유 : Quest 용 Mobile 렌더러는 포스트 프로세싱이 꺼져 있고, 켜면 매 프레임 화면 전체를 한 번 더 그려서 무겁다.
    //
    //   오브젝트 구성
    //     Main Camera
    //       HitVignette  ← Quad (Mesh Collider 는 지운다) + 이 스크립트
    //                      Material : Universal Render Pipeline/Unlit, Surface Type = Transparent
    [RequireComponent(typeof(MeshRenderer))]
    public class HitVignette : MonoBehaviour
    {
        // URP Unlit 셰이더의 칸 이름 (Animator 파라미터처럼 숫자로 한 번만 바꿔 둔다)
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");       // Base Map (그림)
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");   // Base Map 옆 색 (그림에 곱해진다)

        private const int TextureSize = 128;   // 코드로 만드는 비네팅 그림의 크기 (128 x 128 칸)

        [Header("모양")]
        [SerializeField] private Color color = new Color(0.7f, 0f, 0f);   // 붉은색 (알파는 아래 maxAlpha 가 정한다)
        [SerializeField] private float clearRadius = 0.35f;               // 가운데 투명하게 남길 크기. 0 = 다 붉게, 1 = 판 가장자리까지 투명

        [Header("깜빡임")]
        [SerializeField] private float maxAlpha = 0.8f;      // 가장 진할 때 (0 ~ 1)
        [SerializeField] private float fadeInTime = 0.1f;    // 진해지는 시간 (초). 적 총알 궤적이 날아오는 시간과 비슷하게
        [SerializeField] private float holdTime = 0.4f;      // 진한 채로 머무는 시간 (초)
        [SerializeField] private float fadeOutTime = 1.5f;   // 흐려지는 시간 (초)

        private MeshRenderer meshRenderer;
        private Material material;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();

            // Quad 를 만들면 Mesh Collider 가 같이 붙는다. 눈앞에 콜라이더가 있으면 총알 레이 · UI 레이가 여기에 막힌다
            if (TryGetComponent(out Collider attachedCollider))
                throw new Exception($"[HitVignette] {name} 에 {attachedCollider.GetType().Name} 가 붙어 있습니다. 눈앞의 판이 레이를 막으니 Inspector 에서 지워 주세요.");

            // .material = 이 판만 쓰는 복사본. 알파를 바꿔도 Material 에셋 파일은 그대로다
            material = meshRenderer.material;
            if (material.HasProperty(BaseMap) == false)
                throw new Exception($"[HitVignette] {name} 의 Material 셰이더({material.shader.name})에 Base Map 칸이 없습니다. Shader 를 Universal Render Pipeline/Unlit 으로 바꿔 주세요.");

            material.SetTexture(BaseMap, CreateVignetteTexture());
            SetAlpha(0f);   // 처음에는 안 보인다
        }

        private void OnEnable()
        {
            StageManager.Instance.RoundEnded += HandleRoundEnded;
        }

        private void OnDisable()
        {
            StageManager.Instance.RoundEnded -= HandleRoundEnded;
        }

        // 판정 끝 → 적이 나를 맞혔을 때만 붉게
        private void HandleRoundEnded(RoundResult result)
        {
            if (result.Outcome != RoundOutcome.EnemyWin) return;

            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }

        // 진해짐 → 머묾 → 흐려짐
        private IEnumerator FlashRoutine()
        {
            // ① 0 → maxAlpha
            float timer = 0f;
            while (timer < fadeInTime)
            {
                timer += Time.deltaTime;
                SetAlpha(Mathf.Lerp(0f, maxAlpha, timer / fadeInTime));
                yield return null;
            }

            // ② 진한 채로 잠깐
            SetAlpha(maxAlpha);
            yield return new WaitForSeconds(holdTime);

            // ③ maxAlpha → 0
            timer = 0f;
            while (timer < fadeOutTime)
            {
                timer += Time.deltaTime;
                SetAlpha(Mathf.Lerp(maxAlpha, 0f, timer / fadeOutTime));
                yield return null;
            }
            SetAlpha(0f);
        }

        // 판 전체의 진하기를 alpha 로 (그림의 알파 × 이 알파 = 실제로 보이는 진하기)
        //   0 이면 아예 그리지 않는다. 투명한 판도 그리면 눈앞 화면 전체를 한 번 더 칠해서 Quest 에서 아깝다
        private void SetAlpha(float alpha)
        {
            Color tinted = color;
            tinted.a = alpha;
            material.SetColor(BaseColor, tinted);
            meshRenderer.enabled = alpha > 0f;
        }

        // 가운데는 투명하고 바깥으로 갈수록 불투명한 흰 그림을 만든다 (색은 BaseColor 가 입힌다)
        //   d = 가운데에서 떨어진 거리. 가운데 0, 판 변의 한가운데 1, 모서리 약 1.41
        //   예) clearRadius 0.35 → d 가 0.35 까지는 투명, 1 에서 완전히 불투명, 그 바깥(모서리)도 불투명
        private Texture2D CreateVignetteTexture()
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;   // 가장자리 바깥을 반대편 그림으로 채우지 않는다

            float half = TextureSize / 2f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    // ① 이 칸이 가운데에서 얼마나 떨어졌나 (0 ~ 1.41)
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // ② clearRadius ~ 1 사이를 부드럽게 0 → 1 (SmoothStep 은 양 끝이 완만한 Lerp)
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearRadius, 1f, d));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();   // SetPixel 로 바꾼 칸을 그래픽 카드로 보낸다
            return texture;
        }
    }
}
