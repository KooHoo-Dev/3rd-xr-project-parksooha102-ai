using System;

namespace Game
{
    // 결과판 칭호 (기획서 7장). 수배지에 숫자 대신 별명을 적는다
    //   반응속도 0.85초 → "눈 깜짝할 새"
    //   enum 은 "어느 칸에 들었나" 만 정하고, 글자는 DuelTitles.GetName 이 꺼내 준다 (HitPartNames 와 같은 모양)

    // 반응속도 칭호. 승리의 반응속도, 패배의 가장 빠른 반응에 같이 쓴다
    public enum ReactionTitle
    {
        Lightning,   // 0.6초 미만
        Quick,       // 0.9초 미만
        Steady,      // 1.2초 미만
        Slow,        // 1.5초 미만
        Rusty,       // 1.5초 이상
        Frozen,      // 한 발도 쏘지 못함 (패배에서만 나온다)
    }

    // 명중 부위 칭호 (승리)
    public enum HitTitle
    {
        Marksman,    // 머리
        Center,      // 몸통
    }

    // 끝낸 라운드 칭호 (승리)
    public enum WinRoundTitle
    {
        FirstShot,   // 1라운드
        Exchange,    // 그 사이
        CloseRange,  // 마지막 두 라운드
    }

    // 쓰러진 라운드 칭호 (패배)
    public enum LoseRoundTitle
    {
        FirstBlood,  // 1라운드
        Fallen,      // 그 사이
        LastStand,   // 마지막 라운드
    }

    // 먼저 뽑은 시간 칭호 (반칙)
    public enum FoulTitle
    {
        Twitchy,     // 0.3초 미만
        Impatient,   // 1.0초 미만
        Backstabber, // 1.0초 이상
    }

    // 반칙한 라운드 칭호 (반칙)
    public enum FoulRoundTitle
    {
        NoGreeting,   // 1라운드
        LostPatience, // 2라운드부터
    }

    // 칭호 고르기 + 칭호 글자. ScoreCalculator 처럼 기억할 것이 없어서 static 이다
    //   ReactionTitle title = DuelTitles.GetReaction(true, 0.85f);   // → Quick
    //   string name = DuelTitles.GetName(title);                     // → "눈 깜짝할 새"
    public static class DuelTitles
    {
        // 반응속도 칸 경계 (초). 모두 "미만" 으로 비교한다 → 0.9초 정각은 Quick 이 아니라 Steady
        private const float LightningLimit = 0.6f;
        private const float QuickLimit = 0.9f;
        private const float SteadyLimit = 1.2f;
        private const float SlowLimit = 1.5f;

        // 먼저 뽑은 시간 칸 경계 (초)
        private const float TwitchyLimit = 0.3f;
        private const float ImpatientLimit = 1.0f;

        // ── 칭호 고르기 ─────────────────────────────

        // fired : 쐈나. 안 쐈으면 reactionTime 은 보지 않는다
        public static ReactionTitle GetReaction(bool fired, float reactionTime)
        {
            if (fired == false) return ReactionTitle.Frozen;
            if (reactionTime < LightningLimit) return ReactionTitle.Lightning;
            if (reactionTime < QuickLimit) return ReactionTitle.Quick;
            if (reactionTime < SteadyLimit) return ReactionTitle.Steady;
            if (reactionTime < SlowLimit) return ReactionTitle.Slow;
            return ReactionTitle.Rusty;
        }

        public static HitTitle GetHit(HitPart part)
        {
            switch (part)
            {
                case HitPart.Head: return HitTitle.Marksman;
                case HitPart.Body: return HitTitle.Center;
            }

            // HitPart 에 부위를 새로 추가하고 여기를 안 고쳤다
            throw new Exception($"[DuelTitles] 칭호가 정해지지 않은 부위입니다 : {part}");
        }

