using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

namespace Game
{
    // 홀스터 자리 (허리춤)
    // "홀스터를 플레이어 허리춤에 다시 붙이는" 일을 한다. 붙인 뒤 총도 홀스터에 돌려놓는다.
    // (총을 꽂는 방법은 HolsterSlot 이 안다. 여기서는 자식 HolsterSlot 의 PutBack() 만 부른다)
    //   부모가 자식을 아는 건 자연스럽다. (Hierarchy 에서 이미 부모-자식이므로)
    //   나중에 GameManager 가 생기면 라운드 시작 때 GameManager 가 ResetPosition() 을 부르면 된다.
    //
    // 어떻게 붙이나?
    //   이 오브젝트는 "몸통 기준점" 이다. 위치 초기화를 하면
    //     자리 : 머리(카메라) 자리로 옮긴다
    //     방향 : 머리가 보는 방향으로 돌린다. 단 수평으로만 (고개를 숙이거나 갸웃한 건 버린다)
    //   Holster 는 이 오브젝트의 자식이라서 같이 따라온다. (부모가 움직이면 자식도 같이 움직인다)
    //   그래서 "머리에서 얼마나 떨어진 곳이 허리춤인가" 는 계산하지 않고 Holster 의 Transform 으로 정한다.
    //     Holster Position : (x, -0.6, z)  ← 머리에서 아래로 0.6m. x 는 좌우, z 는 앞뒤
    //     Holster Rotation : (90, 0, 0)
    //
    // 언제 붙이나?
    //   ① 게임이 시작될 때 (머리 추적이 잡힌 뒤 waitTime 초 뒤 한 번)
    //   ② 기기에서 위치 초기화를 했을 때 (Quest: 오른손 Meta 버튼 길게 누르기)
    //   ③ 다른 스크립트가 ResetPosition() 을 불렀을 때 (라운드 시작 등)
    //   ④ 에디터 Play 중 이 컴포넌트 오른쪽 위 ⋮ 메뉴 > "위치 초기화" (헤드셋 없이 시험할 때)
    //
    // 기기의 위치 초기화를 아는 방법
    //   기기가 위치 초기화를 하면 XRInputSubsystem 이 trackingOriginUpdated 이벤트를 보낸다.
    //   (버튼 onClick 에 AddListener 하는 것과 같은 일이다. C# 이벤트라서 += 로 등록, -= 로 해제한다)
    //   한 번 초기화해도 이벤트가 여러 번 올 수 있고, 머리 위치도 몇 프레임 뒤에야 자리를 잡는다.
    //   그래서 이벤트가 오면 바로 옮기지 않고, waitTime 초 뒤에 한 번만 옮긴다. (Invoke)
    //
    // 머리 추적이 잡혔는지 아는 방법
    //   Quest 에서 앱이 막 켜졌을 때는 헤드셋 추적이 몇 초 늦게 잡힌다. 그 전의 머리 위치는 바닥(높이 0)이다.
    //   이때 붙이면 홀스터가 바닥에서 0.6m 아래 = 땅속에 붙어서 총이 안 보인다. (위치 초기화를 해야 보이던 버그)
    //   그래서 머리 높이가 minHeadHeight 보다 높아질 때까지 기다린 뒤에 붙인다.
    //   (Tracking Origin Mode 가 Floor 라서 머리의 localPosition.y = 바닥에서 머리까지 높이다)
    //
    // Allow Recentering
    //   XR Origin 의 Tracking Origin Mode 가 Floor 면, OpenXR 은 기본으로 "방(STAGE) 기준" 좌표를 쓴다.
    //   방 기준 좌표는 위치 초기화를 해도 움직이지 않아서, Quest 에서 버튼을 눌러도 아무 일이 안 일어날 수 있다.
    //   그래서 시작할 때 SetAllowRecentering(true) 로 "위치 초기화를 따르는 바닥 좌표" 를 쓰게 바꾼다.
    //   이게 켜져 있어야 위치 초기화 때 플레이어가 XR Origin 자리로 오고, 이벤트도 온다.
    //
    // 사용법
    //   ① Hierarchy 에서 XR Origin (XR Rig) 아래에 빈 오브젝트 HolsterAnchor 를 만들고 이 스크립트를 붙인다.
    //        Position : (0, 1.6, 0)  ← 보통 키 사람의 머리 높이. 시작 직후 잠깐 동안만 쓰인다
    //        Rotation : (0, 0, 0)
    //   ② 기존 Holster 를 HolsterAnchor 의 자식으로 끌어다 넣고, Position y 를 -0.6 정도로 고친다.
    //   ③ Head : XR Origin > Camera Offset > Main Camera (꼭 넣는다)
    //
    // 다른 스크립트에서 부르기
    //   [SerializeField] private HolsterAnchor holsterAnchor;
    //   holsterAnchor.ResetPosition();
    public class HolsterAnchor : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform head;

