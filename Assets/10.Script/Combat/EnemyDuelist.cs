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

        // 빗나간 총알이 귀 옆을 스치는 "휙". 총알 자국이 스치는 점을 지나는 순간에 그 자리에서 난다 (3D)
        //   여러 개 꽂으면 무작위로 고른다. 비워 두면 소리 없음
        [Header("스치는 총알 소리")]
        [SerializeField] private AudioClip[] whizClips;
        [SerializeField, Range(0f, 1f)] private float whizVolume = 1f;
        [SerializeField] private Vector2 whizPitchRange = new Vector2(0.9f, 1.1f);
        // 스치기 이만큼 전에 틀기 시작 (초) = 클립에서 가장 큰 순간이 몇 초에 있나
        //   BulletWhiz1 ~ 5 는 모두 0.15초에 맞춰 두었다. 다른 클립을 쓰면 그 클립의 정점 시각으로
        [SerializeField] private float whizLeadTime = 0.15f;

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
            if (isHit)
            {
                gunEffects.Play(GetHitPoint());
            }
            else
            {
                Vector3 passPoint = GetPassPoint();
                gunEffects.Play(GetMissEnd(passPoint));
                PlayWhiz(passPoint);
            }

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

        // 빗나감 ① 스치는 점 : 몸 바로 옆(왼쪽 또는 오른쪽). 높이는 머리 조금 위 ~ 허리, 옆으로는 몸 폭보다 바깥
        private Vector3 GetPassPoint()
        {
            float down = Random.Range(-bodyTop, bodyBottom);   // 음수 = 머리보다 위 (귀 옆을 스치는 느낌)
            float side = Random.Range(missSideOffset.x, missSideOffset.y);
            if (Random.value < 0.5f) side = -side;              // 반반으로 왼쪽 / 오른쪽
            return target.position + Vector3.down * down + GetTargetRight() * side;
        }

        // 빗나감 ② 궤적이 끝나는 곳 : 총구 → 스치는 점 방향으로, 스치는 점보다 missPassDistance 만큼 더 간 곳
        //   궤적이 스치는 점에서 멈추면 "허공에서 사라진 총알" 처럼 보여서, 같은 방향으로 더 보낸다
        private Vector3 GetMissEnd(Vector3 passPoint)
        {
            Vector3 muzzle = gunEffects.transform.position;
            Vector3 direction = (passPoint - muzzle).normalized;
            return passPoint + direction * missPassDistance;
        }

        // 스치는 점에서 "휙". 총알 자국 머리가 그 점에 닿는 순간에 맞춰 튼다
        //   예) 10m, 120m/초 → 약 0.08초 뒤
        //   적에게서 떨어진 소리 오브젝트로 낸다. 적의 코루틴으로 기다리면, 빗나감으로 라운드가 바로 끝날 때
        //   EndRound 의 StopAllCoroutines 에 같이 멈춰서 소리가 안 난다
        private void PlayWhiz(Vector3 passPoint)
        {
            if (whizClips == null || whizClips.Length == 0) return;
            AudioClip clip = whizClips[Random.Range(0, whizClips.Length)];
            if (clip == null) return;

            float travelTime = Vector3.Distance(gunEffects.transform.position, passPoint) / gunEffects.BulletSpeed;
            float delay = Mathf.Max(0f, travelTime - whizLeadTime);

            GameObject whiz = new GameObject($"BulletWhiz ({clip.name})");
            whiz.transform.position = passPoint;

            AudioSource source = whiz.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = whizVolume;
            source.pitch = Random.Range(whizPitchRange.x, whizPitchRange.y);
            source.spatialBlend = 1f;    // 3D : 왼쪽으로 빗나가면 왼쪽 귀에서 들린다
            source.minDistance = 1f;     // 스치는 점은 귀에서 0.35 ~ 0.7m → 늘 이 안쪽이라 어디로 지나가든 같은 크기 (적 총성 여운에 묻히지 않게)
            source.maxDistance = 20f;
            source.dopplerLevel = 0f;    // 소리 오브젝트는 멈춰 있지만, 머리가 움직일 때 피치가 흔들리지 않게
            SoundManager.Route(source, SoundChannel.Sfx);
            source.PlayDelayed(delay);

            Destroy(whiz, delay + clip.length / source.pitch + 0.1f);
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

            // 내가 먼저 맞혔거나, 동귀어진 (StageManager 가 마지막 한 발이 맞으면 한 번 더 부른다)
            if (result.EnemyDown)
            {
                // 교안 Enemy.cs 의 SetInteger(DeadAnimation) + SetTrigger(Dead) 와 같은 모양
                //   부위 번호를 먼저 넣고 Die 를 켜면, Any State 화살표가 그 부위의 쓰러짐을 고른다
                animator.SetInteger(HitPartParam, (int)result.HitPart);
                animator.SetTrigger(Die);
            }
        }
    }
}
