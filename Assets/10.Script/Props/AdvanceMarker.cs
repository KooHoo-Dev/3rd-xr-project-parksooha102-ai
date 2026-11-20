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
