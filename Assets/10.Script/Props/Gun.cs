using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game
{
    // 총. 트리거를 당기면 총구에서 레이를 쏘고, 결과를 Fired 로 내보낸다. 잠겨 있으면 나가지 않는다.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Gun : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private GunEffects effects;   // 총성 · 총구 화염 · 궤적 (보통 Muzzle 에 붙어 있다)

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

            // ④ 쏜 모습 : 총성 · 총구 화염, 그리고 닿은 곳까지 궤적
            effects.Play(shot.Point);

            // ⑤ "쐈다" 고 내보낸다. 누가 듣는지, 그다음 무엇을 하는지는 총이 모른다
            Fired?.Invoke(shot);
        }
    }
}
