using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game
{
    // 홀스터 칸
    // "총을 홀스터에 꽂는" 일만 한다.
    // 소켓이 총을 알아보려면 이 오브젝트에도 Is Trigger 콜라이더가 있어야 한다. (놓았을 때 꽂히는 범위)
    //
    // 홀스터 자리
    //   이 스크립트는 자리를 정하지 않는다. 자리는 부모 HolsterAnchor 가 맞춘다.
    //   Hierarchy : XR Origin (XR Rig) > HolsterAnchor > Holster
    //     Holster Position : (x, -0.6, z)  ← 머리에서 아래로 0.6m (HolsterAnchor 가 머리 자리에 있으므로)
    //     Holster Rotation : (90, 0, 0)
    //   XR Origin 이 움직이면(텔레포트 등) 홀스터도 같이 따라간다.
    //   기기 위치 초기화(오른손 Meta 버튼 길게 누르기)를 하면 HolsterAnchor 가 허리춤으로 다시 붙인다.
    //
    // 동작
    //   시작할 때 꽂는 건 XR Socket Interactor 의 Starting Selected Interactable 이 한다. (XRI 기본 기능)
    //   그 뒤로는 누가 PutBack() 을 불러야만 꽂는다.
    //     손에서 놓친 총은 그대로 떨어진다. 스테이지가 넘어갈 때 PutBack() 을 불러서 돌려놓는다.
    //     홀스터 구 안에서 놓은 총은 소켓이 알아서 꽂는다. (이것도 XRI 기본 기능)
    //
    // 사용법
    //   씬의 Holster 오브젝트(XR Socket Interactor 가 있는 곳)에 붙인다.
    //     Item : 총의 XR Grab Interactable
    //   소켓의 Starting Selected Interactable 에도 같은 총을 넣는다.
    //
    // 다른 스크립트에서 부르기
    //   [SerializeField] private HolsterSlot holsterSlot;
    //   holsterSlot.PutBack();
    [RequireComponent(typeof(XRSocketInteractor))]
    public class HolsterSlot : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private XRGrabInteractable item;

        private XRSocketInteractor socket;

        private void Awake()
        {
            socket = GetComponent<XRSocketInteractor>();
        }

        // ---------------------------------------------------------------- API

        // item 을 홀스터에 꽂는다. 손에 들려 있으면 손에서 빼앗아서 꽂는다
        public void PutBack()
        {
            // ① 이미 꽂혀 있으면 할 일 없음
            if (socket.IsSelecting(item)) return;

            // ② 손이 잡고 있으면 먼저 놓게 한다
            //    SelectExit 를 하면 interactorsSelecting 목록에서 빠지므로, 뒤에서부터 돈다
            for (int i = item.interactorsSelecting.Count - 1; i >= 0; i--)
            {
                IXRSelectInteractor holder = item.interactorsSelecting[i];
                socket.interactionManager.SelectExit(holder, item);
            }

            // ③ 총에 남아 있는 속도를 없앤다 (교안 PhysicsResetter 와 같은 방법)
            //    날아가던 총이나, ② 에서 손이 놓으면서 "던지기" 속도를 받은 총이 꽂히자마자 튕겨 나가지 않게 한다
            //    isKinematic 이 true 면 물리엔진이 속도를 쓰지 않으므로 건드리지 않는다
            //    (XR Grab Interactable 은 Rigidbody 를 RequireComponent 하므로 항상 있다)
            Rigidbody body = item.GetComponent<Rigidbody>();
            if (body.isKinematic == false)
            {
                body.linearVelocity = Vector3.zero;    // 선 속도 (직선 움직임)
                body.angularVelocity = Vector3.zero;   // 각 속도 (회전 움직임)
            }

            // ④ 소켓에게 "이 총을 잡아라" 고 시킨다 → 총이 홀스터 자리로 붙는다
            //    자리를 옮기는 건 소켓이 하므로, 교안처럼 position/rotation 을 직접 넣지 않는다
            socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)item);
        }

        // 지금 홀스터에 꽂혀 있나? (스테이지 시작 조건 등에 쓸 수 있다)
        public bool IsHolstered()
        {
            return socket.IsSelecting(item);
        }
    }
}
