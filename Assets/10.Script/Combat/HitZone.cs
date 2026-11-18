using System;
using UnityEngine;

namespace Game
{
    // 맞는 부위. 부위마다 점수 보너스가 다르다. (기획서 4.2 : 머리 +500, 몸통 +200)
    public enum HitPart
    {
        Head,   // 머리
        Body,   // 몸통
    }

    // 맞는 자리 (교안 HurtBox 와 같은 역할)
    // "총에 맞으면 여기가 어느 부위인지" 만 알려 준다. 맞았을 때 무엇을 할지는 모른다.
    //   쓰러지기, 점수 계산은 CombatManager 의 PlayerFired 를 구독한 쪽이 한다.
    //
    // 사용법
    //   콜라이더가 있는 오브젝트에 붙이고 Part 를 고른다. (콜라이더와 같은 오브젝트에 붙여야 총이 찾는다)
    //     과녁 테스트 : 3D Object > Cube 를 만들고 붙인다 (Cube 에는 Box Collider 가 이미 있다)
    //     적         : Head 뼈에 Sphere Collider + HitZone(Head), Spine 뼈에 Capsule Collider + HitZone(Body)
    //   콜라이더의 Is Trigger 는 꺼 둔다. (총은 트리거를 무시하고 쏜다. 이유는 Gun.cs ②)
    [RequireComponent(typeof(Collider))]
    public class HitZone : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private HitPart part;

        public HitPart Part => part;

        private void Awake()
        {
            // Is Trigger 가 켜져 있으면 총알이 그냥 지나간다.
            // 에러 없이 "빗나감" 만 계속 나와서 원인을 찾기 어려우므로, 시작할 때 바로 멈춘다.
            if (GetComponent<Collider>().isTrigger)
                throw new Exception($"[HitZone] {name} : 콜라이더의 Is Trigger 를 꺼 주세요. 켜져 있으면 총에 맞지 않습니다.");
        }
    }
}
