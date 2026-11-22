using System.Collections;
using UnityEngine;

namespace Game
{
    // 한 걸음 전진할 때 발소리. 전진 표식으로 텔레포트하면 부츠 소리 "저벅, 저벅" (+ 박차 "짤랑").
    //   무승부 → 전진 → 다음 라운드로 넘어가는 리듬을 만든다
    //
    //   AdvanceMarker 의 Advanced 를 듣는다. 표식 자기에게 붙이지 않는 이유 :
    //     표식은 도착하는 순간 스스로 꺼진다 (AdvanceMarker.OnTeleporting → Hide).
    //     꺼진 오브젝트의 코루틴 · AudioSource 는 멈춰서 두 번째 걸음이 안 난다
    //   그래서 Main Camera 아래에 둔다 (HurtVoice(Player) 와 같은 자리)
    //   AudioSource : Play On Awake 끔, Spatial Blend 0(2D) — 내 발소리라 방향이 없다
    //
    //   텔레포트는 순간이동이라 실제로 걷지는 않는다. 발소리를 들려줘서 "걸어갔다" 고 느끼게 한다
    [RequireComponent(typeof(AudioSource))]
    public class AdvanceFootsteps : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private AdvanceMarker advanceMarker;

        [Header("클립")]
        [SerializeField] private AudioClip[] stepClips;   // 부츠가 흙을 밟는 소리. 여러 개면 걸음마다 무작위
        [SerializeField] private AudioClip[] spurClips;   // 박차 "짤랑" (비워 두면 없음). 걸음마다 발소리에 겹친다

        [Header("걸음")]
        [SerializeField, Min(1)] private int stepCount = 2;      // 몇 걸음
        [SerializeField] private float stepInterval = 0.55f;     // 걸음 사이 (초). FootstepGravel 원본 녹음의 걸음 간격 (천천히 다가가는 결투 걸음)
        [SerializeField] private float firstDelay = 0f;          // 텔레포트 후 첫 걸음까지 (초)

        [Header("설정")]
        [SerializeField, Range(0f, 1f)] private float stepVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float spurVolume = 0.4f;   // 박차는 발소리 뒤에 살짝
        [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.05f);

        private AudioSource audioSource;
        private AudioClip lastStep;   // 바로 앞 걸음과 다른 소리 (왼발 · 오른발이 똑같이 들리지 않게)

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            SoundManager.Route(audioSource, SoundChannel.Sfx);
        }

        private void OnEnable()
        {
            advanceMarker.Advanced += HandleAdvanced;
        }

        private void OnDisable()
        {
            advanceMarker.Advanced -= HandleAdvanced;
        }

        private void HandleAdvanced()
        {
            if (stepClips == null || stepClips.Length == 0)
            {
                Debug.LogWarning($"[AdvanceFootsteps] {name} 에 발소리 클립이 없습니다. Step Clips 칸에 클립을 꽂아 주세요.");
                return;
            }

            StopAllCoroutines();
            StartCoroutine(StepRoutine());
        }

        // 저벅 (stepInterval) 저벅 ...
        private IEnumerator StepRoutine()
        {
            if (firstDelay > 0f)
                yield return new WaitForSeconds(firstDelay);

            for (int i = 0; i < stepCount; i++)
            {
                if (i > 0)
                    yield return new WaitForSeconds(stepInterval);
                PlayStep();
            }
        }

        private void PlayStep()
        {
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);

            AudioClip step = PickStep();
            if (step != null)
                audioSource.PlayOneShot(step, stepVolume);

            if (spurClips != null && spurClips.Length > 0)
            {
                AudioClip spur = spurClips[Random.Range(0, spurClips.Length)];
                if (spur != null)
                    audioSource.PlayOneShot(spur, spurVolume);
            }
        }

        // 바로 앞 걸음과 다른 클립을 무작위로. 하나뿐이면 그것
        private AudioClip PickStep()
        {
            if (stepClips.Length == 1) return stepClips[0];

            AudioClip picked = stepClips[Random.Range(0, stepClips.Length)];
            for (int i = 0; i < 4 && picked == lastStep; i++)
                picked = stepClips[Random.Range(0, stepClips.Length)];

            lastStep = picked;
            return picked;
        }
    }
}
