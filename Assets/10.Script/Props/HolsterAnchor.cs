using UnityEngine;

namespace Game
{
    // 홀스터 자리 (허리춤). 부르면 홀스터를 플레이어 허리춤에 다시 붙이고, 총도 홀스터에 돌려놓는다.
    //   언제 부를지(시작할 때 · 위치 초기화할 때)는 PlayerPlacer 가 정한다. 머리 추적이 잡힌 뒤에만 부른다
    //   허리춤 안에서 홀스터가 어디 있는지(자식 Holster 의 Position)는 메인화면에서 맞춘 값(GameSettings)을 쓴다
    public class HolsterAnchor : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform head;

        private HolsterSlot holsterSlot;   // 자식 Holster. 부모-자식 관계이므로 Awake 에서 자식에서 꺼낸다

        private void Awake()
        {
            holsterSlot = GetComponentInChildren<HolsterSlot>();

            // 메인화면에서 맞춘 자리로 홀스터를 옮긴다
            //   씬의 Holster Position 은 편집할 때 보이는 자리일 뿐이고, Play 하면 이 값으로 바뀐다
            holsterSlot.transform.localPosition = GameSettings.HolsterOffset;
        }

        // ---------------------------------------------------------------- API

        // 허리춤 안에서 홀스터 자리를 바꾼다 (메인화면의 조정 버튼이 부른다)
        //   offset : 이 오브젝트(머리 자리 · 머리가 보는 쪽) 기준. x = 오른쪽, y = 위, z = 앞
        public void SetOffset(Vector3 offset)
        {
            holsterSlot.transform.localPosition = offset;

            // 총도 새 자리에 꽂는다. 뽑아서 들고 있거나 바닥에 떨어뜨렸어도 돌아온다
            //   꽂혀 있던 총은 소켓을 따라 저절로 옮겨지므로 PutBack 이 아무것도 안 한다
            holsterSlot.PutBack();
        }

        // 홀스터를 position(월드 좌표) 자리로 옮긴다. 빠른 조정 중에 매 프레임 주 손 자리를 넣는다
        //   Holster 는 이 오브젝트의 자식이라, 월드 자리(position)를 넣으면
        //   허리춤 기준 자리(localPosition)는 유니티가 알아서 계산해 둔다 → GetOffset 으로 꺼낸다
        public void MoveHolsterTo(Vector3 position)
        {
            holsterSlot.transform.position = position;
        }

        // 지금 홀스터 자리 (이 오브젝트 기준. SetOffset 에 넣는 값과 같은 모양)
        public Vector3 GetOffset()
        {
            return holsterSlot.transform.localPosition;
        }

        // 홀스터를 지금 머리 기준 허리춤으로 옮긴다
        [ContextMenu("위치 초기화")]
        public void ResetPosition()
        {
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
