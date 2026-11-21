using UnityEngine;

namespace Game
{
    // 홀스터 자리의 은은한 빛. 무승부 뒤 "총을 여기 꽂아라" 를 글자 대신 보여 준다.
    //   빛 = 홀스터를 감싼 반투명 구(Unlit 덧칠 재질). 켜져 있는 동안 밝아졌다 어두워졌다 한다
    //   RoundBoard 가 무승부일 때 켜고, 총이 꽂히면 끈다
    //   만들기 : Town 의 RoundBoard ⋮ → 쪽지 모양으로 정렬 (홀스터 칸 아래에 HolsterGlow 오브젝트를 만든다)
    public class HolsterGlow : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Renderer glowRenderer;   // 빛 구의 Mesh Renderer

        [Header("설정")]
        [SerializeField] private Color color = new Color(1f, 0.72f, 0.3f, 1f);   // 등불 같은 주황빛
        [SerializeField] private float minAlpha = 0.05f;   // 가장 어두울 때
        [SerializeField] private float maxAlpha = 0.35f;   // 가장 밝을 때
        [SerializeField] private float pulseSpeed = 4f;    // 깜빡이는 빠르기 (클수록 빠르다)

        // 재질은 여럿이 같이 쓰므로 직접 바꾸지 않고, 이 Renderer 에만 색을 덧씌운다
        //   MaterialPropertyBlock : "이 Renderer 에서만 이 칸 값을 이걸로" 라는 쪽지
        private MaterialPropertyBlock block;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            glowRenderer.enabled = false;   // 무승부가 나야 켠다
        }

        private void Update()
        {
            if (glowRenderer.enabled == false) return;

            // Sin 은 -1 ~ 1 을 오르내린다 → 0 ~ 1 로 바꿔서 투명도를 min ~ max 사이로 흔든다
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            Color current = color;
            current.a = Mathf.Lerp(minAlpha, maxAlpha, pulse);
            block.SetColor("_BaseColor", current);   // URP Unlit 의 색 칸
            glowRenderer.SetPropertyBlock(block);
        }

        // ---------------------------------------------------------------- API

        public void Show()
        {
            glowRenderer.enabled = true;
        }

        public void Hide()
        {
            glowRenderer.enabled = false;
        }
    }
}
