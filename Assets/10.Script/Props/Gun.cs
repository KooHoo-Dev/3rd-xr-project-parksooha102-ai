using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game
{
    // 총
    // 총이 하는 일만 둔다. (교안 FishingRod 가 낚싯대 일만 하는 것처럼)
    //   잡히는 일              → XR Grab Interactable
    //                             손으로 직접 잡기(가까이)와 레이로 잡기(멀리) 둘 다 된다. (XRI 기본, Near-Far Interactor)
    //                             잡히는 범위는 총 모델의 Mesh Collider 다.
    //   홀스터에 꽂히는 일      → XR Socket Interactor + HolsterSlot (Holster 오브젝트)
    //   총은 홀스터를 모른다. 홀스터 쪽이 총을 안다.
    //   쏘는 일                → Fire() : 총구에서 레이를 쏘고, 결과를 Fired 이벤트로 내보낸다
    //   총은 누가 듣는지 모른다. CombatManager 가 총을 알고 Fired 를 구독한다. (홀스터가 총을 아는 것과 같다)
    //   맞은 다음 일(쓰러지기, 점수, 소리)은 CombatManager 의 PlayerFired 를 구독한 쪽이 한다.
    //
    // 지금 하는 일
    //   잡은 손으로 트리거를 당기면(activated) Fire() 가 불린다.
    //   잠겨 있으면(IsLocked) 트리거를 당겨도 나가지 않는다.
    //   총은 "잠김 / 풀림" 스위치만 가진다. 언제 잠그고 풀지(라운드당 한 발)는 라운드를 아는 StageManager 가 정한다.
    //     StageManager ──▶ CombatManager.LockPlayerGun / UnlockPlayerGun ──▶ Gun.Lock / Unlock
    //   처음에는 풀려 있다. 그래서 StageManager 가 없는 테스트 씬(ShotTester)에서는 당기는 만큼 쏜다.
    //
    // 사용법
    //   총 오브젝트(SM_Wep_Revolver_01)에 XR Grab Interactable 과 같이 붙인다.
    //     Muzzle : 총의 Muzzle (총알이 나갈 자리, 파란 화살표(z)가 앞)
    //     Range  : 총알이 닿는 거리 (m)
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Gun : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform muzzle;

        [Header("설정")]
        [SerializeField] private float range = 50f;   // 결투 거리는 최대 10m 라서 넉넉하게

        // 한 발 쐈다. 맞았든 빗나갔든 쏠 때마다 한 번 내보낸다 (교안 Enemy 의 GoalReached 와 같은 방식)
        public event Action<ShotInfo> Fired;

        // 잠겼나? true 면 트리거를 당겨도 나가지 않는다
        public bool IsLocked { get; private set; }

        private XRGrabInteractable grab;

        public void Lock()
        {
            IsLocked = true;
        }

        public void Unlock()
        {
            IsLocked = false;
        }

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            grab.activated.AddListener(OnActivated);
        }

        private void OnDisable()
        {
            grab.activated.RemoveListener(OnActivated);
        }

        // 잡은 상태에서 트리거를 당겼다
        private void OnActivated(ActivateEventArgs args)
        {
            // 잠겨 있으면 헛방아쇠 (나중에 "딸깍" 소리를 여기에 붙인다)
            if (IsLocked)
            {
                Debug.Log("[Gun] 잠겨 있어서 나가지 않았다");
                return;
            }

            Fire();
        }

        // 한 발 쏜다
        public void Fire()
        {
            // ① 봉투(ShotInfo)에 쏜 시각과 총구 자리를 적는다
            ShotInfo shot = new ShotInfo();
            shot.FireTime = Time.time;
            shot.Origin = muzzle.position;

            // ② 총구에서 앞(파란 화살표 z)으로 레이를 쏜다 (히트스캔)
            //    총알이 날아가는 게 아니라, 쏘는 순간 그 직선 위에서 처음 닿는 콜라이더를 바로 찾는다.
            //    QueryTriggerInteraction.Ignore : Is Trigger 콜라이더는 무시하고 지나간다.
            //      홀스터 소켓의 꽂히는 범위 같은 트리거에 총알이 막히면 안 되기 때문이다.
            if (Physics.Raycast(muzzle.position, muzzle.forward, out RaycastHit hit, range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                shot.Point = hit.point;

                // ③ 맞은 콜라이더에 HitZone(적의 머리 / 몸통)이 붙어 있나?
                //    벽 · 바닥처럼 HitZone 이 없는 곳을 맞히면 Zone 은 비어 있다 → 빗나감. 이건 정상이다
                if (hit.collider.TryGetComponent(out HitZone zone))
                    shot.Zone = zone;
            }
            else
            {
                // 아무것도 안 맞았다 (허공) → 사거리 끝을 닿은 곳으로 적는다
                shot.Point = muzzle.position + muzzle.forward * range;
            }

            // ④ "쐈다" 고 내보낸다. 누가 듣는지, 그다음 무엇을 하는지는 총이 모른다
            Fired?.Invoke(shot);
        }
    }
}
