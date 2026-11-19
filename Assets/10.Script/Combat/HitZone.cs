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

    // 부위 이름 (HitPart.Head → "머리"). 결과판과 라운드 쪽지가 같이 쓴다
    //   ScoreCalculator 처럼 기억할 것이 없어서 static 이다.
    //     string name = HitPartNames.Get(HitPart.Head);   // → "머리"
    public static class HitPartNames
    {
        public static string Get(HitPart part)
        {
            switch (part)
            {
                case HitPart.Head: return "머리";
                case HitPart.Body: return "몸통";
            }

            // HitPart 에 부위를 새로 추가하고 여기를 안 고쳤다
            throw new Exception($"[HitPartNames] 이름이 정해지지 않은 부위입니다 : {part}");
        }
    }

    // 맞는 자리 (교안 HurtBox 와 같은 역할). "총에 맞으면 여기가 어느 부위인지" 만 알려 준다.
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
