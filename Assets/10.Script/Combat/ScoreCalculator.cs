using System;
using UnityEngine;

namespace Game
{
    // 점수 계산 (기획서 7장)
    //   점수 = S + max(0, 1.5 - t) × 1000 + B + (500 - 100 × (r - 1))
    public static class ScoreCalculator
    {
        private const float SpeedLimit = 1.5f;           // 이보다 늦게 쏘면 속도 점수가 0
        private const int SpeedScorePerSecond = 1000;    // 1.5초보다 1초 빠를 때마다 +1000
        private const int HeadBonus = 500;               // 머리 보너스
        private const int BodyBonus = 200;               // 몸통 보너스
        private const int FirstRoundBonus = 500;         // 1라운드에 이기면 받는 라운드 보너스
        private const int RoundBonusStep = 100;          // 라운드가 하나 늦어질 때마다 줄어드는 보너스

        // baseScore : 이 적의 기본 점수 (EnemyDef.BaseScore)
        public static int Calculate(int baseScore, float reactionTime, HitPart part, int round)
        {
            return baseScore + GetSpeedScore(reactionTime) + GetBonus(part) + GetRoundBonus(round);
        }

        // ① 속도 점수 : 1.5초보다 빨랐던 만큼. 1.5초보다 늦으면 음수가 아니라 0 (Max)
        //    소수점은 반올림한다 (0.8234초 → 676.6 → 677)
        public static int GetSpeedScore(float reactionTime)
        {
            float speedScore = Mathf.Max(0f, SpeedLimit - reactionTime) * SpeedScorePerSecond;
            return Mathf.RoundToInt(speedScore);
        }

        // ② 부위 보너스
        public static int GetBonus(HitPart part)
        {
            switch (part)
            {
                case HitPart.Head: return HeadBonus;
                case HitPart.Body: return BodyBonus;
            }

            // HitPart 에 부위를 새로 추가하고 여기를 안 고쳤다
            throw new Exception($"[ScoreCalculator] 보너스가 정해지지 않은 부위입니다 : {part}");
        }

        // ③ 라운드 보너스 : 1라운드 500, 2라운드 400, 3라운드 300, 4라운드 200, 5라운드 100
        //    빨리 끝낼수록 많이 받는다
        public static int GetRoundBonus(int round)
        {
            int roundBonus = FirstRoundBonus - RoundBonusStep * (round - 1);

            // enemies.json 에 라운드를 5개보다 많이 넣었다 → 보너스가 0 이하가 된다
            if (roundBonus <= 0)
                throw new Exception($"[ScoreCalculator] {round}라운드는 라운드 보너스가 {roundBonus} 입니다. 라운드를 늘렸다면 FirstRoundBonus 도 늘려 주세요.");

            return roundBonus;
        }
    }
}
