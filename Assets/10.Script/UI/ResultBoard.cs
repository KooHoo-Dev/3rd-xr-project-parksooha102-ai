using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 결과판 (기획서 7장). DuelEnded 를 듣고, 플레이어 눈앞에 현상금 수배지 모양으로 결과를 띄운다.
    public class ResultBoard : MonoBehaviour
    {
        // 가운데 줄 양옆의 점 (수배지의 "• • • DEAD OR ALIVE • • •" 줄 모양을 따왔다)
        private const string Dots = "•  •  •";

        // 점수 내역에서 숫자를 세우는 자리
        //   TMP 의 <pos=70%> : 이 뒤의 글자는 칸 왼쪽에서 70% 자리부터 쓴다 → 줄마다 숫자가 같은 세로줄에 선다
        private const string NumberColumn = "<pos=70%>";

        [Header("연결")]
        [SerializeField] private Transform head;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text resultText;   // 가운데 줄 (Subtitle 오브젝트) : WIN / LOSE / COWARD
        [SerializeField] private TMP_Text detailText;   // 가운데 : 결투 정리 (점수 내역 / 진 라운드 · 가장 빠른 반응)
        [SerializeField] private TMP_Text rewardText;   // 맨 아래 : 점수 (REWARD $ 2,080)
        [SerializeField] private Button retryButton;

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
        }

        private void OnDisable()
        {
            StageManager.Instance.DuelEnded -= HandleDuelEnded;
            retryButton.onClick.RemoveListener(HandleRetryClicked);
        }

        // 결투가 끝났다 (StageManager 가 알려 줌)
        private void HandleDuelEnded(DuelResult result)
        {
            // ① 결과에 맞게 글자를 채운다 (어떻게 끝났는지는 승부가 난 라운드의 봉투에 있다)
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

            //    맨 아래 보상은 모든 결과가 같은 모양이다. 지거나 반칙하면 Score 가 0 이라 REWARD $ 0
            //    $"REWARD $ {result.Score:N0}" 읽는 법
            //      맨 앞 $" 는 "{ } 안에 값을 넣는 글자" 라는 표시, REWARD 뒤의 $ 는 그냥 달러 글자다.
            //      { } 안이 값 자리이고, :N0 은 소수점 없이 세 자리마다 쉼표 (2080 → 2,080)
            rewardText.SetText($"REWARD $ {result.Score:N0}");

            // ② 머리 앞 distance(m) 로 옮긴다. 방향은 Inspector 에서 돌려 둔 그대로 (적 쪽)
            transform.position = head.position + transform.forward * distance;

            // ③ 켠다
            panel.SetActive(true);
        }

        // 승리 : WIN + 점수 내역
        private void ShowPlayerWin(DuelResult result)
        {
            resultText.SetText($"{Dots}   WIN   {Dots}");

            // 이긴 라운드의 봉투 (반응속도 · 맞힌 부위 · 라운드 번호가 여기 있다)
            RoundResult winRound = result.LastRound;

            // 점수 조각 (ScoreCalculator.Calculate 와 같은 함수)
            int speedScore = ScoreCalculator.GetSpeedScore(winRound.PlayerReactionTime);
            int bonus = ScoreCalculator.GetBonus(winRound.HitPart);
            int penalty = ScoreCalculator.GetRoundPenalty(winRound.Round);
            string penaltyText = penalty == 0 ? "0" : $"-{penalty}";   // 1라운드는 감점이 없다

            // <align=left>  : 이 글자만 왼쪽 정렬 (나머지 결과는 가운데 정렬 그대로)
            // <indent=6%>   : 줄마다 왼쪽을 조금 띄운다 (종이의 그을린 가장자리에서 떨어지게)
            detailText.SetText(
                "<align=left><indent=6%>" +
                $"기본{NumberColumn}+{result.BaseScore:N0}\n" +
                $"반응속도 {winRound.PlayerReactionTime:0.00}초{NumberColumn}+{speedScore}\n" +
                $"{HitPartNames.Get(winRound.HitPart)} 명중{NumberColumn}+{bonus}\n" +
                $"{winRound.Round}라운드{NumberColumn}{penaltyText}");
        }

        // 패배 : LOSE + 진 라운드, 가장 빠른 반응
        private void ShowEnemyWin(DuelResult result)
        {
            resultText.SetText($"{Dots}   LOSE   {Dots}");

            string fastestLine;
            if (result.HasFired)
                fastestLine = $"가장 빠른 반응 {result.FastestReactionTime:0.00}초";
            else
                fastestLine = "한 발도 쏘지 못함";

            detailText.SetText(
                $"{result.LastRound.Round}라운드에서 패배\n" +
                fastestLine);
        }

        // 반칙 : COWARD (이번엔 내가 수배된 비겁자)
        private void ShowFoul(DuelResult result)
        {
            resultText.SetText($"{Dots}   COWARD   {Dots}");
            detailText.SetText(
                $"신호보다 {result.LastRound.FoulEarly:0.00}초 먼저 뽑음\n" +
                $"{result.LastRound.Round}라운드");
        }

        // '다시 결투' 버튼을 눌렀다
        private void HandleRetryClicked()
        {
            StageManager.Instance.RestartDuel();
        }
    }
}
