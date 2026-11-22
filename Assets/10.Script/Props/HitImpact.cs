using System.Collections;
using UnityEngine;

namespace Game
{
    // 적이 맞는 소리. 내 총알이 적에게 닿는 순간 "퍽" 하고 난다.
    //   HurtVoice(신음)와 같은 방식으로 StageManager 의 방송(RoundEnded)만 듣는다. PlayerWin 일 때만
    //   적 프리팹에 붙인다 (HurtVoice 와 같은 오브젝트도 괜찮다. AudioSource 는 같이 쓴다)
    //   AudioSource : Play On Awake 끔, Spatial Blend 1(3D), Min Distance 5 (적 총구 · 신음과 같게)
    //
    //   순서 : 총성(내 손) → 퍽(적, 총알이 닿는 순간) → 윽(신음)
    //     총알 자국은 120m/초로 날아간다 (GunEffects.bulletSpeed). 결투 거리 10m 면 약 0.08초 → impactDelay
    //     신음은 퍽 다음에 와야 하니 HurtVoice 의 Delay 를 impactDelay 보다 길게 둔다 (0.2 쯤)
    //   머리 · 몸통 소리를 따로 쓸 수 있다. headClips 를 비워 두면 bodyClips 를 쓴다
    [RequireComponent(typeof(AudioSource))]
    public class HitImpact : MonoBehaviour
    {
        [Header("클립")]
        [SerializeField] private AudioClip[] bodyClips;   // 몸통 : 둔탁한 "퍽"
        [SerializeField] private AudioClip[] headClips;   // 머리 : 짧고 단단한 "딱" (비워 두면 bodyClips)

        [Header("설정")]
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private float impactDelay = 0.08f;                       // 쏜 뒤 퍽까지 (초) = 총알이 날아가는 시간
        [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

        private AudioSource audioSource;
        private AudioClip lastClip;   // 바로 앞에 낸 소리 (연달아 같은 소리가 안 나게)

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

        // 판정 끝 → 내가 적을 맞혔을 때만
        private void HandleRoundEnded(RoundResult result)
        {
            if (result.Outcome != RoundOutcome.PlayerWin) return;

            bool isHead = result.HitPart == HitPart.Head;
            AudioClip clip = PickClip(isHead && HasAny(headClips) ? headClips : bodyClips);
            if (clip == null)
            {
                Debug.LogWarning($"[HitImpact] {name} 에 맞는 소리 클립이 없습니다. Body Clips 칸에 클립을 꽂아 주세요.");
                return;
            }

            StopAllCoroutines();
            StartCoroutine(ImpactRoutine(clip));
        }

        private IEnumerator ImpactRoutine(AudioClip clip)
        {
            if (impactDelay > 0f)
                yield return new WaitForSeconds(impactDelay);

            // pitch 는 AudioSource 하나에 하나뿐이다. HurtVoice 와 같이 쓰면 마지막에 정한 값으로 둘 다 난다
            //   퍽(0.08초)과 윽(0.2초)은 시각이 달라서, 각자 낼 때 정하면 서로 섞이지 않는다
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(clip, volume);
        }

        // 바로 앞과 다른 클립을 무작위로. 하나뿐이면 그것 (HurtVoice 와 같은 방법)
        private AudioClip PickClip(AudioClip[] clips)
        {
            if (HasAny(clips) == false) return null;
            if (clips.Length == 1) return clips[0];

            AudioClip picked = clips[Random.Range(0, clips.Length)];
            for (int i = 0; i < 4 && picked == lastClip; i++)
                picked = clips[Random.Range(0, clips.Length)];

            lastClip = picked;
            return picked;
        }

        private static bool HasAny(AudioClip[] clips)
        {
            return clips != null && clips.Length > 0;
        }
    }
}
