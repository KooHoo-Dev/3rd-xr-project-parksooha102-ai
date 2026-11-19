using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Game
{
    // 전진 표식 (기획서 4.4 : 무승부 → 총을 꽂으면 다음 걸음 표식이 켜진다 → 가리켜서 이동하면 다음 라운드)
    // 실제 이동은 XRI 의 Teleportation Anchor 가 한다. 이 스크립트는 두 가지만 한다.
    //   ① 켜고 끄기 : StageManager 가 Show(자리, 방향) 로 켜고, Hide() 로 끈다
    //   ② 알리기   : 앵커가 이동시키면(teleporting 이벤트) 표식을 끄고 Advanced 를 내보낸다
    //   (HolsterSlot 이 소켓의 selectExited 를 Drawn 으로 옮겨 말해 주는 것과 같은 방식이다)
    //
    // Teleportation Anchor 는 어떻게 이동시키나? (XRI 기본 기능)
    //   손의 텔레포트 레이로 앵커를 가리키고 "고르기(Select)" 를 하면, 앵커 자리로 플레이어를 옮긴다.
    //     Starter Assets 기본 조작 : 썸스틱을 앞으로 밀면 포물선 레이가 나온다 → 표식에 맞추고 → 썸스틱을 놓으면 이동
    //   언제 옮길지는 앵커의 Teleport Trigger 가 정한다. (기본 On Select Exited = 고르기를 놓을 때)
    //   옮기는 일은 XR Origin 의 Teleportation Provider 가 한다. 앵커가 씬에서 알아서 찾는다.
    //   플레이어의 발밑(머리 바로 아래 바닥)이 앵커 자리에 온다. 보는 방향은 그대로다. (Match Orientation : None)
    //   HolsterAnchor 는 XR Origin 의 자식이라 홀스터도 같이 따라온다.
    //
    // 사용법
    //   ① 바닥에 놓을 표식 오브젝트(얇은 원판 등)를 만들고 이 스크립트를 붙인다.
    //      Teleportation Anchor 는 자동으로 붙는다.
    //      XR Origin 의 자식으로 넣지 않는다. (자식이면 이동할 때 같이 따라와서 표식이 계속 앞으로 간다)
    //   ② Teleportation Anchor 설정
    //      Interaction Layer Mask : Teleport  ← Starter Assets 의 Teleport Interactor 가 보는 레이어
    //      Match Orientation      : None  ← 표식의 방향으로 몸을 돌리지 않는다 (원기둥이라 방향이 없다)
    //   ③ 콜라이더는 Is Trigger 를 끈다. (레이는 트리거 콜라이더를 무시한다)
    //   ④ StageManager 의 Advance Marker 에 이 오브젝트를 넣는다.
    //
    // 구독하는 법 (StageManager)
    //   advanceMarker.Advanced += HandleAdvanced;
    [RequireComponent(typeof(TeleportationAnchor))]
    public class AdvanceMarker : MonoBehaviour
    {
        // 플레이어가 표식으로 이동했다
        public event Action Advanced;

        private TeleportationAnchor anchor;

        private void Awake()
        {
            anchor = GetComponent<TeleportationAnchor>();
        }

        private void OnEnable()
        {
            anchor.teleporting.AddListener(OnTeleporting);
        }

        private void OnDisable()
        {
            anchor.teleporting.RemoveListener(OnTeleporting);
        }

        // 앵커가 플레이어를 이 자리로 옮기라고 요청했다
        private void OnTeleporting(TeleportingEventArgs args)
        {
            Hide();
            Advanced?.Invoke();
        }

        // ---------------------------------------------------------------- API

        // 표식을 position 에 놓고 켠다 (원기둥이라 방향은 상관없다)
        public void Show(Vector3 position)
        {
            transform.position = position;
            gameObject.SetActive(true);
        }

        // 표식을 끈다
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
