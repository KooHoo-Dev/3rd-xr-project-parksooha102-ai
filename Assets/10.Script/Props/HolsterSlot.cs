using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game
{
    // 홀스터 칸. "총을 홀스터에 꽂는" 일만 하고, 총이 빠지면 Drawn 을 내보낸다.
    [RequireComponent(typeof(XRSocketInteractor))]
    public class HolsterSlot : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private XRGrabInteractable item;

        // 총이 홀스터에서 빠졌다 (뽑았다)
        public event Action Drawn;

        private XRSocketInteractor socket;

        private void Awake()
        {
            socket = GetComponent<XRSocketInteractor>();
        }

        private void OnEnable()
        {
            socket.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            socket.selectExited.RemoveListener(OnSelectExited);
        }

        // 소켓이 잡고 있던 것을 놓았다 = 총이 홀스터에서 빠졌다
        private void OnSelectExited(SelectExitEventArgs args)
        {
            Drawn?.Invoke();
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
