using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.OpenXR;

namespace Game
{
    // 플레이어를 결투 자리에 세우는 객체.
    //   "머리 추적이 잡혔나" 와 "기기 위치 초기화" 는 여기서만 다룬다.
    //   세울 때는 플레이어 몸(텔레포트)과 허리 홀스터(HolsterAnchor)를 같이 맞춘다.
    //   어디에 세울지는 StageManager 가 정해서 알려 준다 (출발점 → 전진 표식 자리)
    public class PlayerPlacer : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform head;                               // Main Camera
        [SerializeField] private TeleportationProvider teleportationProvider;   // XR Origin → Locomotion → Teleportation
        [SerializeField] private HolsterAnchor holsterAnchor;

        [Header("설정")]
        [SerializeField] private float waitTime = 0.5f;        // 머리 추적이 잡힌 직후 · 위치 초기화 직후, 머리 위치가 자리 잡을 때까지 기다리는 시간(초)
        [SerializeField] private float minHeadHeight = 0.5f;   // 머리가 이것보다 낮으면 아직 추적 전으로 본다 (m). 앉아서 해도 넘는 높이

        // 지금 플레이어가 서 있어야 할 자리 (출발점 → 전진할 때마다 그 표식 자리). 위치 초기화하면 여기에 다시 세운다
        private Vector3 standPosition;
        private Quaternion standRotation;

        // 플레이어 머리 (Main Camera). 적이 총알 궤적을 어디로 보낼지 정할 때 쓴다
        public Transform Head => head;

        // 위치 초기화 이벤트를 보내 주는 쪽 (보통 1개)
        private List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();

        private void Awake()
        {
            // Floor 모드에서도 기기 위치 초기화가 먹히게 켠다
            OpenXRSettings.SetAllowRecentering(true);
            OpenXRSettings.RefreshRecenterSpace();   // 바뀐 설정으로 좌표를 다시 만든다
        }

        private void OnEnable()
        {
            // ① 지금 켜져 있는 XRInputSubsystem 을 모두 찾는다
            SubsystemManager.GetSubsystems(subsystems);

            // ② 하나하나에 "위치 초기화하면 HandleRecentered 를 불러 줘" 라고 등록한다
            for (int i = 0; i < subsystems.Count; i++)
                subsystems[i].trackingOriginUpdated += HandleRecentered;

            // 에디터에서 시뮬레이터로 돌리면 기기가 없어서 0개다. 이때는 ⋮ 메뉴의 "플레이어 다시 세우기" 로 시험한다
            if (subsystems.Count == 0)
                Debug.Log("[PlayerPlacer] 연결된 VR 기기가 없어 위치 초기화 버튼을 받을 수 없습니다. (시뮬레이터라면 정상)");
        }

        private void OnDisable()
        {
            // 등록했던 것을 그대로 해제한다
            for (int i = 0; i < subsystems.Count; i++)
                subsystems[i].trackingOriginUpdated -= HandleRecentered;
            subsystems.Clear();

            CancelInvoke(nameof(Place));
        }

        // 머리 추적이 잡혔나?
        //   Quest 에서 앱이 막 켜졌을 때는 헤드셋 추적이 몇 초 늦게 잡힌다. 그 전의 머리 위치는 바닥(높이 0)이다.
        //   (Tracking Origin Mode 가 Floor 라서 머리의 localPosition.y = 바닥에서 머리까지 높이다)
        //   에디터 시뮬레이터는 기기가 0개라 기다릴 추적이 없다 → 바로 잡힌 것으로 본다
        //     (시뮬레이터 머리는 높이 0 에서 시작해서, 높이로 보면 영영 안 잡힌 것이 된다)
        private bool IsHeadTracked()
        {
            if (subsystems.Count == 0) return true;

            return head.localPosition.y > minHeadHeight;
        }

        // 기기가 위치 초기화를 했다
        //   위치 초기화는 머리를 "XR Origin 의 정면" 으로 돌려놓는다.
        //   그런데 출발점 · 전진 표식 텔레포트가 적 쪽을 보게 하려고 XR Origin 을 이미 돌려놨다 → 그 정면은 적 쪽이 아니다.
        //   그래서 위치 초기화가 끝나면 지금 자리에서 적 쪽을 보게 다시 세운다 (UI · 시계탑도 다시 정면에 온다)
        private void HandleRecentered(XRInputSubsystem subsystem)
        {
            // 이벤트가 여러 번 와도, 마지막 이벤트로부터 waitTime 초 뒤에 한 번만 세운다
            CancelInvoke(nameof(Place));
            Invoke(nameof(Place), waitTime);
        }

        // 플레이어를 지금 설 자리에 세우고, 홀스터도 허리춤에 다시 붙인다
        //   ① 텔레포트 : XR Origin 뿌리가 아니라 플레이어 몸(머리 바로 아래 바닥)이 그 자리에 오고, 머리 방향이 standRotation 의 앞쪽으로 돌아간다
        //      → 방 안 어디에 서서 어느 쪽을 보고 있든 결투 자리에서 적을 보게 된다
        //      요청만 넣어 두면 TeleportationProvider 가 다음 Update 에서 옮긴다
        //   ② 홀스터 : 지금 머리 기준 허리춤에 붙인다
        //      홀스터는 XR Origin 의 자식이라 ① 의 텔레포트 때 몸과 같이 옮겨진다 → ① 보다 먼저 붙여도 자리가 맞는다
        [ContextMenu("플레이어 다시 세우기")]
        private void Place()
        {
            // 시작 직후 기기가 보내는 위치 초기화 이벤트가 추적보다 먼저 올 수 있다. 그때는 PlaceFirst 가 나중에 세운다
            if (IsHeadTracked() == false)
            {
                Debug.Log("[PlayerPlacer] 아직 머리 추적 전이라 플레이어를 세우지 않았습니다.");
                return;
            }

            TeleportRequest request = new TeleportRequest();
            request.destinationPosition = standPosition;
            request.destinationRotation = standRotation;
            request.matchOrientation = MatchOrientation.TargetUpAndForward;   // 위 방향 + 앞 방향까지 맞춘다
            request.requestTime = Time.time;
            teleportationProvider.QueueTeleportRequest(request);

            holsterAnchor.ResetPosition();
        }

        // ---------------------------------------------------------------- API (StageManager 가 부른다)

        // 처음 세우기 : 머리 추적이 잡힐 때까지 기다렸다가 position 에 세우고 rotation 의 앞쪽을 보게 한다
        //   StageManager 가 yield return 으로 기다린다 → 플레이어가 서기 전에는 1라운드가 시작되지 않는다
        public IEnumerator PlaceFirst(Vector3 position, Quaternion rotation)
        {
            SetStandPose(position, rotation);

            // ① 머리가 바닥 근처에 있는 동안은 기다린다
            while (IsHeadTracked() == false)
                yield return null;

            // ② 잡힌 직후엔 위치가 조금 흔들리므로 waitTime 초 더 기다린 뒤 세운다
            yield return new WaitForSeconds(waitTime);
            Place();
        }

        // 이제부터 설 자리 (전진 표식으로 이동했을 때)
        //   옮기는 건 표식(텔레포트 앵커)이 이미 했으므로 기억만 한다. 위치 초기화하면 여기에 다시 세운다
        public void SetStandPose(Vector3 position, Quaternion rotation)
        {
            standPosition = position;
            standRotation = rotation;
        }
    }
}
