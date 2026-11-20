using Game.Table;
using UnityEngine;

namespace Game
{
    // [Facade] 표들로 들어가는 단일 창구. 표를 보유하고, 로딩 순서를 정한다.
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
