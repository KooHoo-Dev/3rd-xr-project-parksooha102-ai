namespace Game
{
    // 라운드가 어떻게 끝났나 (기획서 4.3 라운드 판정)
    public enum RoundOutcome
    {
        PlayerWin,   // 플레이어가 먼저 맞힘 → 적이 쓰러진다
        EnemyWin,    // 적이 먼저 맞힘
        Draw,        // 둘 다 빗나감 (또는 시간 초과) → 한 걸음 전진 후 다음 라운드
        Foul,        // 신호 전에 총을 뽑음 → 반칙패 ('비겁자', 기획서 4.5)
    }

    // 라운드 결과 봉투. StageManager 가 RoundEnded 로 내보낸다. (ShotInfo 와 같은 struct 봉투)
    public struct RoundResult
    {
        public int Round;              // 몇 라운드였나 (1 ~ 5)
        public RoundOutcome Outcome;
        public HitPart HitPart;        // 플레이어가 맞힌 부위. Outcome 이 PlayerWin 일 때만 의미가 있다
        public float FoulEarly;        // 신호보다 몇 초 먼저 뽑았나. Outcome 이 Foul 일 때만 의미가 있다 (결과판 : "2.3초 먼저 뽑았다")
    }
}
