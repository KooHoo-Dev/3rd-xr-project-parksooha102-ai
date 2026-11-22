using System;
using Game.Table;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 결과판 (기획서 7장). DuelEnded 를 듣고, 플레이어 눈앞에 현상금 수배지 모양으로 결과를 띄운다.
    //   수배지 : WANTED → DEAD OR ALIVE → 몽타주(현상범 얼굴) → 이름 → 결투 정리 → REWARD
    //   결과는 몽타주 위에 찍힌 빨간 도장으로 보여 준다 (DEAD / AT LARGE / COWARD)
    public class ResultBoard : MonoBehaviour
    {
        // 점수 내역에서 숫자를 세우는 자리
        //   TMP 의 <pos=70%> : 이 뒤의 글자는 칸 왼쪽에서 70% 자리부터 쓴다 → 줄마다 숫자가 같은 세로줄에 선다
        private const string NumberColumn = "<pos=70%>";

        // 몽타주 위 총알 구멍 자리 (몽타주 가운데 기준, 캔버스 칸). 맞힌 부위에 구멍이 난다
        private static readonly Vector2 HeadHolePosition = new Vector2(14f, 18f);     // 얼굴
        private static readonly Vector2 BodyHolePosition = new Vector2(-34f, -118f);  // 가슴 (몽타주 아래쪽)

        [Header("연결")]
        [SerializeField] private Transform head;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text resultText;   // 도장 글자 (Stamp/Label) : DEAD / AT LARGE / COWARD
        [SerializeField] private TMP_Text nameText;     // 몽타주 아래 현상범 이름
        [SerializeField] private TMP_Text detailText;   // 가운데 : 결투 정리 (점수 내역 / 진 라운드 · 가장 빠른 반응)
        [SerializeField] private TMP_Text rewardText;   // 맨 아래 : 점수 (REWARD $ 2,580)
        [SerializeField] private Image montageImage;    // 몽타주 그림
        [SerializeField] private RectTransform bulletHole;   // 몽타주 위 총알 구멍 (이겼을 때만 켠다)
        [SerializeField] private Button retryButton;
        [SerializeField] private Button mainMenuButton;

        [Header("몽타주 (수배지 모양으로 정렬 메뉴가 구워서 넣는다)")]
        [SerializeField] private Sprite easyPortrait;
        [SerializeField] private Sprite normalPortrait;
        [SerializeField] private Sprite hardPortrait;
        [SerializeField] private Sprite cowardPortrait;   // 반칙 : 얼굴 모를 실루엣 (= 플레이어)

        [Header("설정")]
        [SerializeField] private float distance = 1.5f;   // 머리에서 결과판까지 (m). 손을 뻗지 않고 레이로 누르는 거리

        private void Awake()
        {
            panel.SetActive(false);   // 결투가 끝나야 켠다
        }

        private void OnEnable()
        {
            StageManager.Instance.DuelEnded += HandleDuelEnded;
            retryButton.onClick.AddListener(HandleRetryClicked);
            mainMenuButton.onClick.AddListener(HandleMainMenuClicked);
        }

        private void OnDisable()
        {
            StageManager.Instance.DuelEnded -= HandleDuelEnded;
            retryButton.onClick.RemoveListener(HandleRetryClicked);
            mainMenuButton.onClick.RemoveListener(HandleMainMenuClicked);
        }

        // 결투가 끝났다 (StageManager 가 알려 줌)
        private void HandleDuelEnded(DuelResult result)
        {
            // ① 결과에 맞게 글자 · 그림을 채운다 (어떻게 끝났는지는 승부가 난 라운드의 봉투에 있다)
            switch (result.LastRound.Outcome)
            {
                case RoundOutcome.PlayerWin:
                    ShowPlayerWin(result);
                    break;
                case RoundOutcome.EnemyWin:
                    ShowEnemyWin(result);
                    break;
                case RoundOutcome.Foul:
                    ShowFoul(result);
                    break;
                default:
                    // 무승부는 DuelEnded 로 오지 않는다 (StageManager 가 다음 라운드로 넘긴다)
                    throw new Exception($"[ResultBoard] 결과판에 띄울 수 없는 결과입니다 : {result.LastRound.Outcome}");
            }

            //    맨 아래 보상은 모든 결과가 같은 모양이다. 지거나 반칙하면 점수 봉투가 비어 있어서 REWARD $ 0
            //    $"REWARD $ {result.Score.Total:N0}" 읽는 법
            //      맨 앞 $" 는 "{ } 안에 값을 넣는 글자" 라는 표시, REWARD 뒤의 $ 는 그냥 달러 글자다.
            //      { } 안이 값 자리이고, :N0 은 소수점 없이 세 자리마다 쉼표 (2580 → 2,580)
            rewardText.SetText($"REWARD $ {result.Score.Total:N0}");

            // ② 머리 앞 distance(m) 로 옮긴다. 방향은 Inspector 에서 돌려 둔 그대로 (적 쪽)
            transform.position = head.position + transform.forward * distance;

            // ③ 켠다
            panel.SetActive(true);
        }

        // 승리 : 적 몽타주에 DEAD 도장 + 맞힌 자리에 총알 구멍 + 점수 내역
        private void ShowPlayerWin(DuelResult result)
        {
            // 이긴 라운드의 봉투 (반응속도 · 맞힌 부위 · 라운드 번호가 여기 있다) → 줄 이름에 쓴다
            RoundResult winRound = result.LastRound;

            ShowOutlaw();
            resultText.SetText("DEAD\n<size=42%>승리 · 현상범 처치</size>");
            ShowBulletHole(winRound.HitPart);

            // 점수 봉투 (StageManager 가 계산해서 담아 준 조각들) → 줄 끝 숫자에 쓴다. 여기서 다시 계산하지 않는다
            ScoreBreakdown score = result.Score;

            // 줄 이름은 숫자 대신 칭호로 적는다 (반응속도 0.85초 → 눈 깜짝할 새)
            string reactionName = DuelTitles.GetName(DuelTitles.GetReaction(true, winRound.PlayerReactionTime));
            string hitName = DuelTitles.GetName(DuelTitles.GetHit(winRound.HitPart));
            string roundName = DuelTitles.GetName(DuelTitles.GetWinRound(winRound.Round, GetRoundCount()));

            // <align=left>  : 이 글자만 왼쪽 정렬 (나머지 결과는 가운데 정렬 그대로)
            // <indent=8%>   : 줄마다 왼쪽을 조금 띄운다 (종이의 인쇄 테두리에서 떨어지게)
            detailText.SetText(
                "<align=left><indent=8%>" +
                $"기본{NumberColumn}+{score.BaseScore:N0}\n" +
                $"{reactionName}{NumberColumn}+{score.SpeedScore}\n" +
                $"{hitName}{NumberColumn}+{score.PartBonus}\n" +
                $"{roundName}{NumberColumn}+{score.RoundBonus}");
        }

        // 패배 : 적 몽타주에 AT LARGE(아직 잡히지 않음) 도장 + 진 라운드, 가장 빠른 반응
        //   동귀어진 : 마지막 한 발이 적을 맞혔다 → 동귀어진 도장 + 맞힌 자리에 총알 구멍. 그래도 패배라 REWARD $ 0
        private void ShowEnemyWin(DuelResult result)
        {
            ShowOutlaw();
            if (result.LastRound.MutualKill)
            {
                resultText.SetText("동귀어진\n<size=42%>패배 · 현상범과 함께 쓰러짐</size>");
                ShowBulletHole(result.LastRound.HitPart);
            }
            else
            {
                resultText.SetText("AT LARGE\n<size=42%>패배 · 현상범 도주</size>");
                bulletHole.gameObject.SetActive(false);
            }

            // 쓰러진 라운드 · 가장 빠른 반응을 칭호로 (한 발도 못 쐈으면 반응 칭호가 '허리춤의 장식품')
            string roundName = DuelTitles.GetName(DuelTitles.GetLoseRound(result.LastRound.Round, GetRoundCount()));
            string reactionName = DuelTitles.GetName(DuelTitles.GetReaction(result.HasFired, result.FastestReactionTime));

            detailText.SetText(
                $"{roundName}\n" +
                reactionName);
        }

        // 반칙 : 이번엔 내가 수배된 비겁자. 얼굴 모를 실루엣에 COWARD 도장
        private void ShowFoul(DuelResult result)
        {
            montageImage.sprite = cowardPortrait;
            nameText.SetText("바로 당신");
            resultText.SetText("COWARD\n<size=42%>반칙 · 신호 전에 뽑음</size>");
            bulletHole.gameObject.SetActive(false);

            // 먼저 뽑은 시간 · 반칙한 라운드를 칭호로
            string foulName = DuelTitles.GetName(DuelTitles.GetFoul(result.LastRound.FoulEarly));
            string roundName = DuelTitles.GetName(DuelTitles.GetFoulRound(result.LastRound.Round));

            detailText.SetText(
                $"{foulName}\n" +
                roundName);
        }

        // 이번 적의 라운드 수 (enemies.json). 라운드 칭호가 "마지막 라운드" 를 알아야 해서 쓴다
        private int GetRoundCount()
        {
            return DataManager.Instance.GetEnemyDef(GameSettings.Difficulty).RoundCount;
        }

        // 이번 결투의 적을 수배지에 올린다 : 몽타주 + 이름 (난이도마다 적이 다르다)
        private void ShowOutlaw()
        {
            EnemyDef enemy = DataManager.Instance.GetEnemyDef(GameSettings.Difficulty);
            montageImage.sprite = GetPortrait(GameSettings.Difficulty);
            nameText.SetText(enemy.Name);
        }

        private Sprite GetPortrait(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return easyPortrait;
                case Difficulty.Normal: return normalPortrait;
                case Difficulty.Hard: return hardPortrait;
            }

            // Difficulty 에 난이도를 새로 추가하고 여기를 안 고쳤다
            throw new Exception($"[ResultBoard] 몽타주가 정해지지 않은 난이도입니다 : {difficulty}");
        }

        // 맞힌 부위 자리에 총알 구멍을 켠다
        private void ShowBulletHole(HitPart part)
        {
            switch (part)
            {
                case HitPart.Head:
                    bulletHole.anchoredPosition = HeadHolePosition;
                    break;
                case HitPart.Body:
                    bulletHole.anchoredPosition = BodyHolePosition;
                    break;
                default:
                    // HitPart 에 부위를 새로 추가하고 여기를 안 고쳤다
                    throw new Exception($"[ResultBoard] 총알 구멍 자리가 정해지지 않은 부위입니다 : {part}");
            }
            bulletHole.gameObject.SetActive(true);
        }

        // '다시 결투' 버튼을 눌렀다
        private void HandleRetryClicked()
        {
            StageManager.Instance.RestartDuel();
        }

        // '메인으로' 버튼을 눌렀다
        private void HandleMainMenuClicked()
        {
            StageManager.Instance.ReturnToMainMenu();
        }
    }
}
