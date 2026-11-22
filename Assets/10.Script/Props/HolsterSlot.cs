using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game
{
    // 홀스터 칸. "총을 홀스터에 꽂는" 일만 하고, 총이 빠지면 Drawn 을 내보낸다.
    //   손으로 뽑거나 꽂을 때 가죽에 쓸리는 소리(마찰음)도 낸다
    //     PutBack 으로 총이 저절로 돌아올 때(라운드 시작 · 홀스터 조정)는 손이 한 일이 아니라서 소리를 내지 않는다
    [RequireComponent(typeof(XRSocketInteractor))]
    public class HolsterSlot : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private XRGrabInteractable item;

        [Header("마찰음 (비워 두면 소리 없음)")]
        [SerializeField] private AudioClip drawClip;       // 뽑을 때 (Holster)
        [SerializeField] private AudioClip holsterClip;    // 꽂을 때 (Holster. 비워 두면 drawClip)
        [SerializeField, Range(0f, 1f)] private float frictionVolume = 0.7f;
        [SerializeField] private float drawPitch = 1.1f;      // 뽑을 때는 빠르게 "슥"
        [SerializeField] private float holsterPitch = 0.9f;   // 꽂을 때는 묵직하게 "스윽"
        [SerializeField] private float startupMute = 0.5f;    // 씬 시작 후 이 시간 동안은 소리 없음 (처음부터 꽂혀 있는 총)

        // 총이 홀스터에서 빠졌다 (뽑았다)
        public event Action Drawn;

        private XRSocketInteractor socket;
        private AudioSource frictionSource;
        private bool isPuttingBack;   // PutBack 이 꽂는 중 (소리 없음)

        private void Awake()
        {
            socket = GetComponent<XRSocketInteractor>();

            // 소리는 허리춤에서 나야 한다 → 홀스터에 3D AudioSource 를 단다 (없으면 만든다)
            if (TryGetComponent(out frictionSource) == false)
            {
                frictionSource = gameObject.AddComponent<AudioSource>();
                frictionSource.spatialBlend = 1f;
                frictionSource.minDistance = 0.3f;   // 허리춤이라 아주 가깝다
            }
            frictionSource.playOnAwake = false;
            SoundManager.Route(frictionSource, SoundChannel.Sfx);
        }

        private void OnEnable()
        {
            socket.selectEntered.AddListener(OnSelectEntered);
            socket.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            socket.selectEntered.RemoveListener(OnSelectEntered);
            socket.selectExited.RemoveListener(OnSelectExited);
        }

        // 소켓이 총을 잡았다 = 총이 홀스터에 꽂혔다
        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (isPuttingBack) return;
            PlayFriction(holsterClip != null ? holsterClip : drawClip, holsterPitch);
        }

        // 소켓이 잡고 있던 것을 놓았다 = 총이 홀스터에서 빠졌다
        private void OnSelectExited(SelectExitEventArgs args)
        {
            Drawn?.Invoke();

            // isCanceled : 손이 뽑은 게 아니라 소켓이 꺼지면서(씬 전환 등) 놓친 것
            if (args.isCanceled == false)
                PlayFriction(drawClip, drawPitch);
        }

        private void PlayFriction(AudioClip clip, float pitch)
        {
            if (clip == null) return;
            if (Time.timeSinceLevelLoad < startupMute) return;

            frictionSource.pitch = pitch;
            frictionSource.PlayOneShot(clip, frictionVolume);
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
            //    저절로 돌아오는 것이라 마찰음은 내지 않는다 (OnSelectEntered 가 isPuttingBack 을 본다)
            isPuttingBack = true;
            socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)item);
            isPuttingBack = false;
        }

        // 지금 홀스터에 꽂혀 있나? (스테이지 시작 조건 등에 쓸 수 있다)
        public bool IsHolstered()
        {
            return socket.IsSelecting(item);
        }
    }
}
