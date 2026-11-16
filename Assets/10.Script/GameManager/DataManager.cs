using Game.Table;
using UnityEngine;

namespace Game
{
    // [Facade] 표들로 들어가는 단일 창구.
    // 표를 직접 읽지 않는다. 읽기 · 해석 · 보관은 각 Table 이 한다.
    // 여기서 하는 일은 두 가지 : 표를 보유하고, 로딩 순서를 정한다.
    //
    // 교안과 다른 점 : MonoBehaviour 가 아니라서 씬에 배치하지 않아도 된다.
    //   [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] 가 붙은 함수는
    //   Play 를 누르면 첫 씬의 어떤 Awake 보다도 먼저 유니티가 알아서 불러 준다.
    //   그래서 다른 스크립트는 Awake / Start 에서 바로 데이터를 꺼내 써도 된다.
    //
    // 새 표를 추가하는 법 (두 줄)
    //   1) public XxxTable Xxxs { get; } = new XxxTable();
    //   2) Load() 안에 Xxxs.Load();
    public class DataManager
    {
        public static DataManager Instance { get; private set; }

        public EnemyTable Enemies { get; } = new EnemyTable();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Instance = new DataManager();
            Instance.Load();
        }

        private void Load()
        {
            // 다른 표를 참조하는 표가 생기면, 참조당하는 표를 먼저 Load 한다.
            Enemies.Load();
        }

        // 편의 조회 함수들
        public EnemyDef GetEnemyDef(Difficulty difficulty) => Enemies.Get(difficulty);

        // 예) DataManager.Instance.GetRound(Difficulty.Normal, 3).HitRate  → 0.65
        public RoundDef GetRound(Difficulty difficulty, int round) => Enemies.Get(difficulty).GetRound(round);
    }
}
