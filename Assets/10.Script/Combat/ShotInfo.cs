using UnityEngine;

namespace Game
{
    // 총 한 발의 결과 봉투. Gun 이 채워서 Fired 로 내보내면 → CombatManager 가 PlayerFired 로 다시 알린다.
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
