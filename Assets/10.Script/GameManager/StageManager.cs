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
    // 아직 없는 것 (4일차) : 반칙패(신호 전 드로우), 텔레포트 전진, 홀스터에 꽂아야 다음 라운드, 결과판
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
        }

        private void OnDisable()
        {
            CombatManager.Instance.PlayerFired -= HandlePlayerFired;
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
                Debug.Log($"[StageManager] {round}라운드 대치");
                RoundStarted?.Invoke(round);

                Vector2 delay = round == 1 ? firstSignalDelay : signalDelay;
                yield return new WaitForSeconds(Random.Range(delay.x, delay.y));

                // ② 신호
                SignalTime = Time.time;
                isDrawPhase = true;
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
            // 신호 전, 판정 후에 쏜 총은 세지 않는다 (신호 전 드로우 = 반칙은 4일차에 홀스터로 판정한다)
            if (isDrawPhase == false) return;

            playerFired = true;
            if (shot.IsHit)
                EndRound(RoundOutcome.PlayerWin, shot.Zone.Part);
            else if (enemyFired)
                EndRound(RoundOutcome.Draw, HitPart.Body);   // 적은 이미 빗나갔고 나도 빗나감
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

            RoundResult result = new RoundResult();
            result.Round = CurrentRound;
            result.Outcome = outcome;
            result.HitPart = hitPart;

            Debug.Log($"[StageManager] {CurrentRound}라운드 판정 : {outcome}" + (outcome == RoundOutcome.PlayerWin ? $" ({hitPart})" : ""));
            RoundEnded?.Invoke(result);
        }
    }
}
