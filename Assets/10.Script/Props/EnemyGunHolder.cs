using UnityEngine;

namespace Game
{
    // 적의 총을 홀스터 ↔ 오른손으로 옮겨 주는 객체. 애니메이션 이벤트가 부른다.
    public class EnemyGunHolder : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Transform gun;            // 총 모델
        [SerializeField] private Transform holsterPoint;   // 홀스터 안 총 자리
        [SerializeField] private Transform handPoint;      // 오른손 안 총 자리

        // 애니메이션 이벤트 : 드로우 중 손이 손잡이를 잡는 순간
        public void GrabGun()
        {
            Attach(handPoint);
        }

        // 애니메이션 이벤트 : 대기 자세로 돌아오는 순간 (다음 라운드)
        public void HolsterGun()
        {
            Attach(holsterPoint);
        }

        // 총을 자리의 자식으로 옮기고, 자리와 딱 겹치게 한다
        // (부모가 손 뼈이면 손을 따라 움직이고, 홀스터이면 허리를 따라 움직인다)
        private void Attach(Transform point)
        {
            gun.SetParent(point, false);
            gun.localPosition = Vector3.zero;
            gun.localRotation = Quaternion.identity;
        }
    }
}
