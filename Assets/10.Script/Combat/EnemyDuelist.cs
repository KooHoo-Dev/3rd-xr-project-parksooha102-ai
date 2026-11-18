using System;
using System.Collections;
using Game.Table;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game
{
    // 결투하는 적. StageManager 의 방송을 듣고 "자기 표대로" 움직인다.
    //   매니저는 이 적이 몇 초에 쏘는지, 명중률이 얼마인지 모른다. 그건 적 자신이 표(EnemyDef)를 보고 정한다.
    //
    //   방송                 →  하는 일                                    →  Animator 파라미터
    //   RoundStarted(라운드)     이번 라운드 표를 꺼내 둔다. 대기 자세로        IsAiming = false
    //   DrawSignaled()           드로우. 격발 시각(± 0.08초)에 쏜다            IsAiming = true → Fire
    //   RoundEnded(결과)         쏘려던 것을 취소. 플레이어가 이겼으면 쓰러짐    HitPart + Die
    //
    // 쏘면 Fired(맞혔나) 로 알린다. 받는 쪽은 StageManager 다 (적을 만든 쪽이 구독한다).
    //
    // Animator 는 교안(Mini Defence Enemy)과 같은 방식이다 : 파라미터를 바꾸면 컨트롤러의 전이 화살표가 상태를 고른다
    //   파라미터 이름은 EnemyDuel.controller 의 Parameters 와 같아야 한다
    //
    // 사용법
    //   적 프리팹 맨 위(Animator 가 있는 곳)에 붙인다.
    //   적을 만든 쪽이 Instantiate 직후 Init(표) 를 불러 준다.
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
        private EnemyDef enemyDef;   // 내 표 (5라운드 분량)
        private RoundDef roundDef;   // 이번 라운드 줄

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            StageManager.Instance.RoundStarted += HandleRoundStarted;
            StageManager.Instance.DrawSignaled += HandleDrawSignaled;
            StageManager.Instance.RoundEnded += HandleRoundEnded;
        }

        private void OnDisable()
        {
            StageManager.Instance.RoundStarted -= HandleRoundStarted;
            StageManager.Instance.DrawSignaled -= HandleDrawSignaled;
            StageManager.Instance.RoundEnded -= HandleRoundEnded;
        }

        // 나를 만든 쪽이 내 표를 건네준다
        public void Init(EnemyDef def)
        {
            enemyDef = def;
        }

        // ① 대치 시작
        private void HandleRoundStarted(int round)
        {
            roundDef = enemyDef.GetRound(round);
            animator.SetBool(IsAiming, false);   // Draw / Fire → Idle 화살표를 탄다. 1라운드는 이미 Idle 이라 그대로
        }

        // ② 정오 신호 → 드로우, 그리고 내 격발 시각에 쏘기
        private void HandleDrawSignaled()
        {
            animator.SetBool(IsAiming, true);    // Idle → Draw
            float fireTime = roundDef.FireTime + Random.Range(-FireTimeJitter, FireTimeJitter);
            StartCoroutine(FireAfter(fireTime));
        }

        private IEnumerator FireAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            animator.SetTrigger(Fire);           // Draw → Fire
            bool isHit = Random.value < roundDef.HitRate;   // 명중률 0.35 → 0 ~ 1 사이 랜덤이 0.35 보다 작으면 맞힘
            Debug.Log($"[EnemyDuelist] {name} 격발 — " + (isHit ? "명중" : "빗나감"));
            Fired?.Invoke(isHit);
        }

        // ③ 판정 끝
        private void HandleRoundEnded(RoundResult result)
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
