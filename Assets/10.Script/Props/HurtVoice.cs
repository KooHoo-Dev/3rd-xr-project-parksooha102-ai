using System.Collections;
using UnityEngine;

namespace Game
{
    // 신음 소리. 총에 맞은 쪽이 "윽" 하고 신음한다.
    //   NoonBell · FoulBoo 와 같은 방식으로 StageManager 의 방송(RoundEnded)만 듣는다
    //   who 로 누구의 목소리인지 고른다
    //     Player : 적이 나를 맞혔을 때 (EnemyWin). Main Camera 아래에 둔다
    //              AudioSource : Play On Awake 끔, Spatial Blend 0(2D) — 내 목에서 나는 소리라 방향이 없다
    //     Enemy  : 내가 적을 맞혔을 때 (PlayerWin). 적 프리팹의 머리 근처에 둔다
    //              AudioSource : Play On Awake 끔, Spatial Blend 1(3D) — 10m 앞에서 들려야 "맞혔다" 가 실감난다
    //
    //   같은 신음이 매번 똑같이 들리면 녹음 티가 난다. 그래서
    //     ① 클립을 여러 개 꽂아 두면 바로 앞과 다른 것을 무작위로 고른다
    //     ② 피치를 조금씩 흔든다
    //   머리를 맞히면 짧게 끊기는 소리(headClips)를 따로 쓸 수 있다. 비워 두면 bodyClips 를 쓴다
    [RequireComponent(typeof(AudioSource))]
    public class HurtVoice : MonoBehaviour
    {
        public enum Who
        {
            Player,   // 적이 나를 맞혔을 때 신음
            Enemy,    // 내가 적을 맞혔을 때 신음
        }

        [Header("누구의 목소리")]
        [SerializeField] private Who who = Who.Player;

        [Header("클립")]
        [SerializeField] private AudioClip[] bodyClips;   // 몸통에 맞았을 때 (Player 는 늘 이것)
        [SerializeField] private AudioClip[] headClips;   // 머리에 맞았을 때 (Enemy 만. 비워 두면 bodyClips)

        [Header("설정")]
        [SerializeField] private float delay = 0.05f;                            // 총성 직후 신음까지 (초). 총성과 겹치면 묻힌다
        [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);   // 피치 흔들기 (최소, 최대)

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

        // 판정 끝 → 내가 맞은 쪽일 때만 신음
        private void HandleRoundEnded(RoundResult result)
        {
            RoundOutcome hurtOutcome = who == Who.Player ? RoundOutcome.EnemyWin : RoundOutcome.PlayerWin;
            if (result.Outcome != hurtOutcome) return;

            // 머리 부위는 플레이어가 적을 맞혔을 때만 의미가 있다 (RoundResult.HitPart 설명 참고)
            bool isHead = who == Who.Enemy && result.HitPart == HitPart.Head;
            AudioClip[] clips = isHead && HasAny(headClips) ? headClips : bodyClips;

            AudioClip clip = PickClip(clips);
            if (clip == null)
            {
                Debug.LogWarning($"[HurtVoice] {name} 에 신음 클립이 없습니다. Body Clips 칸에 클립을 꽂아 주세요.");
                return;
            }

            StopAllCoroutines();
            StartCoroutine(VoiceRoutine(clip));
        }

        private IEnumerator VoiceRoutine(AudioClip clip)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(clip);
        }

        // 바로 앞과 다른 클립을 무작위로. 하나뿐이면 그것
        private AudioClip PickClip(AudioClip[] clips)
        {
            if (HasAny(clips) == false) return null;
            if (clips.Length == 1) return clips[0];

            // 몇 번 뽑아 보고 그래도 같으면 그냥 쓴다 (같은 클립을 두 칸에 꽂아 둬도 멈추지 않게)
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
