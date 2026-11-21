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
    //   라운드 하나에서 일어난 일만 담는다. 결투 전체의 일(점수, 가장 빠른 반응)은 DuelResult 에 담는다 (아래)
    public struct RoundResult
    {
        public int Round;              // 몇 라운드였나 (1 ~ 6)
        public RoundOutcome Outcome;
        public HitPart HitPart;        // 플레이어가 맞힌 부위. Outcome 이 PlayerWin 일 때만 의미가 있다
        public float FoulEarly;        // 신호보다 몇 초 먼저 뽑았나. Outcome 이 Foul 일 때만 의미가 있다 (결과판 : "2.3초 먼저 뽑았다")

        // 반응속도 = 신호부터 쏠 때까지 걸린 시간 (초). 기획서 7장 점수 공식의 t
        public bool PlayerFired;           // 플레이어가 이 라운드에 쐈나
        public float PlayerReactionTime;   // PlayerFired 일 때만 의미가 있다
        public bool EnemyFired;            // 적이 이 라운드에 쐈나
        public float EnemyReactionTime;    // EnemyFired 일 때만 의미가 있다
    }

    // 결투 결과 봉투. StageManager 가 결투가 끝날 때 DuelEnded 로 한 번 내보낸다 → 결과판(ResultBoard)이 읽는다
    //   결투 전체의 기록은 StageManager 가 들고 있다가 여기에 담아 준다. 결과판은 보여 주기만 한다.
    //   (교안 Mini Defence 에서 목숨(Lives)은 GameManager 가 들고, HUD 는 받아서 보여 주기만 하는 것과 같다)
    public struct DuelResult
    {
        public RoundResult LastRound;        // 승부가 난 라운드의 봉투 (승리 · 패배 · 반칙. 무승부는 오지 않는다)

        // 가장 빠른 반응 : 플레이어가 쏜 라운드의 반응속도 중 가장 짧은 값 (패배 결과판에 쓴다)
        //   평균은 안 쏜 라운드를 뺄 수밖에 없고, 1 ~ 2발로는 뜻이 약해서 "가장 빠른 값" 을 쓴다
        public bool HasFired;                // 이번 결투에서 한 발이라도 쐈나
        public float FastestReactionTime;    // HasFired 일 때만 의미가 있다

        // 점수 (조각별로 담긴 봉투. 합계는 Score.Total)
        //   LastRound 가 PlayerWin 일 때만 채운다. 지거나 반칙하면 비어 있어서 Total 이 0 (기획서 7장)
        //   StageManager 가 한 번 계산해서 담는다. 결과판은 다시 계산하지 않고 읽기만 한다
        public ScoreBreakdown Score;
    }
}
