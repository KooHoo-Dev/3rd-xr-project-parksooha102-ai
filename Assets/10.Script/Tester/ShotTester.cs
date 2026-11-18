using UnityEngine;

namespace Game
{
    // 총 쏘기를 테스트하는 객체 (개발용)
    // CombatManager 의 PlayerFired 이벤트를 구독해서, 한 발 쏠 때마다 결과를 로그와 선으로 보여 준다.
    // 실제 게임에서는 GameManager · 적 · 소리 같은 것들이 이 자리에서 같은 이벤트를 구독한다.
    //
    // 이 스크립트는 총(Gun)을 모르고 CombatManager 만 안다. 총은 아무도 모른다.
    // 그래서 이 오브젝트를 씬에서 지워도 총은 아무 문제 없이 쏜다. (구독자가 하나 줄었을 뿐이다)
    //
    // 사용법
    //   빈 오브젝트(ShotTester)를 만들고 붙인다. 연결할 것은 없다.
    //   씬에 CombatManager 가 있어야 한다. (없으면 OnEnable 에서 Instance 가 비어 있어 바로 터진다)
    //   총을 쏘면
    //     Console  : "명중 — Head (Target)" 또는 "빗나감"
    //     Scene 뷰 : 총구 → 닿은 곳까지 선이 1초 동안 보인다 (초록 = 명중, 빨강 = 빗나감)
    //                Game 뷰에서도 보려면 Game 뷰 오른쪽 위의 Gizmos 를 켠다
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
