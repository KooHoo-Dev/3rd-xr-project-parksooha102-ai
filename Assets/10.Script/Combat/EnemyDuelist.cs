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

        [Header("연결")]
        [SerializeField] private GunEffects gunEffects;   // 적 총의 총구 (SM_Wep_Revolver_01 프리팹의 Muzzle)

        // 총알 궤적이 끝날 자리. 판정은 명중률로 이미 끝났고, 궤적은 그 결과를 "보여 주기만" 한다
        //   적 쪽에서 보면 플레이어 몸은 과녁판 하나다. 머리(눈) 아래로 세로 띠, 좌우로 몸 반 폭
        //   예) 머리 높이 1.6m 면 → 높이 1.5m ~ 0.9m, 몸 중심선에서 좌우 0.15m 안
        [Header("총알 궤적")]
        [SerializeField] private float bodyTop = 0.1f;                         // 머리(눈)에서 이만큼 아래부터 몸 (m)
        [SerializeField] private float bodyBottom = 0.7f;                      // 머리(눈)에서 이만큼 아래까지 몸 (m)
        [SerializeField] private float bodyHalfWidth = 0.15f;                  // 몸 중심선에서 좌우로 (m)
        [SerializeField] private Vector2 missSideOffset = new Vector2(0.35f, 0.7f);   // 빗나감 : 몸 중심선에서 옆으로 이만큼 벗어난 곳을 지나간다 (최소, 최대)
        [SerializeField] private float missPassDistance = 15f;                 // 빗나간 총알이 플레이어를 지나 더 날아가는 거리 (m)

        // 적이 쐈다. true = 플레이어를 맞힘
        public event Action<bool> Fired;

        private Animator animator;
        private RoundDef roundDef;   // 이번 라운드 줄 (BeginRound 가 받아 둔다)
        private Transform target;    // 쏠 곳 = 플레이어 머리 (Draw 가 받아 둔다)

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

            // 쏜 모습 : 맞혔으면 몸 안 아무 데나, 빗나갔으면 몸 옆을 스쳐 뒤로 날아간다
            Vector3 bulletEnd = isHit ? GetHitPoint() : GetMissPoint();
            gunEffects.Play(bulletEnd);

            Fired?.Invoke(isHit);
        }

        // 적 → 플레이어를 바라봤을 때의 "오른쪽" (수평)
        //   과녁판의 가로 방향이다. 위쪽(Vector3.up)이 세로 방향
        private Vector3 GetTargetRight()
        {
            Vector3 toTarget = target.position - gunEffects.transform.position;
            toTarget.y = 0f;   // 높이 차이는 빼고 수평 방향만 본다
            return Vector3.Cross(Vector3.up, toTarget.normalized);   // 위 × 앞 = 오른쪽 (Unity 는 왼손 좌표계)
        }

        // 명중 : 플레이어 몸(과녁판) 안의 무작위 한 점
        private Vector3 GetHitPoint()
        {
            float down = Random.Range(bodyTop, bodyBottom);              // 머리에서 얼마나 아래
            float side = Random.Range(-bodyHalfWidth, bodyHalfWidth);    // 중심선에서 좌우로 얼마나
            return target.position + Vector3.down * down + GetTargetRight() * side;
        }

        // 빗나감 : 몸 바로 옆(왼쪽 또는 오른쪽)을 스치는 점을 지나, 플레이어 뒤로 계속 날아간 곳
        //   궤적이 스치는 점에서 멈추면 "허공에서 사라진 총알" 처럼 보여서, 같은 방향으로 더 보낸다
        private Vector3 GetMissPoint()
        {
            // ① 스치는 점 : 높이는 머리 조금 위 ~ 허리, 옆으로는 몸 폭보다 바깥
            float down = Random.Range(-bodyTop, bodyBottom);   // 음수 = 머리보다 위 (귀 옆을 스치는 느낌)
            float side = Random.Range(missSideOffset.x, missSideOffset.y);
            if (Random.value < 0.5f) side = -side;              // 반반으로 왼쪽 / 오른쪽
            Vector3 passPoint = target.position + Vector3.down * down + GetTargetRight() * side;

            // ② 총구 → 스치는 점 방향으로, 스치는 점보다 missPassDistance 만큼 더 간 곳
            Vector3 muzzle = gunEffects.transform.position;
            Vector3 direction = (passPoint - muzzle).normalized;
            return passPoint + direction * missPassDistance;
        }

        // ---------------------------------------------------------------- API (StageManager 가 부른다)

        // ① 대치 시작 : 이번 라운드 줄을 받아 두고 대기 자세로
        public void BeginRound(RoundDef round)
        {
            roundDef = round;
            animator.SetBool(IsAiming, false);   // Draw / Fire → Idle 화살표를 탄다. 1라운드는 이미 Idle 이라 그대로
        }

        // ② 정오 신호 → 드로우, 그리고 내 격발 시각에 aimTarget(플레이어 머리) 쪽으로 쏘기
        public void Draw(Transform aimTarget)
        {
            target = aimTarget;
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
