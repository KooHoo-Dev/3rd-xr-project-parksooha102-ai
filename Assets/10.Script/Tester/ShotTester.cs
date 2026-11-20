using UnityEngine;

namespace Game
{
    // 총 쏘기를 테스트하는 객체 (개발용). PlayerFired 를 구독해서 결과를 로그와 선으로 보여 준다.
    public class ShotTester : MonoBehaviour
    {
        private void OnEnable()
        {
            // 구독 : "PlayerFired 가 오면 HandlePlayerFired 를 불러 줘" (버튼 onClick.AddListener 와 같은 일)
            CombatManager.Instance.PlayerFired += HandlePlayerFired;
        }

        private void OnDisable()
        {
            // 구독 해제 : 등록한 것을 그대로 뺀다. 안 빼면 이 오브젝트가 사라진 뒤에도 매니저가 부르려고 한다
            CombatManager.Instance.PlayerFired -= HandlePlayerFired;
        }

        private void HandlePlayerFired(ShotInfo shot)
        {
            if (shot.IsHit)
            {
                Debug.DrawLine(shot.Origin, shot.Point, Color.green, 1f);
                Debug.Log($"[ShotTester] 명중 — {shot.Zone.Part} ({shot.Zone.name}), 쏜 시각 {shot.FireTime:F2}초");
            }
            else
            {
                Debug.DrawLine(shot.Origin, shot.Point, Color.red, 1f);
                Debug.Log($"[ShotTester] 빗나감, 쏜 시각 {shot.FireTime:F2}초");
            }
        }
    }
}