        // 라운드 칭호는 "마지막 라운드에서 몇 번째 전인가" 로 고른다
        //   라운드 수는 enemies.json 에 있어서 (EnemyDef.RoundCount), 라운드를 늘려도 여기를 고칠 필요가 없다
        public static WinRoundTitle GetWinRound(int round, int roundCount)
        {
            if (round == 1) return WinRoundTitle.FirstShot;
            if (round >= roundCount - 1) return WinRoundTitle.CloseRange;
            return WinRoundTitle.Exchange;
        }

        public static LoseRoundTitle GetLoseRound(int round, int roundCount)
        {
            if (round == 1) return LoseRoundTitle.FirstBlood;
            if (round == roundCount) return LoseRoundTitle.LastStand;
            return LoseRoundTitle.Fallen;
        }

        // foulEarly : 신호보다 몇 초 먼저 뽑았나 (RoundResult.FoulEarly)
        public static FoulTitle GetFoul(float foulEarly)
        {
            if (foulEarly < TwitchyLimit) return FoulTitle.Twitchy;
            if (foulEarly < ImpatientLimit) return FoulTitle.Impatient;
            return FoulTitle.Backstabber;
        }

        public static FoulRoundTitle GetFoulRound(int round)
        {
            if (round == 1) return FoulRoundTitle.NoGreeting;
            return FoulRoundTitle.LostPatience;
        }

        // ── 칭호 글자 ─────────────────────────────
        //   글자를 바꾸고 싶으면 여기만 고친다

        public static string GetName(ReactionTitle title)
        {
            switch (title)
            {
                case ReactionTitle.Lightning: return "전광석화";
                case ReactionTitle.Quick: return "눈 깜짝할 새";
                case ReactionTitle.Steady: return "조준의 정석";
                case ReactionTitle.Slow: return "굼뜬 손";
                case ReactionTitle.Rusty: return "낡디 낡은 실력";
                case ReactionTitle.Frozen: return "허리춤의 장식품";
            }
            throw new Exception($"[DuelTitles] 글자가 정해지지 않은 칭호입니다 : {title}");
        }

        public static string GetName(HitTitle title)
        {
            switch (title)
            {
                case HitTitle.Marksman: return "명사수";
                case HitTitle.Center: return "넓은 표적";
            }
            throw new Exception($"[DuelTitles] 글자가 정해지지 않은 칭호입니다 : {title}");
        }

        public static string GetName(WinRoundTitle title)
        {
            switch (title)
            {
                case WinRoundTitle.FirstShot: return "원샷 원킬";
                case WinRoundTitle.Exchange: return "치열한 공방";
                case WinRoundTitle.CloseRange: return "최후의 최후까지";
            }
            throw new Exception($"[DuelTitles] 글자가 정해지지 않은 칭호입니다 : {title}");
        }

        public static string GetName(LoseRoundTitle title)
        {
            switch (title)
            {
                case LoseRoundTitle.FirstBlood: return "불운";
                case LoseRoundTitle.Fallen: return "몇 발은 버틴";
                case LoseRoundTitle.LastStand: return "오징어 게임";
            }
            throw new Exception($"[DuelTitles] 글자가 정해지지 않은 칭호입니다 : {title}");
        }

        public static string GetName(FoulTitle title)
        {
            switch (title)
            {
                case FoulTitle.Twitchy: return "근질근질한 손가락";
                case FoulTitle.Impatient: return "참을성 없는 근성";
                case FoulTitle.Backstabber: return "극악무도한 무법자";
            }
            throw new Exception($"[DuelTitles] 글자가 정해지지 않은 칭호입니다 : {title}");
        }

        public static string GetName(FoulRoundTitle title)
        {
            switch (title)
            {
                case FoulRoundTitle.NoGreeting: return "총알 인사";
                case FoulRoundTitle.LostPatience: return "안절부절";
            }
            throw new Exception($"[DuelTitles] 글자가 정해지지 않은 칭호입니다 : {title}");
        }
    }
}
