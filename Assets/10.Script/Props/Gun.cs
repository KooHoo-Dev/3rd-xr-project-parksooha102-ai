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
    //
    // 지금 하는 일
    //   잡은 손으로 트리거를 당기면(activated) Fire() 가 불린다.
    //   아직 쏘는 처리는 없고, 총구 방향으로 선을 그리고 로그만 찍는다. (잡기가 되는지 확인용)
    //
    // 사용법
    //   총 오브젝트(SM_Wep_Revolver_01)에 XR Grab Interactable 과 같이 붙인다.
    //     Muzzle : 총의 Muzzle (총알이 나갈 자리, 파란 화살표(z)가 앞)
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Gun : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform muzzle;

        private XRGrabInteractable grab;

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
            Fire();
        }

        public void Fire()
        {
            Debug.DrawRay(muzzle.position, muzzle.forward * 10f, Color.red, 1f);
            Debug.Log("[Gun] 탕!");
        }
    }
}
