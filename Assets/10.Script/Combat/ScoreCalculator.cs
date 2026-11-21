using System;
using UnityEngine;

namespace Game
{
    // 점수 봉투. 점수 조각 4개를 따로 담는다 (ShotInfo · RoundResult 와 같은 struct 봉투)
    //   조각을 따로 담는 이유 : 결과판이 점수 내역을 보여 줄 때 다시 계산하지 않고 이 봉투를 읽기만 하게
    //   이기지 못한 결투는 봉투가 비어 있다 → 조각이 모두 0 이라 Total 도 0
    public struct ScoreBreakdown
    {
        public int BaseScore;    // 기본 점수 (적마다 다르다. EnemyDef.BaseScore)
        public int SpeedScore;   // 속도 점수
        public int PartBonus;    // 부위 보너스
        public int RoundBonus;   // 라운드 보너스

        // 합계는 조각을 더하기만 하면 되므로 따로 적어 두지 않는다 (ShotInfo 의 IsHit 와 같은 모양)
        //   조각과 합계가 어긋날 일이 없다
        public int Total => BaseScore + SpeedScore + PartBonus + RoundBonus;
    }

    // 점수 계산 (기획서 7장)
    //   점수 = S + max(0, 1.5 - t) × 1000 + B + (600 - 100 × (r - 1))
    //   점수를 계산하는 곳은 여기 Calculate 하나뿐이다. 조각 함수들은 밖에서 부르지 않는다 (private)
    public static class ScoreCalculator
    {
        private const float SpeedLimit = 1.5f;           // 이보다 늦게 쏘면 속도 점수가 0
        private const int SpeedScorePerSecond = 1000;    // 1.5초보다 1초 빠를 때마다 +1000
        private const int HeadBonus = 500;               // 머리 보너스
        private const int BodyBonus = 200;               // 몸통 보너스
        private const int FirstRoundBonus = 600;         // 1라운드에 이기면 받는 라운드 보너스 (6라운드 = 리볼버 6발이라 6라운드도 100 이 남게)
        private const int RoundBonusStep = 100;          // 라운드가 하나 늦어질 때마다 줄어드는 보너스

        // baseScore : 이 적의 기본 점수 (EnemyDef.BaseScore)
        public static ScoreBreakdown Calculate(int baseScore, float reactionTime, HitPart part, int round)
        {
            ScoreBreakdown score = new ScoreBreakdown();
            score.BaseScore = baseScore;
            score.SpeedScore = GetSpeedScore(reactionTime);
            score.PartBonus = GetPartBonus(part);
            score.RoundBonus = GetRoundBonus(round);
            return score;
        }

        // ① 속도 점수 : 1.5초보다 빨랐던 만큼. 1.5초보다 늦으면 음수가 아니라 0 (Max)
        //    소수점은 반올림한다 (0.8234초 → 676.6 → 677)
        private static int GetSpeedScore(float reactionTime)
        {
            float speedScore = Mathf.Max(0f, SpeedLimit - reactionTime) * SpeedScorePerSecond;
            return Mathf.RoundToInt(speedScore);
        }

        // ② 부위 보너스
        private static int GetPartBonus(HitPart part)
        {
            switch (part)
            {
                case HitPart.Head: return HeadBonus;
                case HitPart.Body: return BodyBonus;
            }

            // HitPart 에 부위를 새로 추가하고 여기를 안 고쳤다
            throw new Exception($"[ScoreCalculator] 보너스가 정해지지 않은 부위입니다 : {part}");
        }

        // ③ 라운드 보너스 : 1라운드 600, 2라운드 500, 3라운드 400, 4라운드 300, 5라운드 200, 6라운드 100
        //    빨리 끝낼수록 많이 받는다
        private static int GetRoundBonus(int round)
        {
            int roundBonus = FirstRoundBonus - RoundBonusStep * (round - 1);

            // enemies.json 에 라운드를 6개보다 많이 넣었다 → 보너스가 0 이하가 된다
            if (roundBonus <= 0)
                throw new Exception($"[ScoreCalculator] {round}라운드는 라운드 보너스가 {roundBonus} 입니다. 라운드를 늘렸다면 FirstRoundBonus 도 늘려 주세요.");

            return roundBonus;
        }
    }
}
