using Game.Table;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    // 적 스폰을 테스트하는 객체 (에디터 전용)
    // 개발할 때만 사용을 하며, 실제 게임에서는 GameManager 가 이 일을 맡는다.
    //
    // 사용법
    //   빈 오브젝트에 붙이고, 플레이어가 서는 자리에 둔다. (XR Origin 과 같은 위치)
    //   파란 화살표(Z)가 적이 나올 쪽을 향하게 한다.
    //
    //   1 → 쉬움 적,  2 → 보통 적,  3 → 어려움 적
    //   적은 이 오브젝트 앞쪽으로 1라운드 거리(10m)만큼 떨어진 곳에, 이쪽을 바라보게 나온다.
    public class EnemyTester : MonoBehaviour
    {
        private GameObject currentEnemy;   // 지금 나와 있는 적. 1:1 결투라 한 명만 둔다

        // 에디터 전용. 빌드(Quest)에는 Update 가 아예 들어가지 않는다.
        //   클래스 전체를 #if 로 감싸면 빌드에서 씬의 컴포넌트가 "Missing Script" 가 되므로 Update 만 감싼다.
        //   에디터에는 키보드가 항상 있으므로 Keyboard.current 는 null 일 수 없다.
#if UNITY_EDITOR
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            // 개발용 하드 코딩 : 숫자 키 → 난이도
            if (keyboard.digit1Key.wasPressedThisFrame) Spawn(Difficulty.Easy);
            if (keyboard.digit2Key.wasPressedThisFrame) Spawn(Difficulty.Normal);
            if (keyboard.digit3Key.wasPressedThisFrame) Spawn(Difficulty.Hard);
        }
#endif

        private void Spawn(Difficulty difficulty)
        {
            // 표에서 그 난이도의 적을 꺼낸다
            EnemyDef enemyDef = DataManager.Instance.GetEnemyDef(difficulty);

            // 위치 : 내 앞쪽으로 1라운드 거리만큼
            RoundDef round = enemyDef.GetRound(1);
            Vector3 position = transform.position + transform.forward * round.Distance;

            // 방향 : 나(플레이어 자리)를 바라보게. 내 앞쪽의 반대 방향이다
            Quaternion rotation = Quaternion.LookRotation(-transform.forward);

            // 이미 나와 있는 적은 치우고 새로 만든다
            if (currentEnemy != null) Destroy(currentEnemy);
            currentEnemy = Instantiate(enemyDef.Prefab, position, rotation);

            Debug.Log($"[EnemyTester] {difficulty} 적 스폰 — {enemyDef.Prefab.name}, 거리 {round.Distance}m");
        }
    }
}
