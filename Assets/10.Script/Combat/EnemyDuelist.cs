using System;
using System.Collections;
using Game.Table;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game
{
    // 결투하는 적. StageManager 가 라운드마다 시키는 대로 움직이고, 쏘면 Fired 로 알린다.
    //   적은 StageManager 를 모른다. 나를 만든 StageManager 가 아래 API 를 직접 부른다 (한 방향)
    //   (교안에서 Spawner 가 만든 Enemy 에게 직접 일을 시키는 것과 같다)
    [RequireComponent(typeof(Animator))]
    public class EnemyDuelist : MonoBehaviour
    {
        private const float FireTimeJitter = 0.08f;   // 기획서 5장 : 격발 시각에 ±0.08초 랜덤

        // Animator 파라미터 (교안처럼 이름을 숫자로 한 번만 바꿔 둔다)
        private static readonly int IsAiming = Animator.StringToHash("IsAiming");
        private static readonly int Fire = Animator.StringToHash("Fire");
        private static readonly int Die = Animator.StringToHash("Die");
        private static readonly int HitPartParam = Animator.StringToHash("HitPart");   // HitPart enum 과 이름이 겹쳐서 Param 을 붙였다

        // 적이 쐈다. true = 플레이어를 맞힘
        public event Action<bool> Fired;

        private Animator animator;
        private RoundDef roundDef;   // 이번 라운드 줄 (BeginRound 가 받아 둔다)

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private IEnumerator FireAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            animator.SetTrigger(Fire);           // Draw → Fire

            // 명중률 0.35 → 0 ~ 1 사이 랜덤이 0.35 이하면 맞힘
            //   < 가 아니라 <= 인 이유 : Random.value 는 1.0 도 나온다 (0 과 1 을 둘 다 포함한다)
            //   < 로 쓰면 명중률 1(100%)인 마지막 라운드도 아주 드물게 빗나가서, 무승부가 나고 StageManager 가 멈춘다
            //   (반대로 명중률 0 인 줄은 0.0 이 나오면 맞는데, 표에 명중률 0 인 줄은 없다)
            bool isHit = Random.value <= roundDef.HitRate;
            Debug.Log($"[EnemyDuelist] {name} 격발 — " + (isHit ? "명중" : "빗나감"));
            Fired?.Invoke(isHit);
        }

        // ---------------------------------------------------------------- API (StageManager 가 부른다)

        // ① 대치 시작 : 이번 라운드 줄을 받아 두고 대기 자세로
        public void BeginRound(RoundDef round)
        {
            roundDef = round;
            animator.SetBool(IsAiming, false);   // Draw / Fire → Idle 화살표를 탄다. 1라운드는 이미 Idle 이라 그대로
        }

        // ② 정오 신호 → 드로우, 그리고 내 격발 시각에 쏘기
        public void Draw()
        {
            animator.SetBool(IsAiming, true);    // Idle → Draw
            float fireTime = roundDef.FireTime + Random.Range(-FireTimeJitter, FireTimeJitter);
            StartCoroutine(FireAfter(fireTime));
        }

        // ③ 판정 끝
        public void EndRound(RoundResult result)
        {
            // 아직 안 쐈으면 취소 (플레이어가 먼저 맞혔거나, 시간 초과)
            StopAllCoroutines();

            if (result.Outcome == RoundOutcome.PlayerWin)
            {
                // 교안 Enemy.cs 의 SetInteger(DeadAnimation) + SetTrigger(Dead) 와 같은 모양
                //   부위 번호를 먼저 넣고 Die 를 켜면, Any State 화살표가 그 부위의 쓰러짐을 고른다
                animator.SetInteger(HitPartParam, (int)result.HitPart);
                animator.SetTrigger(Die);
            }
        }
    }
}