        [Header("설정")]
        [SerializeField] private float waitTime = 0.5f;   // 위치 초기화 후 머리 위치가 자리 잡을 때까지 기다리는 시간(초)
        [SerializeField] private float minHeadHeight = 0.5f;   // 머리가 이것보다 낮으면 아직 추적 전으로 본다 (m). 앉아서 해도 넘는 높이

        private HolsterSlot holsterSlot;   // 자식 Holster. 부모-자식 관계이므로 Awake 에서 자식에서 꺼낸다

        // 위치 초기화 이벤트를 보내 주는 쪽 (보통 1개)
        private List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();

        private void Awake()
        {
            holsterSlot = GetComponentInChildren<HolsterSlot>();

            // Floor 모드에서도 기기 위치 초기화가 먹히게 켠다 (위 Allow Recentering 설명)
            OpenXRSettings.SetAllowRecentering(true);
            OpenXRSettings.RefreshRecenterSpace();   // 바뀐 설정으로 좌표를 다시 만든다
        }

        private void OnEnable()
        {
            // ① 지금 켜져 있는 XRInputSubsystem 을 모두 찾는다
            SubsystemManager.GetSubsystems(subsystems);

            // ② 하나하나에 "위치 초기화하면 OnRecentered 를 불러 줘" 라고 등록한다
            for (int i = 0; i < subsystems.Count; i++)
                subsystems[i].trackingOriginUpdated += OnRecentered;

            // 에디터에서 시뮬레이터로 돌리면 기기가 없어서 0개다. 이때는 ⋮ 메뉴로 시험한다
            if (subsystems.Count == 0)
                Debug.Log("[HolsterAnchor] 연결된 VR 기기가 없어 위치 초기화 버튼을 받을 수 없습니다. (시뮬레이터라면 정상)");
        }

        private void OnDisable()
        {
            // 등록했던 것을 그대로 해제한다
            for (int i = 0; i < subsystems.Count; i++)
                subsystems[i].trackingOriginUpdated -= OnRecentered;
            subsystems.Clear();

            CancelInvoke(nameof(ResetPosition));
        }

        // 시작할 때도 한 번 붙인다. 단 머리 추적이 잡힌 뒤에 (위 "머리 추적이 잡혔는지 아는 방법")
        private IEnumerator Start()
        {
            // ① 머리가 바닥 근처에 있는 동안은 기다린다
            while (IsHeadTracked() == false)
                yield return null;

            // ② 잡힌 직후엔 위치가 조금 흔들리므로 waitTime 초 더 기다린 뒤 붙인다
            yield return new WaitForSeconds(waitTime);
            ResetPosition();
        }

        // 머리 추적이 잡혔나? (머리가 바닥에서 minHeadHeight 보다 높은가)
        private bool IsHeadTracked()
        {
            return head.localPosition.y > minHeadHeight;
        }

        // 기기가 위치 초기화를 했다
        private void OnRecentered(XRInputSubsystem subsystem)
        {
            // 이벤트가 여러 번 와도, 마지막 이벤트로부터 waitTime 초 뒤에 한 번만 옮긴다
            CancelInvoke(nameof(ResetPosition));
            Invoke(nameof(ResetPosition), waitTime);
        }

        // ---------------------------------------------------------------- API

        // 홀스터를 지금 머리 기준 허리춤으로 옮긴다
        [ContextMenu("위치 초기화")]
        public void ResetPosition()
        {
            // ⓪ 아직 머리 추적 전이면 땅속에 붙으므로 건너뛴다
            //    (시작 직후 기기가 보내는 위치 초기화 이벤트가 추적보다 먼저 올 수 있다. 그때는 Start 가 나중에 붙인다)
            if (IsHeadTracked() == false)
            {
                Debug.Log("[HolsterAnchor] 아직 머리 추적 전이라 홀스터를 옮기지 않았습니다.");
                return;
            }

            // ① 머리가 보는 방향을 바닥과 평행하게 만든다 (y 를 0 으로 → 위아래로 기울어진 만큼을 버린다)
            Vector3 forward = head.forward;
            forward.y = 0f;

            //    바로 아래나 위를 보고 있으면 어디가 앞인지 알 수 없다 → 이번엔 건너뛴다
            if (forward.sqrMagnitude < 0.01f)
            {
                Debug.Log("[HolsterAnchor] 앞을 보고 다시 위치 초기화를 해 주세요.");
                return;
            }

            // ② 자리 : 머리 자리로 옮긴다 (Holster 는 자식이라 머리 기준 (x, -0.6, z) 에 놓인다)
            transform.position = head.position;

            // ③ 방향 : ① 의 방향을 보게 돌린다
            //    LookRotation(방향) = "그 방향을 바라보게 하는 회전값". 위쪽은 기본으로 하늘(Vector3.up)이다
            transform.rotation = Quaternion.LookRotation(forward);

            // ④ 총도 돌려놓는다. 홀스터가 먼저 자리를 잡은 뒤라서 총이 새 허리춤 자리에 꽂힌다
            //    (이미 꽂혀 있으면 PutBack 이 아무것도 안 한다)
            holsterSlot.PutBack();
        }
    }
}
