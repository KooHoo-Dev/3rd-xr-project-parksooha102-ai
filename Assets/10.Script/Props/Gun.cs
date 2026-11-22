using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game
{
    // 총 (더블 액션 리볼버). 트리거를 당긴 만큼 해머가 젖혀지고, 끝까지 당기면 해머가 떨어지며 한 발 나간다.
    //   결과는 Fired 로 내보낸다. 잠겨 있으면 해머는 떨어지지만 총알은 나가지 않는다 (헛방아쇠).
    //   부품이 움직이는 모습은 GunFeedback 이 이 클래스의 상태(TriggerPull · HammerCock · ChamberIndex)를 읽어서 그린다.
    //
    //   더블 액션 한 번 당기기
    //     ① 트리거를 당기는 동안 : 해머가 뒤로 젖혀지고, 실린더가 다음 약실로 한 칸 돈다
    //     ② 트리거가 격발점(breakPoint)을 넘는 프레임 : 해머가 "탁" 하고 제자리로 떨어진다
    //     ③ 바로 다음 프레임 : 총알이 나간다 (해머가 공이를 친 다음 화약이 터지는 순서)
    //     ④ 트리거를 resetPoint 아래로 놓아야 다시 젖혀진다 (트리거 리셋). 꾹 쥐고 있으면 연사가 안 된다
    //
    //   트리거 값 읽기 : 교안 HandInputReader 처럼 InputActionReference 의 ReadValue<float>() 로 읽는다.
    //     총은 왼손 · 오른손 어느 쪽으로도 잡을 수 있어서, 잡은 손(handedness)의 Activate Value 를 고른다
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Gun : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private GunEffects effects;   // 총성 · 총구 화염 · 궤적 (보통 Muzzle 에 붙어 있다)

        [Header("트리거 입력 (XRI Default Input Actions)")]
        [SerializeField] private InputActionReference leftTriggerValue;    // XRI Left Interaction/Activate Value
        [SerializeField] private InputActionReference rightTriggerValue;   // XRI Right Interaction/Activate Value

        [Header("설정")]
        [SerializeField] private float range = 50f;   // 결투 거리는 최대 10m 라서 넉넉하게

        [Header("격발 매커니즘")]
        [SerializeField, Range(0.1f, 1f)] private float breakPoint = 0.9f;   // 트리거가 여기를 넘으면 해머가 떨어진다
        [SerializeField, Range(0f, 0.9f)] private float resetPoint = 0.2f;   // 여기 아래로 놓아야 다시 쏠 수 있다

        [Header("손맛")]
        [SerializeField] private float fireHapticAmplitude = 0.9f;
        [SerializeField] private float fireHapticDuration = 0.08f;
        [SerializeField] private AudioClip dryFireClip;   // 잠겨 있을 때 해머만 떨어지는 "딸깍" (비워 두면 소리 없음)

        // 한 발 쐈다. 맞았든 빗나갔든 쏠 때마다 한 번 내보낸다 (교안 Enemy 의 GoalReached 와 같은 방식)
        public event Action<ShotInfo> Fired;

        // 잠겼나? true 면 해머가 떨어져도 총알이 나가지 않는다
        public bool IsLocked { get; private set; }

        // 지금 트리거를 얼마나 당겼나 (0 ~ 1). 잡고 있지 않으면 0
        public float TriggerPull { get; private set; }

        // 해머가 얼마나 젖혀졌나 (0 = 제자리, 1 = 격발 직전). 떨어진 뒤 리셋 전까지는 0
        public float HammerCock { get; private set; }

        // 실린더가 몇 칸 돌았나. 해머가 떨어질 때마다 1 씩 는다
        public int ChamberIndex { get; private set; }

        private XRGrabInteractable grab;
        private InputActionReference heldTrigger;   // 지금 잡은 손의 트리거. 안 잡았으면 null
        private HapticImpulsePlayer haptics;        // 지금 잡은 손의 진동

        private bool hammerDropped;   // 해머가 떨어진 뒤 트리거 리셋을 기다리는 중
        private bool firePending;     // 해머가 이번 프레임에 떨어졌다 → 다음 프레임에 총알이 나간다

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

            if (leftTriggerValue == null || rightTriggerValue == null)
                throw new Exception($"[Gun] {name} 의 Left / Right Trigger Value 칸이 비어 있습니다. XRI Default Input Actions 의 XRI Left(Right) Interaction/Activate Value 를 꽂아 주세요.");
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnSelectEntered);
            grab.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
            ReleaseTrigger();
        }

        // 잡았다 → 어느 손인지 보고 그 손의 트리거 · 진동을 고른다
        //   홀스터(XRSocketInteractor)에 꽂혀도 selectEntered 가 온다. 소켓은 손이 아니라서(handedness = None)
        //   트리거를 읽지 않는다. 안 막으면 홀스터에 꽂힌 총이 오른손 트리거에 격발된다
        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            IXRSelectInteractor interactor = args.interactorObject;

            if (interactor.handedness == InteractorHandedness.Left)
                heldTrigger = leftTriggerValue;
            else if (interactor.handedness == InteractorHandedness.Right)
                heldTrigger = rightTriggerValue;
            else
            {
                ReleaseTrigger();
                return;
            }

            haptics = interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
        }

        // 놓았다 (홀스터에 꽂음 포함) → 트리거에서 손가락이 빠진다. 해머도 제자리로
        private void OnSelectExited(SelectExitEventArgs args)
        {
            ReleaseTrigger();
        }

        private void ReleaseTrigger()
        {
            heldTrigger = null;
            haptics = null;
            hammerDropped = false;
            firePending = false;
            TriggerPull = 0f;
            HammerCock = 0f;
        }

        private void Update()
        {
            // ③ 앞 프레임에 해머가 떨어졌다 → 이번 프레임에 총알이 나간다
            if (firePending)
            {
                firePending = false;
                Fire();
            }

            if (heldTrigger == null) return;

            TriggerPull = heldTrigger.action != null ? heldTrigger.action.ReadValue<float>() : 0f;

            // ④ 해머가 떨어진 뒤 : 트리거를 충분히 놓을 때까지 해머는 제자리
            if (hammerDropped)
            {
                if (TriggerPull < resetPoint)
                    hammerDropped = false;   // 리셋. 다음 당김부터 다시 젖혀진다
                return;
            }

            // ① 당긴 만큼 젖혀진다 (격발점에서 1)
            HammerCock = Mathf.Clamp01(TriggerPull / breakPoint);

            // ② 격발점을 넘었다 → 해머가 떨어진다
            if (TriggerPull >= breakPoint)
                DropHammer();
        }

        // 해머가 떨어지는 프레임. 실린더는 이미 다음 약실에 맞물려 있다
        private void DropHammer()
        {
            hammerDropped = true;
            HammerCock = 0f;
            ChamberIndex++;

            // 잠겨 있으면 해머만 떨어지고 총알은 나가지 않는다 (헛방아쇠)
            if (IsLocked)
            {
                if (dryFireClip != null)
                    AudioSource.PlayClipAtPoint(dryFireClip, transform.position);
                Debug.Log("[Gun] 잠겨 있어서 나가지 않았다");
                return;
            }

            firePending = true;
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

            // ④ 쏜 모습 : 총성 · 총구 화염, 그리고 닿은 곳까지 궤적. 쥔 손에는 반동 진동
            effects.Play(shot.Point);
            if (haptics != null)
                haptics.SendHapticImpulse(fireHapticAmplitude, fireHapticDuration);

            // ⑤ "쐈다" 고 내보낸다. 누가 듣는지, 그다음 무엇을 하는지는 총이 모른다
            Fired?.Invoke(shot);
        }
    }
}
