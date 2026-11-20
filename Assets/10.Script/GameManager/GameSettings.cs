using Game.Table;
using UnityEngine;

namespace Game
{
    // [게임 설정] 메인화면(Title 씬)에서 고른 값을 결투 씬(Town)으로 넘겨주는 곳.
    //   씬을 바꾸면 씬 안의 오브젝트는 모두 사라지지만, static 변수는 앱이 꺼질 때까지 남는다.
    //   그래서 Title 에서 여기에 넣어 두고, Town 에서 여기서 꺼내 쓴다.
    //   (DataManager 처럼 씬 오브젝트가 아니라서 Hierarchy 에 놓지 않는다)
    //
    //   난이도 : 앱이 켜져 있는 동안만 기억한다 (껐다 켜면 보통)
    //   홀스터 : PlayerPrefs 에도 저장한다 → 껐다 켜도 맞춰 둔 자리에서 시작한다
    public static class GameSettings
    {
        // PlayerPrefs 에 저장할 이름
        //   PlayerPrefs 는 int · float · string 만 저장할 수 있어서, Vector3 는 x, y, z 를 따로 넣는다
        private const string HolsterXKey = "HolsterX";
        private const string HolsterYKey = "HolsterY";
        private const string HolsterZKey = "HolsterZ";

        // 홀스터 기본 자리 : 머리 기준 오른쪽 20cm, 아래 60cm, 앞 6cm
        //   저장된 값이 없을 때(처음 켰을 때)와 메인화면의 '처음 자리' 버튼이 쓴다
        //   (Vector3 는 const 로 못 써서 static readonly 로 둔다. StageManager 의 DuelStartPosition 과 같은 모양)
        public static readonly Vector3 DefaultHolsterOffset = new Vector3(0.2f, -0.6f, 0.06f);

        // 고른 난이도. 메인화면이 넣고, StageManager 가 꺼내서 그 난이도의 적을 만든다
        //   Town 을 바로 Play 하면 메인화면을 안 거치므로 이 처음 값(보통)으로 결투한다
        public static Difficulty Difficulty { get; set; } = Difficulty.Normal;

        // 홀스터 자리 (HolsterAnchor 기준. HolsterAnchor 는 머리 자리 · 머리 방향을 따라간다)
        //   바꿀 때는 SetHolsterOffset 으로만 바꾼다 (저장도 같이 해야 해서 set 을 private 으로 막았다)
        public static Vector3 HolsterOffset { get; private set; }

        // 앱이 켜질 때(첫 씬을 열기 전) 한 번, 저장해 둔 홀스터 자리를 읽는다 (DataManager.Boot 와 같은 방식)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            // GetFloat(이름, 없을 때 값) : 저장한 적이 없으면 뒤의 값을 준다 → 처음 켜면 기본 자리
            float x = PlayerPrefs.GetFloat(HolsterXKey, DefaultHolsterOffset.x);
            float y = PlayerPrefs.GetFloat(HolsterYKey, DefaultHolsterOffset.y);
            float z = PlayerPrefs.GetFloat(HolsterZKey, DefaultHolsterOffset.z);
            HolsterOffset = new Vector3(x, y, z);

            Debug.Log($"[GameSettings] 홀스터 자리 {HolsterOffset:F2}");
        }

        // ---------------------------------------------------------------- API

        // 홀스터 자리를 바꾸고 저장한다 (메인화면의 조정 버튼이 부른다)
        public static void SetHolsterOffset(Vector3 offset)
        {
            HolsterOffset = offset;

            PlayerPrefs.SetFloat(HolsterXKey, offset.x);
            PlayerPrefs.SetFloat(HolsterYKey, offset.y);
            PlayerPrefs.SetFloat(HolsterZKey, offset.z);

            // 바로 파일에 쓴다. 안 부르면 앱이 정상 종료될 때 쓰는데, Quest 는 헤드셋을 벗으면 그냥 꺼질 때가 많다
            PlayerPrefs.Save();
        }
    }
}
