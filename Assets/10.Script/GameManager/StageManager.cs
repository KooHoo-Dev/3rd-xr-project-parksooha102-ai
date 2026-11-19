using System;
using System.Collections;
using Game.Table;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game
{
    // [스테이지 매니저] 결투 한 판의 라운드 흐름을 돌리고, 지금 무슨 일이 일어났는지 "방송" 한다.
    //
    // 매니저는 적이 누구인지 모른다. (카우보이인지, 몇 초에 쏘는지, 명중률이 얼마인지)
    //   알리는 것은 세 가지뿐이다.
    //     RoundStarted(라운드)   대치 시작      → 적 : 대기 자세로, 이번 라운드 표를 꺼내 둔다
    //     DrawSignaled()         정오 신호(종)  → 적 : 드로우, 자기 격발 시각에 쏜다
    //     RoundEnded(결과)       판정 끝        → 적 : 졌으면 맞은 부위를 잡고 쓰러진다
    //   적이 이걸 듣고 "자기 표대로" 움직인다. 적을 바꿔도 이 파일은 고칠 것이 없다.
    //
    // 누가 누구를 아나? (화살표 = "안다")  — CombatManager 와 같은 방향이다
    //   StageManager ──▶ CombatManager  : PlayerFired 를 구독 (플레이어가 쐈다)
    //   StageManager ──▶ EnemyDuelist   : 직접 만든(Instantiate) 적의 Fired 를 구독 (적이 쐈다)
    //                                     적의 "공통 부품" 만 알고, 어느 캐릭터인지는 모른다
    //   EnemyDuelist ──▶ StageManager   : 세 방송을 구독
    //
    // 한 라운드의 흐름 (기획서 3장, 4.3)
    //   대치 ─(2~5초 랜덤, 1라운드만 5~8초)─▶ 신호 ─▶ 드로우 (최대 3초) ─▶ 판정
    //     먼저 "맞힌" 쪽이 이긴다. 먼저 쏜 쪽이 빗나가면 상대에게 기회가 남는다.
    //     둘 다 빗나가거나 3초가 지나면 무승부 → 다음 라운드
    //
    // 총 잠그기 (기획서 4.2 : 라운드당 한 발, 쏜 뒤에는 다음 라운드까지 트리거가 잠긴다)
    //   대치 시작 → 잠금   신호 → 풀림   한 발 쏨 → 잠금   판정 끝 → 잠금
    //   총을 직접 모르므로 CombatManager 에게 시킨다 (StageManager ──▶ CombatManager ──▶ Gun)
    //
    // 반칙패 (기획서 4.5 : 신호 전에 총을 홀스터에서 빼면 즉시 '비겁자')
    //   대치 중(신호 전)에 CombatManager.PlayerDrew 가 오면 → Foul 로 판정하고 결투를 끝낸다.
    //   결과에는 "신호가 울릴 예정이던 시각 - 뽑은 시각" = 몇 초 먼저 뽑았는지를 담는다.
    //   대치가 시작될 때 총이 손에 있으면 반칙 판정이 안 되므로, 대치 시작에 총을 홀스터에 꽂아 둔다.
    //     (임시. "홀스터에 꽂아야 다음 라운드" 가 생기면 그쪽이 꽂게 된다)
    //
    // 아직 없는 것 (4일차) : 텔레포트 전진, 홀스터에 꽂아야 다음 라운드, 결과판
    //   지금은 무승부 뒤 nextRoundDelay 초가 지나면 저절로 다음 라운드가 시작된다.
    //
    // 왜 [DefaultExecutionOrder(-50)] 인가?
    //   OnEnable 에서 CombatManager.Instance 를 쓰므로 CombatManager(-100) 보다 늦게,
    //   적(기본 0)이 StageManager.Instance 를 구독하기 전에 Instance 가 채워져야 하므로 그보다 먼저.
    //
    // 사용법
    //   ① 빈 오브젝트 StageManager 를 만들고 붙인다. 씬에 하나만 둔다.
    //   ② Player Spot : 플레이어가 1라운드에 서는 자리 (XR Origin 시작 위치). 파란 화살표(Z)가 적 쪽을 보게.
    //      EnemyTester 와 같은 방식이다. 둘 다 적을 만들므로 EnemyTester 는 씬에서 꺼 둔다.
    //   ③ Difficulty : 지금은 Inspector 에서 고른다 (나중에 타이틀 화면이 정한다)
    //
    // 구독하는 법
    //   private void OnEnable()  { StageManager.Instance.RoundStarted += HandleRoundStarted; }
    //   private void OnDisable() { StageManager.Instance.RoundStarted -= HandleRoundStarted; }
    [DefaultExecutionOrder(-50)]
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        [Header("연결")]
        [SerializeField] private Transform playerSpot;

        [Header("설정")]
        [SerializeField] private Difficulty difficulty = Difficulty.Normal;
        [SerializeField] private Vector2 firstSignalDelay = new Vector2(5f, 8f);   // 1라운드 대치 시간 (최소, 최대)
        [SerializeField] private Vector2 signalDelay = new Vector2(2f, 5f);        // 2라운드부터
        [SerializeField] private float drawTimeLimit = 3f;                         // 신호 후 이 시간이 지나면 안 쏜 것으로 본다
        [SerializeField] private float nextRoundDelay = 3f;                        // 무승부 뒤 다음 라운드까지 (임시. 나중엔 텔레포트가 정한다)

        // ---- 방송 ----
        public event Action<int> RoundStarted;         // 대치 시작. 라운드 번호 (1부터)
        public event Action DrawSignaled;              // 정오 신호
        public event Action<RoundResult> RoundEnded;   // 판정 끝

        public int CurrentRound { get; private set; }
        public float SignalTime { get; private set; }  // 신호가 울린 시각 (Time.time). 반응속도 = 쏜 시각 - SignalTime

        private EnemyDef enemyDef;
        private EnemyDuelist enemy;

        // 지금 라운드의 상태
        private bool isStandoff;      // 대치 중이다 (신호 전). 이때 총을 뽑으면 반칙
        private float signalAt;       // 이번 라운드 신호가 울릴 예정 시각 (Time.time)
        private bool isDrawPhase;     // 신호가 울렸고, 아직 판정 전이다 (이때 쏜 총만 센다)
        private bool playerFired;
        private bool enemyFired;
        private RoundOutcome lastOutcome;   // 방금 끝난 라운드의 결과 (무승부면 다음 라운드로)

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            CombatManager.Instance.PlayerFired += HandlePlayerFired;
            CombatManager.Instance.PlayerDrew += HandlePlayerDrew;
        }

        private void OnDisable()
        {
            CombatManager.Instance.PlayerFired -= HandlePlayerFired;
            CombatManager.Instance.PlayerDrew -= HandlePlayerDrew;
            if (enemy != null) enemy.Fired -= HandleEnemyFired;   // 적을 만들기 전에 꺼질 수도 있다
        }

        private void Start()
        {
            SpawnEnemy();
            StartCoroutine(DuelRoutine());
        }

        // 표에서 이번 난이도의 적을 꺼내 만든다. 매니저가 아는 것은 "EnemyDuelist 가 붙어 있다" 뿐이다
        private void SpawnEnemy()
        {
            enemyDef = DataManager.Instance.GetEnemyDef(difficulty);
            GameObject enemyObject = Instantiate(enemyDef.Prefab);

            enemy = enemyObject.GetComponent<EnemyDuelist>();
            if (enemy == null)
                throw new Exception($"[StageManager] {enemyDef.Prefab.name} 에 EnemyDuelist 가 없습니다. 적 프리팹 맨 위에 EnemyDuelist 를 붙여 주세요.");

            enemy.Init(enemyDef);
            enemy.Fired += HandleEnemyFired;
        }

        // 결투 한 판. 라운드를 하나씩 돌고, 승부가 나면 끝낸다
        private IEnumerator DuelRoutine()
        {
            for (int round = 1; round <= enemyDef.RoundCount; round++)
            {
                // ① 대치
                CurrentRound = round;
                PlaceEnemy(round);
                playerFired = false;
                enemyFired = false;
                CombatManager.Instance.LockPlayerGun();     // 신호 전에는 쏠 수 없다
                // 총을 홀스터에서 시작해야 "신호 전에 뽑음" 을 알 수 있다
                //   1라운드는 소켓의 Starting Selected Interactable 이 이미 꽂아 둔다.
                //   (StageManager 의 Start 가 소켓의 Start 보다 먼저 돌아서, 여기서 꽂으면 소켓과 겹친다)
                if (round > 1)
                    CombatManager.Instance.HolsterPlayerGun();
                Debug.Log($"[StageManager] {round}라운드 대치");
                RoundStarted?.Invoke(round);

                // 신호 시각을 미리 정해 두고, 그때까지 기다린다
                //   WaitForSeconds 대신 한 프레임씩 기다리는 이유 : 기다리는 도중 반칙이 나면 바로 멈춰야 해서
                Vector2 delay = round == 1 ? firstSignalDelay : signalDelay;
                signalAt = Time.time + Random.Range(delay.x, delay.y);
                isStandoff = true;
                while (isStandoff && Time.time < signalAt)
                    yield return null;

                // 대치 중에 반칙으로 끝났다 → 결투 끝
                if (isStandoff == false)
                    yield break;
                isStandoff = false;

                // ② 신호
                SignalTime = Time.time;
                isDrawPhase = true;
                CombatManager.Instance.UnlockPlayerGun();   // 이 라운드의 한 발
                Debug.Log($"[StageManager] {round}라운드 신호!");
                DrawSignaled?.Invoke();

                // ③ 판정이 날 때까지 기다린다. 총을 쏘면 HandlePlayerFired / HandleEnemyFired 가 판정한다
                while (isDrawPhase && Time.time - SignalTime < drawTimeLimit)
                    yield return null;

                // 3초 동안 승부가 안 났으면 무승부 (안 쏜 쪽은 쏘지 않은 것으로 본다)
                if (isDrawPhase)
                    EndRound(RoundOutcome.Draw, HitPart.Body);

                // ④ 승부가 났으면 끝, 무승부면 잠시 뒤 다음 라운드
                if (lastOutcome != RoundOutcome.Draw)
                    yield break;
                yield return new WaitForSeconds(nextRoundDelay);
            }
        }

        // 라운드마다 적 자리 : 플레이어와 적이 0.5m 씩 같이 다가서므로,
        //   플레이어 = 출발점 + (1라운드 거리 - 이번 거리) / 2,  적 = 플레이어 + 이번 거리
        //   → 적 = 출발점 + (1라운드 거리 + 이번 거리) / 2
        //   (텔레포트가 생기기 전에는 플레이어가 출발점에 있으므로, 실제 거리는 이것보다 조금 멀다)
        private void PlaceEnemy(int round)
        {
            float firstDistance = enemyDef.GetRound(1).Distance;
            float distance = enemyDef.GetRound(round).Distance;
            Vector3 position = playerSpot.position + playerSpot.forward * ((firstDistance + distance) / 2f);
            Quaternion rotation = Quaternion.LookRotation(-playerSpot.forward);   // 플레이어를 바라보게
            enemy.transform.SetPositionAndRotation(position, rotation);
        }

        // 플레이어가 쐈다 (CombatManager 가 알려 줌)
        private void HandlePlayerFired(ShotInfo shot)
        {
            // 신호 전, 판정 후에는 총이 잠겨 있어서 여기 오지 않는다.
            // 그래도 잠금 밖에서 Fire() 를 직접 부른 총알(테스터 등)은 판정에 넣지 않는다.
            if (isDrawPhase == false) return;

            playerFired = true;
            CombatManager.Instance.LockPlayerGun();   // 한 발 쐈으니 다음 라운드까지 잠금
            if (shot.IsHit)
                EndRound(RoundOutcome.PlayerWin, shot.Zone.Part);
            else if (enemyFired)
                EndRound(RoundOutcome.Draw, HitPart.Body);   // 적은 이미 빗나갔고 나도 빗나감
        }

        // 플레이어가 홀스터에서 총을 뽑았다 (CombatManager 가 알려 줌)
        private void HandlePlayerDrew()
        {
            // 신호 뒤에 뽑는 것은 정상이다. 대치 중(신호 전)에 뽑았을 때만 반칙
            if (isStandoff == false) return;

            isStandoff = false;   // DuelRoutine 의 기다림을 멈춘다
            EndRound(RoundOutcome.Foul, HitPart.Body);
        }

        // 적이 쐈다 (적이 자기 격발 시각에 알려 줌)
        private void HandleEnemyFired(bool isHit)
        {
            if (isDrawPhase == false) return;

            enemyFired = true;
            if (isHit)
                EndRound(RoundOutcome.EnemyWin, HitPart.Body);
            else if (playerFired)
                EndRound(RoundOutcome.Draw, HitPart.Body);
        }

        // 판정 끝 → 방송
        //   hitPart 는 PlayerWin 일 때만 쓰인다. 다른 결과에서는 아무 값이나 넣는다
        private void EndRound(RoundOutcome outcome, HitPart hitPart)
        {
            isDrawPhase = false;
            lastOutcome = outcome;
            CombatManager.Instance.LockPlayerGun();   // 안 쏘고 끝났어도 (적이 먼저 맞힘, 시간 초과) 잠근다

            RoundResult result = new RoundResult();
            result.Round = CurrentRound;
            result.Outcome = outcome;
            result.HitPart = hitPart;
            if (outcome == RoundOutcome.Foul)
                result.FoulEarly = signalAt - Time.time;   // 신호 예정 시각까지 남은 시간 = 몇 초 먼저 뽑았나

            string detail = "";
            if (outcome == RoundOutcome.PlayerWin) detail = $" ({hitPart})";
            if (outcome == RoundOutcome.Foul) detail = $" (신호보다 {result.FoulEarly:0.00}초 먼저 뽑음)";
            Debug.Log($"[StageManager] {CurrentRound}라운드 판정 : {outcome}{detail}");
            RoundEnded?.Invoke(result);
        }
    }
}
