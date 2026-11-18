using UnityEngine;

namespace Game
{
    // 총 한 발의 결과.
    //   총(Gun)이 채워서 Fired 로 내보내면 → CombatManager 가 받아서 PlayerFired 로 다시 알리고 → 구독자들이 읽는다.
    // 교안 CombatEvent 와 같은 역할의 struct 다. (값 몇 개를 봉투 하나에 담아 보내는 것)
    //
    // 구독자가 쓸 곳 (예)
    //   GameManager : 반응속도 = shot.FireTime - 신호 시각,  승패 = shot.IsHit,  점수 보너스 = shot.Zone.Part
    //   적          : shot.IsHit 이고 맞은 HitZone 이 내 것이면 쓰러진다
    //   소리 · 연출  : shot.Origin 에서 총성과 총구 화염
    public struct ShotInfo
    {
        public float FireTime;   // 쏜 시각 (Time.time = 게임 시작부터 흐른 초)
        public Vector3 Origin;   // 총알이 나간 곳 (총구)
        public Vector3 Point;    // 총알이 닿은 곳. 아무것도 안 맞았으면 사거리 끝
        public HitZone Zone;     // 맞은 HitZone (적의 머리 / 몸통). 빗나가면 비어 있다(null)

        // HitZone 을 맞혔나? 벽 · 바닥을 맞혔거나 허공이면 false
        public bool IsHit => Zone != null;
    }
}
