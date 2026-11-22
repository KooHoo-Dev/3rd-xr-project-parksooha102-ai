using System;
using System.Collections;
using Game.Table;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Game
{
    // 한 라운드가 지금 어느 단계인가 (Ready → Standoff → Draw → Judged)
    //   Ready 를 맨 앞에 둔다. enum 의 기본값은 맨 앞 이름이라, 결투 전에 총을 뽑아도 반칙이 아니게 된다
    public enum DuelPhase
    {
        Ready,      // 결투 시작 전. 쏘거나 뽑아도 판정하지 않는다
        Standoff,   // 대치 (신호 전). 이때 총을 뽑으면 반칙
        Draw,       // 신호가 울렸고 아직 판정 전. 이때 쏜 총만 센다
        Judged,     // 판정 끝. 무승부면 꽂기 · 전진을 기다리고, 승부가 났으면 결과판으로 간다
    }

    // [스테이지 매니저] 결투 한 판의 라운드 흐름을 돌리고, 지금 무슨 일이 일어났는지 "방송" 한다.
    //   적은 내가 만들어서 들고 있으므로 방송하지 않고 직접 시킨다 (적은 StageManager 를 모른다)
    [DefaultExecutionOrder(-50)]   // CombatManager(-100) 다음, 해 · 쪽지 · 결과판(기본 0) 보다 먼저 Instance 를 채운다
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        // 결투 출발점 : 월드 원점(0, 0, 0)에 서서 +Z 쪽(적 쪽)을 본다. Town 맵이 이 줄에 맞춰 지어져 있다
        //   플레이어(XR Origin)를 기준으로 삼지 않는다. 플레이어가 어디로 움직였든 적 · 표식은 맵의 같은 자리에 선다
        //   (Vector3 는 const 로 못 써서 static readonly 로 둔다. EnemyDuelist 의 IsAiming 과 같은 모양)
        private static readonly Vector3 DuelStartPosition = Vector3.zero;
        private static readonly Vector3 DuelForward = Vector3.forward;

        // 바닥 찾기 레이 : 놓을 자리에서 이만큼 위에서 출발해, 이 길이만큼 아래로 쏜다
        //   (출발점보다 5m 높은 땅부터 15m 낮은 땅까지 찾을 수 있다)
        private const float GroundRayHeight = 5f;
        private const float GroundRayLength = 20f;

        // 메인화면 씬 이름. Build Profiles 의 Scene List 에 들어 있어야 열 수 있다
        private const string MainMenuSceneName = "Title";

        [Header("연결")]
        [SerializeField] private PlayerPlacer playerPlacer;   // 플레이어를 결투 자리에 세운다 (어디에 세울지는 내가 알려 준다)
        [SerializeField] private AdvanceMarker advanceMarker;

        [Header("설정")]
        [SerializeField] private LayerMask groundLayer;                            // 바닥 레이어 (Ground). 플레이어 · 적 · 전진 표식을 이 바닥 높이에 세운다
        [SerializeField] private Vector2 firstSignalDelay = new Vector2(5f, 8f);   // 1라운드 대치 시간 (최소, 최대)
        [SerializeField] private Vector2 signalDelay = new Vector2(2f, 5f);        // 2라운드부터
        [SerializeField] private float drawTimeLimit = 3f;                         // 신호 후 이 시간이 지나면 안 쏜 것으로 본다
        [SerializeField] private float holsterTimeLimit = 5f;                      // 무승부 뒤 이 시간 안에 안 꽂으면 자동으로 꽂는다
        [SerializeField] private float resultDelay = 2f;                           // 승부가 나고 결과판이 뜨기까지 (쓰러지는 모습을 볼 시간)

        // ---- 방송 ----
        public event Action<int> RoundStarted;         // 대치 시작. 라운드 번호 (1부터)
        public event Action DrawSignaled;              // 정오 신호
        public event Action<RoundResult> RoundEnded;   // 판정 끝
        public event Action<DuelResult> DuelEnded;     // 결투 끝. 점수 · 가장 빠른 반응 · 승부가 난 라운드

        public int CurrentRound { get; private set; }
        public DuelPhase Phase { get; private set; }   // 지금 단계 (처음에는 Ready)
        public float SignalTime { get; private set; }  // 신호가 울린 시각 (Time.time). 반응속도 = 쏜 시각 - SignalTime
        public float SignalAt { get; private set; }    // 이번 라운드 신호가 울릴 예정 시각 (Time.time). RoundStarted 를 방송할 때 이미 정해져 있다

        private EnemyDef enemyDef;
        private EnemyDuelist enemy;

        // 지금 라운드의 상태
        private bool playerFired;
        private bool enemyFired;
        private float playerReactionTime;   // 신호부터 플레이어가 쏠 때까지 (초). playerFired 일 때만 의미가 있다
        private float enemyReactionTime;    // 신호부터 적이 쏠 때까지 (초). enemyFired 일 때만 의미가 있다
        private RoundResult lastResult;     // 방금 끝난 라운드의 결과 (무승부면 다음 라운드로, 아니면 결과판으로)
        private bool isAdvanced;            // 전진 표식으로 이동했다

        // 결투 전체의 기록 (라운드가 바뀌어도 지우지 않는다. 다시 결투는 씬을 다시 열므로 저절로 처음 값이 된다)
        private bool hasFiredInDuel;          // 이번 결투에서 한 발이라도 쐈나
        private float fastestReactionTime;    // 쏜 라운드 중 가장 빠른 반응속도 (초). hasFiredInDuel 일 때만 의미가 있다

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            CombatManager.Instance.PlayerFired += HandlePlayerFired;
            CombatManager.Instance.PlayerDrew += HandlePlayerDrew;
            advanceMarker.Advanced += HandleAdvanced;
        }

        private void OnDisable()
        {
            CombatManager.Instance.PlayerFired -= HandlePlayerFired;
            CombatManager.Instance.PlayerDrew -= HandlePlayerDrew;
            advanceMarker.Advanced -= HandleAdvanced;
            if (enemy != null) enemy.Fired -= HandleEnemyFired;   // 적을 만들기 전에 꺼질 수도 있다
        }

        private void Start()
        {
            SpawnEnemy();
            advanceMarker.Hide();   // 표식은 무승부 뒤에만 켠다
            StartCoroutine(DuelRoutine());
        }

        // 표에서 이번 난이도의 적을 꺼내 만든다. 매니저가 아는 것은 "EnemyDuelist 가 붙어 있다" 뿐이다
        //   난이도는 메인화면에서 고른 값 (GameSettings). Town 을 바로 Play 하면 보통
        private void SpawnEnemy()
        {
            enemyDef = DataManager.Instance.GetEnemyDef(GameSettings.Difficulty);
            Debug.Log($"[StageManager] {enemyDef.Difficulty} 난이도 적 등장");
            GameObject enemyObject = Instantiate(enemyDef.Prefab);

            enemy = enemyObject.GetComponent<EnemyDuelist>();
            if (enemy == null)
                throw new Exception($"[StageManager] {enemyDef.Prefab.name} 에 EnemyDuelist 가 없습니다. 적 프리팹 맨 위에 EnemyDuelist 를 붙여 주세요.");

            enemy.Fired += HandleEnemyFired;
        }

        // 결투 한 판. 라운드를 하나씩 돌고, 승부가 나면 for 를 빠져나가 결과판을 띄운다
        private IEnumerator DuelRoutine()
        {
            // ⓪ 플레이어를 결투 출발점에 세우고 적 쪽을 보게 한다
            //    켜자마자는 헤드셋 추적이 아직 안 잡혔을 수 있어서 (그러면 엉뚱한 자리를 기준으로 옮긴다),
            //    PlayerPlacer 가 추적이 잡힐 때까지 기다렸다가 세운다. 그동안 1라운드는 시작하지 않는다
            yield return playerPlacer.PlaceFirst(GetGroundPosition(DuelStartPosition), Quaternion.LookRotation(DuelForward));

            //    적도 1라운드 자리에 세운 다음 암전을 걷는다 (자리 잡는 모습이 안 보이게)
            PlaceEnemy(1);
            ScreenFader.FadeIn();

            for (int round = 1; round <= enemyDef.RoundCount; round++)
            {
                // ① 대치
                CurrentRound = round;
                PlaceEnemy(round);
                playerFired = false;
                enemyFired = false;
                playerReactionTime = 0f;
                enemyReactionTime = 0f;
                CombatManager.Instance.LockPlayerGun();     // 신호 전에는 쏠 수 없다
                // 총은 홀스터에 꽂힌 채로 대치를 시작한다 (그래야 "신호 전에 뽑음" 을 알 수 있다)
                //   1라운드 : 소켓의 Starting Selected Interactable 이 꽂아 둔다
                //   2라운드부터 : 앞 라운드 ⑤ WaitForHolster 가 꽂힌 것을 확인하고 넘어온다
                Debug.Log($"[StageManager] {round}라운드 대치");

                // 신호 시각을 미리 정해 둔다
                //   RoundStarted 보다 먼저 정하는 이유 : 정오의 해(NoonSun)가 방송을 듣자마자 "신호가 언제인지" 알아야 해서
                Vector2 delay = round == 1 ? firstSignalDelay : signalDelay;
                SignalAt = Time.time + Random.Range(delay.x, delay.y);
                enemy.BeginRound(enemyDef.GetRound(round));   // 적에게는 이번 라운드 줄을 건네며 직접 시킨다
                RoundStarted?.Invoke(round);

                // 신호 시각까지 기다린다
                //   WaitForSeconds 대신 한 프레임씩 기다리는 이유 : 기다리는 도중 반칙이 나면 바로 멈춰야 해서
                Phase = DuelPhase.Standoff;
                while (Phase == DuelPhase.Standoff && Time.time < SignalAt)
                    yield return null;

                // 대치 중에 반칙으로 끝났다 (HandlePlayerDrew → EndRound 가 Judged 로 바꿨다)
                //   → 결투 끝 (for 를 빠져나가 ⑦ 결과판으로)
                if (Phase == DuelPhase.Judged)
                    break;

                // ② 신호
                SignalTime = Time.time;
                Phase = DuelPhase.Draw;
                CombatManager.Instance.UnlockPlayerGun();   // 이 라운드의 한 발
                Debug.Log($"[StageManager] {round}라운드 신호!");
                enemy.Draw(playerPlacer.Head);   // 적이 뽑고, 자기 격발 시각에 플레이어 쪽으로 쏜다 (쏘면 HandleEnemyFired 로 온다)
                DrawSignaled?.Invoke();

                // ③ 판정이 날 때까지 기다린다. 총을 쏘면 HandlePlayerFired / HandleEnemyFired 가 판정한다
                while (Phase == DuelPhase.Draw && Time.time - SignalTime < drawTimeLimit)
                    yield return null;

                // 3초 동안 승부가 안 났으면 무승부 (안 쏜 쪽은 쏘지 않은 것으로 본다)
                if (Phase == DuelPhase.Draw)
                    EndRound(RoundOutcome.Draw, HitPart.Body);

                // ④ 승부가 났으면 결투 끝 (for 를 빠져나가 ⑦ 결과판으로)
                if (lastResult.Outcome != RoundOutcome.Draw)
                    break;

                // 마지막 라운드는 적 명중률이 100% 라서 무승부가 날 수 없다 (기획서 3장)
                //   여기 왔다면 표가 잘못된 것이다. 결과판도 안 뜨고 멈춰 있지 않게 바로 알린다
                if (round == enemyDef.RoundCount)
                    throw new Exception($"[StageManager] 마지막 {round}라운드가 무승부로 끝났습니다. enemies.json 의 {enemyDef.Difficulty} 마지막 라운드가 HitRate 1, FireTime {drawTimeLimit}초 미만인지 확인해 주세요.");

                // ⑤ 무승부 → 총을 홀스터에 꽂아야 다음으로
                yield return WaitForHolster();

                // ⑥ 전진 표식으로 이동해야 다음 라운드
                yield return WaitForAdvance(round + 1);
            }

            // ⑦ 결투 끝 → 쓰러지는 모습을 잠깐 보여 준 뒤 결과판
            yield return new WaitForSeconds(resultDelay);

            //    총을 홀스터에 돌려놓는다. 손이 비어야 레이로 버튼을 누르기 쉽다
            //    드로우 중에 떨어뜨린 총도 이때 돌아온다 (기획서 9장)
            CombatManager.Instance.HolsterPlayerGun();

            EndDuel();
        }

        // 다음 라운드 자리에 전진 표식을 켜고, 플레이어가 그리로 이동할 때까지 기다린다
        private IEnumerator WaitForAdvance(int nextRound)
        {
            // ① 표식을 다음 라운드 플레이어 자리에 켠다 (거리가 1m 씩 줄면 지금 자리에서 0.5m 앞)
            //    표식이 적 쪽을 보게 놓는다 → 도착한 플레이어도 적 쪽을 보게 된다 (기획서 8장)
            isAdvanced = false;
            advanceMarker.Show(GetPlayerPosition(nextRound), Quaternion.LookRotation(DuelForward));
            Debug.Log("[StageManager] 전진 표식으로 텔레포트하면 다음 라운드");

            // ② 이동할 때까지 기다린다 (HandleAdvanced 가 isAdvanced 를 켠다)
            while (isAdvanced == false)
                yield return null;

            // ③ 표식을 기다리는 동안 총을 다시 뽑았을 수도 있다 → 꽂고 시작한다 (이미 꽂혀 있으면 아무 일 없음)
            CombatManager.Instance.HolsterPlayerGun();
        }

        // 플레이어가 전진 표식으로 이동했다 (AdvanceMarker 가 알려 줌)
        private void HandleAdvanced()
        {
            isAdvanced = true;

            // 이제부터 설 자리는 이 표식 자리 (꺼졌어도 자리는 그대로 남아 있다). 위치 초기화하면 PlayerPlacer 가 여기에 다시 세운다
            playerPlacer.SetStandPose(advanceMarker.transform.position, advanceMarker.transform.rotation);
        }

        // 총이 홀스터에 꽂힐 때까지 기다린다. holsterTimeLimit 초가 지나면 대신 꽂아 준다
        //   기다리는 동안 총을 뽑았다 꽂았다 해도 괜찮다 (Phase 가 Judged 라서 반칙이 아니다)
        //   안 뽑고 버틴 라운드(시간 초과 무승부)는 이미 꽂혀 있으므로 바로 넘어간다
        private IEnumerator WaitForHolster()
        {
            // ① 꽂히거나 시간이 다 될 때까지 한 프레임씩 기다린다
            float waitStart = Time.time;
            Debug.Log($"[StageManager] 무승부. 총을 홀스터에 꽂으면 다음 라운드 ({holsterTimeLimit}초 뒤 자동)");
            while (CombatManager.Instance.IsPlayerGunHolstered() == false && Time.time - waitStart < holsterTimeLimit)
                yield return null;

            // ② 끝까지 안 꽂았으면 대신 꽂는다 (기획서 5장 : 무승부 뒤 총을 안 꽂음 → 5초 뒤 자동으로 꽂는다)
            //    손에 들고 있어도, 바닥에 떨어져 있어도 PutBack 이 홀스터로 가져온다
            if (CombatManager.Instance.IsPlayerGunHolstered() == false)
            {
                Debug.Log("[StageManager] 시간이 지나 총을 자동으로 꽂았습니다.");
                CombatManager.Instance.HolsterPlayerGun();
            }
        }

        // 라운드마다 플레이어 자리 : 플레이어와 적이 같이 다가서서 줄어든 거리를 반씩 나눠 걷는다
        //   플레이어 = 출발점 + (1라운드 거리 - 이번 거리) / 2
        //   예) 10m → 9m 면 플레이어가 0.5m, 적이 0.5m 앞으로
        private Vector3 GetPlayerPosition(int round)
        {
            float firstDistance = enemyDef.GetRound(1).Distance;
            float distance = enemyDef.GetRound(round).Distance;
            Vector3 position = DuelStartPosition + DuelForward * ((firstDistance - distance) / 2f);
            return GetGroundPosition(position);   // 바닥이 울퉁불퉁하니 그 자리 땅 높이에 맞춘다
        }

        // 라운드마다 적 자리 : 적 = 플레이어 + 이번 거리
        //   → 적 = 출발점 + (1라운드 거리 + 이번 거리) / 2
        //   (플레이어가 방 안에서 걸어 다닐 수 있으므로, 실제 거리는 이것과 조금 다를 수 있다)
        private void PlaceEnemy(int round)
        {
            float firstDistance = enemyDef.GetRound(1).Distance;
            float distance = enemyDef.GetRound(round).Distance;
            Vector3 position = DuelStartPosition + DuelForward * ((firstDistance + distance) / 2f);
            position = GetGroundPosition(position);   // 바닥이 울퉁불퉁하니 그 자리 땅 높이에 맞춘다 (안 그러면 뜨거나 묻힌다)
            Quaternion rotation = Quaternion.LookRotation(-DuelForward);   // 플레이어를 바라보게
            enemy.transform.SetPositionAndRotation(position, rotation);
        }

        // position 바로 위에서 아래로 레이를 쏴서, 땅에 닿은 자리를 돌려준다 (x, z 는 그대로, y 만 땅 높이로)
        //   groundLayer 만 맞히므로 적 몸통 · 머리 콜라이더나 소품에는 걸리지 않는다
        private Vector3 GetGroundPosition(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * GroundRayHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, GroundRayLength, groundLayer) == false)
                throw new Exception($"[StageManager] {position} 아래에서 바닥을 못 찾았습니다. StageManager 의 Ground Layer 를 골랐는지, 그 자리 바닥 타일의 Layer 가 Ground 인지 확인해 주세요.");

            return hit.point;
        }

        // 플레이어가 쐈다 (CombatManager 가 알려 줌)
        private void HandlePlayerFired(ShotInfo shot)
        {
            // 신호 전, 판정 후에는 총이 잠겨 있어서 여기 오지 않는다.
            // 그래도 잠금 밖에서 Fire() 를 직접 부른 총알(테스터 등)은 판정에 넣지 않는다.
            if (Phase != DuelPhase.Draw) return;

            playerFired = true;
            playerReactionTime = shot.FireTime - SignalTime;   // 반응속도 = 쏜 시각 - 신호 시각

            // 결투 전체에서 가장 빠른 반응만 기억해 둔다 (첫 발이거나, 지금까지보다 빠르면 바꾼다)
            if (hasFiredInDuel == false || playerReactionTime < fastestReactionTime)
                fastestReactionTime = playerReactionTime;
            hasFiredInDuel = true;

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
            if (Phase != DuelPhase.Standoff) return;

            // EndRound 가 Phase 를 Judged 로 바꾼다 → DuelRoutine 의 대치 기다림이 멈춘다
            EndRound(RoundOutcome.Foul, HitPart.Body);
        }

        // 적이 쐈다 (적이 자기 격발 시각에 알려 줌)
        private void HandleEnemyFired(bool isHit)
        {
            if (Phase != DuelPhase.Draw) return;

            enemyFired = true;
            enemyReactionTime = Time.time - SignalTime;   // 적은 쏘는 그 순간 알려 주므로 지금 시각이 쏜 시각이다
            if (isHit)
                EndRound(RoundOutcome.EnemyWin, HitPart.Body);
            else if (playerFired)
                EndRound(RoundOutcome.Draw, HitPart.Body);
        }

        // 판정 끝 → 방송
        //   hitPart 는 PlayerWin 일 때만 쓰인다. 다른 결과에서는 아무 값이나 넣는다
        private void EndRound(RoundOutcome outcome, HitPart hitPart)
        {
            Phase = DuelPhase.Judged;   // 이제부터 쏘거나 뽑아도 판정하지 않는다
            CombatManager.Instance.LockPlayerGun();   // 안 쏘고 끝났어도 (적이 먼저 맞힘, 시간 초과) 잠근다

            RoundResult result = new RoundResult();
            result.Round = CurrentRound;
            result.Outcome = outcome;
            result.HitPart = hitPart;
            if (outcome == RoundOutcome.Foul)
                result.FoulEarly = SignalAt - Time.time;   // 신호 예정 시각까지 남은 시간 = 몇 초 먼저 뽑았나

            result.PlayerFired = playerFired;
            result.PlayerReactionTime = playerReactionTime;
            result.EnemyFired = enemyFired;
            result.EnemyReactionTime = enemyReactionTime;

            lastResult = result;   // DuelRoutine 이 보고 다음 라운드로 갈지, 결과판을 띄울지 정한다

            string detail = "";
            if (outcome == RoundOutcome.PlayerWin) detail = $" ({hitPart}, 반응속도 {playerReactionTime:0.00}초)";
            if (outcome == RoundOutcome.Foul) detail = $" (신호보다 {result.FoulEarly:0.00}초 먼저 뽑음)";
            Debug.Log($"[StageManager] {CurrentRound}라운드 판정 : {outcome}{detail}");
            enemy.EndRound(result);   // 아직 안 쐈으면 멈추고, 맞았으면 쓰러진다
            RoundEnded?.Invoke(result);
        }

        // 결투 끝 → 방송 (EndRound 가 라운드 봉투를 만들듯, 여기서는 결투 봉투를 만든다)
        private void EndDuel()
        {
            DuelResult result = new DuelResult();
            result.LastRound = lastResult;
            result.HasFired = hasFiredInDuel;
            result.FastestReactionTime = fastestReactionTime;

            // 점수는 이겼을 때만 계산한다. 지거나 반칙하면 봉투가 비어 있어서 0점 (기획서 7장)
            //   기본 점수는 난이도마다 달라서 표(enemyDef)에서 꺼내 넘긴다
            //   조각별로 담겨 오므로 결과판은 점수 내역을 다시 계산하지 않는다
            if (lastResult.Outcome == RoundOutcome.PlayerWin)
                result.Score = ScoreCalculator.Calculate(enemyDef.BaseScore, lastResult.PlayerReactionTime, lastResult.HitPart, lastResult.Round);

            Debug.Log($"[StageManager] 결투 끝 : {lastResult.Outcome}, {result.Score.Total}점");
            DuelEnded?.Invoke(result);
        }

        // ---------------------------------------------------------------- API

        // 다시 결투 (결과판의 '다시 결투' 버튼이 부른다)
        //   지금 씬을 처음부터 다시 연다. 씬의 모든 오브젝트가 처음 상태로 돌아가고 Start 가 1라운드부터 다시 돈다.
        //   DataManager 는 씬 오브젝트가 아니라서 다시 읽지 않는다. (Play 를 누를 때 한 번만 읽는다)
        public void RestartDuel()
        {
            Debug.Log("[StageManager] 다시 결투");
            ScreenFader.LoadScene(SceneManager.GetActiveScene().name);   // 암전한 뒤 연다
        }

        // 메인화면으로 (결과판의 '메인으로' 버튼이 부른다)
        //   난이도 · 홀스터 자리는 GameSettings(static)에 남아 있어서, 메인화면이 지난번 값 그대로 열린다
        public void ReturnToMainMenu()
        {
            Debug.Log("[StageManager] 메인화면으로");
            ScreenFader.LoadScene(MainMenuSceneName);   // 암전한 뒤 연다
        }
    }
}
