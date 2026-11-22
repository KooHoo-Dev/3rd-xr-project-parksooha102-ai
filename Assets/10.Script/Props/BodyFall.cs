using System.Collections;
using UnityEngine;

namespace Game
{
    // 적이 쓰러지는 소리. 내가 적을 맞히면 쓰러지는 애니메이션에 맞춰 두 번 "쿵" 한다.
    //   HitImpact · HurtVoice 와 같은 방식으로 StageManager 의 방송(RoundEnded)만 듣는다. PlayerWin 일 때만
    //   적 프리팹에 붙인다. AudioSource : Play On Awake 끔, Spatial Blend 1(3D), Min Distance 5 (HitImpact 와 같게)
    //
    //   Enemy_Death_Head · Enemy_Death_Body 애니메이션 (둘 다 1.5초, 몸 중심 높이 RootT.y 로 본 순간)
    //     0.00 ~ 0.30초 : 맞고 휘청 (1.0m → 0.94m)
    //     0.30 ~ 0.65초 : 무릎이 꺾인다 (→ 0.62m)  ← 0.65초 무릎이 땅에 닿는다 : kneeDelay
    //     0.65 ~ 0.95초 : 무릎 꿇고 버틴다
    //     0.95 ~ 1.50초 : 앞으로 엎어진다 (→ 0.2m)  ← 1.5초 상체가 땅에 닿는다 : fallDelay
    //   Die 를 켜는 것은 판정과 같은 프레임 (StageManager.EndRound → EnemyDuelist.EndRound) 이라 판정 시각부터 센다
    //   애니메이션을 바꾸면 이 두 값도 바꾼다
    [RequireComponent(typeof(AudioSource))]
    public class BodyFall : MonoBehaviour
    {
        [Header("클립 (비워 두면 그 소리 없음)")]
        [SerializeField] private AudioClip[] kneeClips;   // 무릎이 땅에 닿는 "툭" (흙 · 천 쓸리는 짧은 소리)
        [SerializeField] private AudioClip[] fallClips;   // 몸이 엎어지는 "쿵" (무겁고 둔탁한 소리 + 흙먼지)

        [Header("타이밍 (판정부터 초)")]
        [SerializeField] private float kneeDelay = 0.65f;
        [SerializeField] private float fallDelay = 1.5f;

        [Header("설정")]
        [SerializeField, Range(0f, 1f)] private float kneeVolume = 0.6f;   // 무릎은 작게. 마지막 쿵이 크게 들려야 끝난 느낌이 난다
        [SerializeField, Range(0f, 1f)] private float fallVolume = 1f;
        [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.05f);

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            SoundManager.Route(audioSource, SoundChannel.Sfx);
        }

        private void OnEnable()
        {
            StageManager.Instance.RoundEnded += HandleRoundEnded;
        }

        private void OnDisable()
        {
            StageManager.Instance.RoundEnded -= HandleRoundEnded;
        }

        // 판정 끝 → 내가 적을 맞혔을 때만 (적이 쓰러질 때만)
        private void HandleRoundEnded(RoundResult result)
        {
            if (result.Outcome != RoundOutcome.PlayerWin) return;

            if (HasAny(kneeClips) == false && HasAny(fallClips) == false)
            {
                Debug.LogWarning($"[BodyFall] {name} 에 쓰러지는 소리 클립이 없습니다. Fall Clips 칸에 클립을 꽂아 주세요.");
                return;
            }

            StopAllCoroutines();
            StartCoroutine(FallRoutine());
        }

        // 판정 → (kneeDelay) 툭 → (fallDelay) 쿵
        private IEnumerator FallRoutine()
        {
            float startTime = Time.time;

            yield return new WaitForSeconds(kneeDelay);
            Play(kneeClips, kneeVolume);

            // 무릎 소리를 내는 데 걸린 시간까지 빼서, 판정부터 정확히 fallDelay 에 맞춘다
            float remaining = fallDelay - (Time.time - startTime);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
            Play(fallClips, fallVolume);
        }

        private void Play(AudioClip[] clips, float volume)
        {
            if (HasAny(clips) == false) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(clip, volume);
        }

        private static bool HasAny(AudioClip[] clips)
        {
            return clips != null && clips.Length > 0;
        }
    }
}
