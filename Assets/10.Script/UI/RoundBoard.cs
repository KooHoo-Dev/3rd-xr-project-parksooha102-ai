using System;
using TMPro;
using UnityEngine;

namespace Game
{
    // 라운드 쪽지 (탄피 카드). 무승부로 끝난 라운드만 작게 띄운다.
    //   승부가 난 라운드는 띄우지 않는다 → 적이 쓰러지는 모습을 가리지 않고, 곧 큰 결과판이 뜬다
    //   카드 : 맨 위 한 줄 · 왼쪽 리볼버 실린더(쏜 칸은 비고, 남은 탄만 보인다) · 오른쪽 두 사람의 시간
    //   무승부 뒤 할 일은 글자로 적지 않는다
    //     총 꽂기   → 홀스터 자리가 빛난다 (HolsterGlow)
    //     앞으로 가기 → 전진 표식 옆 나무 팻말 (AdvanceMarker 의 자식)
    public class RoundBoard : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform head;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;        // 맨 위 : 둘 다 빗나감 / 승부 없음
        [SerializeField] private TMP_Text detailText;       // 오른쪽 : 나 0.95초 / 적 0.87초
        [SerializeField] private GameObject[] cartridges;   // 실린더의 탄 6개. [0] = 맨 위 칸부터 시계 방향 = 1라운드에 쏜 탄
        [SerializeField] private HolsterGlow holsterGlow;   // 총을 꽂을 때까지 홀스터를 빛낸다

        [Header("설정")]
        [SerializeField] private float distance = 2f;   // 머리에서 쪽지까지 (m). 결과판(1.5m)보다 조금 멀리

        // 무승부 뒤 총이 꽂히기를 기다리는 중인가? (이때만 Update 에서 꽂혔는지 본다)
        private bool isWaitingHolster;

        private void Awake()
        {
            panel.SetActive(false);   // 무승부가 나야 켠다
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

        private void Update()
        {
            if (isWaitingHolster == false) return;

            // 총이 꽂혔다 (플레이어가 꽂았거나, 시간이 지나 StageManager 가 대신 꽂았다) → 빛을 끈다
            if (CombatManager.Instance.IsPlayerGunHolstered())
                StopHolsterGlow();
        }

        // 다음 라운드 대치 시작 → 끈다
        private void HandleRoundStarted(int round)
        {
            panel.SetActive(false);
            StopHolsterGlow();
        }

        // 결투 끝 → 끈다 (큰 결과판이 뜬다)
        private void HandleDuelEnded(DuelResult result)
        {
            panel.SetActive(false);
            StopHolsterGlow();
        }

        // 라운드 판정 끝 → 무승부일 때만 띄운다
        private void HandleRoundEnded(RoundResult result)
        {
            if (result.Outcome != RoundOutcome.Draw) return;

            // ① 맨 위 한 줄 : 둘 다 쐈으면 "둘 다 빗나감", 한 명이라도 안 쐈으면 "승부 없음"
            if (result.PlayerFired && result.EnemyFired)
                titleText.SetText("둘 다 빗나감");
            else
                titleText.SetText("승부 없음");

            // ② 두 사람의 시간
            detailText.SetText(
                $"나   {GetTimeText(result.PlayerFired, result.PlayerReactionTime)}\n" +
                $"적   {GetTimeText(result.EnemyFired, result.EnemyReactionTime)}");

            // ③ 실린더 : 지금까지 쏜 라운드 수만큼 탄을 빼고, 남은 탄만 보인다
            ShowCartridges(result.Round);

            // ④ 머리 앞 distance(m) 로 옮긴다. 방향은 Inspector 에서 돌려 둔 그대로 (적 쪽)
            transform.position = head.position + transform.forward * distance;
            panel.SetActive(true);

            // ⑤ 총이 손에 있으면 홀스터를 빛낸다 (안 뽑고 버틴 라운드는 이미 꽂혀 있어서 켜지 않는다)
            if (CombatManager.Instance.IsPlayerGunHolstered() == false)
            {
                holsterGlow.Show();
                isWaitingHolster = true;
            }
        }

        // 쏜 사람 : "0.95초", 안 쏜 사람 : "안 쏨"
        private string GetTimeText(bool fired, float reactionTime)
        {
            if (fired)
                return $"{reactionTime:0.00}초";

            return "안 쏨";
        }

        // 1 ~ round 칸은 쏜 칸(빈 구멍), 나머지는 탄
        //   예) 2라운드 무승부 → [0][1] 끄고 [2] ~ [5] 켠다 → 4발 남음
        private void ShowCartridges(int round)
        {
            if (round > cartridges.Length)
                throw new Exception($"[RoundBoard] {round}라운드인데 실린더 칸이 {cartridges.Length}개뿐입니다. enemies.json 의 라운드 수가 6 을 넘지 않는지 확인해 주세요.");

            for (int i = 0; i < cartridges.Length; i++)
                cartridges[i].SetActive(i >= round);
        }

        private void StopHolsterGlow()
        {
            isWaitingHolster = false;
            holsterGlow.Hide();
        }
    }
}
