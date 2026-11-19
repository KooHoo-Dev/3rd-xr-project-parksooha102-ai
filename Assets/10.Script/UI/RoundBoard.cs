using System;
using TMPro;
using UnityEngine;

namespace Game
{
    // 라운드 쪽지. 라운드가 끝날 때마다 그 라운드에 무슨 일이 있었는지 작게 띄운다.
    public class RoundBoard : MonoBehaviour
    {
        // 다음에 할 일을 적는 빨간 글자. TMP 는 <color=#색>글자</color> 로 일부만 색을 바꿀 수 있다
        private const string DrawHint = "<color=#A32E22>총을 꽂고 앞의 표식으로 이동</color>";

        [Header("연결")]
        [SerializeField] private Transform head;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;    // 몇 라운드 · 어떻게 끝났나
        [SerializeField] private TMP_Text detailText;   // 나 / 적 줄, 다음에 할 일

        [Header("설정")]
        [SerializeField] private float distance = 2f;   // 머리에서 쪽지까지 (m). 결과판(1.5m)보다 조금 멀리

        private void Awake()
        {
            panel.SetActive(false);   // 라운드가 끝나야 켠다
        }

        private void OnEnable()
        {
            StageManager.Instance.RoundStarted += HandleRoundStarted;
            StageManager.Instance.RoundEnded += HandleRoundEnded;
            StageManager.Instance.DuelEnded += HandleDuelEnded;
        }

        private void OnDisable()
        {
            StageManager.Instance.RoundStarted -= HandleRoundStarted;
            StageManager.Instance.RoundEnded -= HandleRoundEnded;
            StageManager.Instance.DuelEnded -= HandleDuelEnded;
        }

        // 다음 라운드 대치 시작 → 끈다
        private void HandleRoundStarted(int round)
        {
            panel.SetActive(false);
        }

        // 결투 끝 → 끈다 (큰 결과판이 뜬다)
        private void HandleDuelEnded(DuelResult result)
        {
            panel.SetActive(false);
        }

        // 라운드 판정 끝 → 띄운다
        private void HandleRoundEnded(RoundResult result)
        {
            // ① 결과에 맞게 글자를 채운다
            switch (result.Outcome)
            {
                case RoundOutcome.PlayerWin:
                    ShowPlayerWin(result);
                    break;
                case RoundOutcome.EnemyWin:
                    ShowEnemyWin(result);
                    break;
                case RoundOutcome.Draw:
                    ShowDraw(result);
                    break;
                case RoundOutcome.Foul:
                    ShowFoul(result);
                    break;
                default:
                    // RoundOutcome 에 결과를 새로 추가하고 여기를 안 고쳤다
                    throw new Exception($"[RoundBoard] 쪽지에 띄울 수 없는 결과입니다 : {result.Outcome}");
            }

            // ② 머리 앞 distance(m) 로 옮긴다. 방향은 Inspector 에서 돌려 둔 그대로 (적 쪽)
            transform.position = head.position + transform.forward * distance;

            // ③ 켠다
            panel.SetActive(true);
        }

        // 승리 : 내가 먼저 맞혔다
        private void ShowPlayerWin(RoundResult result)
        {
            titleText.SetText($"{result.Round}라운드 · 승리");
            detailText.SetText(
                $"나  명중 {result.PlayerReactionTime:0.00}초 · {HitPartNames.Get(result.HitPart)}\n" +
                $"적  {GetMissText(result.EnemyFired, result.EnemyReactionTime, "늦음")}");
        }

        // 패배 : 적이 먼저 맞혔다
        private void ShowEnemyWin(RoundResult result)
        {
            titleText.SetText($"{result.Round}라운드 · 패배");
            detailText.SetText(
                $"나  {GetMissText(result.PlayerFired, result.PlayerReactionTime, "늦음")}\n" +
                $"적  명중 {result.EnemyReactionTime:0.00}초");
        }

        // 무승부 : 둘 다 못 맞혔다 (빗나갔거나, 3초 동안 안 쐈거나). 다음에 할 일도 알려 준다
        private void ShowDraw(RoundResult result)
        {
            titleText.SetText($"{result.Round}라운드 · 무승부");
            detailText.SetText(
                $"나  {GetMissText(result.PlayerFired, result.PlayerReactionTime, "안 쏨")}\n" +
                $"적  {GetMissText(result.EnemyFired, result.EnemyReactionTime, "안 쏨")}\n" +
                DrawHint);
        }

        // 반칙 : 신호 전에 뽑았다
        private void ShowFoul(RoundResult result)
        {
            titleText.SetText($"{result.Round}라운드 · 반칙");
            detailText.SetText($"신호보다 {result.FoulEarly:0.00}초 먼저 뽑음");
        }

        // 맞히지 못한 사람의 줄
        //   쐈으면   "빗나감 0.95초"
        //   안 쐈으면 notFiredText ("늦음" : 상대가 먼저 맞힘 / "안 쏨" : 시간이 지나 무승부)
        private string GetMissText(bool fired, float reactionTime, string notFiredText)
        {
            if (fired)
                return $"빗나감 {reactionTime:0.00}초";

            return notFiredText;
        }
    }
}
