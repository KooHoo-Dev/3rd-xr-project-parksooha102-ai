using UnityEngine;

namespace Game
{
    // 승리 · 패배 효과음. 결투가 끝나고 결과판이 뜨는 순간 짧게 한 번 울린다.
    //   ResultBoard 와 같은 방송(DuelEnded)을 듣는다 → 결과판이 뜨는 것과 같은 프레임에 난다
    //   AudioSource : Play On Awake 끔, Spatial Blend 0(2D)
    //
    //   소리 흐름 (DuelEnded 는 판정 2초 뒤 = StageManager.resultDelay)
    //     승리 : 총성 → 퍽 → 윽 → 툭(0.65) → 쿵(1.5) → 승리 효과음(2.0) + 결과판
    //     패배 : 적 총성 → 내 신음 · 붉은 화면 → 패배 효과음(2.0) + 결과판
    //     반칙 : 야유(0.3초부터 약 4초)가 이미 패배를 알린다 → foulClip 은 비워 두는 게 기본 (겹치면 시끄럽다)
    //   머리를 맞혀 이겼으면 headshotWinClip 을 쓸 수 있다 (비워 두면 winClip)
    [RequireComponent(typeof(AudioSource))]
    public class DuelSting : MonoBehaviour
    {
        [Header("클립 (비워 두면 그 결과엔 소리 없음)")]
        [SerializeField] private AudioClip winClip;
        [SerializeField] private AudioClip headshotWinClip;   // 머리로 이겼을 때 (비워 두면 winClip)
        [SerializeField] private AudioClip loseClip;
        [SerializeField] private AudioClip foulClip;          // 반칙패 (기본 비움 : 야유와 겹친다)

        [Header("설정")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            SoundManager.Route(audioSource, SoundChannel.Bgm);   // 짧은 음악이라 배경음악 갈래
        }

        private void OnEnable()
        {
            StageManager.Instance.DuelEnded += HandleDuelEnded;
        }

        private void OnDisable()
        {
            StageManager.Instance.DuelEnded -= HandleDuelEnded;
        }

        private void HandleDuelEnded(DuelResult result)
        {
            AudioClip clip = GetClip(result.LastRound);
            if (clip == null) return;

            audioSource.PlayOneShot(clip, volume);
        }

        // 승부가 난 라운드 → 낼 소리 (무승부는 DuelEnded 로 오지 않는다)
        private AudioClip GetClip(RoundResult round)
        {
            switch (round.Outcome)
            {
                case RoundOutcome.PlayerWin:
                    if (round.HitPart == HitPart.Head && headshotWinClip != null)
                        return headshotWinClip;
                    return winClip;
                case RoundOutcome.EnemyWin:
                    return loseClip;
                case RoundOutcome.Foul:
                    return foulClip;
                default:
                    return null;
            }
        }
    }
}
