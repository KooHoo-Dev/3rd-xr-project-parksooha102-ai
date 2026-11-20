using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Game
{
    // 전진 표식 (기획서 4.4). 켜고 끄기, 그리고 플레이어가 이리로 텔레포트하면 Advanced 로 알린다.
    [RequireComponent(typeof(TeleportationAnchor))]
    public class AdvanceMarker : MonoBehaviour
    {
        // 플레이어가 표식으로 이동했다
        public event Action Advanced;

        private TeleportationAnchor anchor;

        private void Awake()
        {
            anchor = GetComponent<TeleportationAnchor>();

            // 도착한 플레이어가 표식이 보는 쪽(적 쪽)을 보게 하려면 이 설정이어야 한다 (기획서 8장)
            //   인스펙터에서 빠뜨리면 플레이어가 엉뚱한 쪽을 본 채로 대치하게 되니, 시작하자마자 알린다
            if (anchor.matchOrientation != MatchOrientation.TargetUpAndForward)
                throw new Exception($"[AdvanceMarker] {name} 의 Teleportation Anchor → Match Orientation 을 Target Up And Forward 로 바꿔 주세요. (도착하면 적을 보게)");
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

        // 표식을 position 에 놓고 켠다
        //   원기둥이라 돌려도 모양은 같지만, 도착한 플레이어는 rotation 의 앞쪽(파란 화살표)을 보게 된다
        public void Show(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
        }

        // 표식을 끈다
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
