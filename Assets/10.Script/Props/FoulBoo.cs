using System.Collections;
using UnityEngine;

namespace Game
{
    // 비겁자 야유 (기획서 4.5). 신호 전에 총을 뽑아 반칙패가 나면 마을 사람들이 야유를 퍼붓는다.
    //   NoonBell · DuelSound 와 같은 방식으로 StageManager 의 방송(RoundEnded)만 듣는다
    //   AudioSource : AudioClip = Boo, Play On Awake 끔, Spatial Blend 0(2D)
    //   (반칙 순간 DuelSound 가 BGM 을 끈다 → 정적 위로 야유가 터진다)
    [RequireComponent(typeof(AudioSource))]
    public class FoulBoo : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float delay = 0.3f;   // 반칙 판정 후 야유가 터지기까지 (초). 0 이면 바로

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

        // 판정 끝 → 반칙일 때만 야유
        private void HandleRoundEnded(RoundResult result)
        {
            if (result.Outcome != RoundOutcome.Foul) return;

            StartCoroutine(BooRoutine());
        }

        private IEnumerator BooRoutine()
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            audioSource.Play();
        }
    }
}
