using System;
using UnityEngine;

namespace Game
{
    // [전투 매니저] 전투에서 일어난 일을 모아서 "방송" 하는 곳.
    // 교안 Action Platformer 의 CombatSystem 과 같은 자리다. 다른 점은 두 가지다.
    //   ① 구독자 목록(List<ICombatObserver>) + for 문으로 알리는 대신 event Action 을 쓴다.
    //      (교안 Delegate.md 의 "적용" : 인터페이스 · 리스트 · 통지 함수를 몇 줄로 줄인 것)
    //   ② 부품이 매니저에게 보고하지 않는다. 매니저가 부품(총)을 알고, 부품의 이벤트를 구독한다.
    //      (교안 WaveManager 가 enemy.GoalReached 를 구독하는 것과 같은 방향)
    //
    // 누가 누구를 아나? (화살표 = "안다")
    //   CombatManager ──▶ Gun          : Inspector 로 받아서 gun.Fired 를 구독한다
    //   구독자들      ──▶ CombatManager : CombatManager.Instance.PlayerFired 를 구독한다
    //                                     ShotTester (지금), GameManager · 적 · 소리 (나중에)
    //   총은 아무도 모른다. 구독자들은 총을 모른다.
    //   → 구독자를 늘리거나 빼도 Gun.cs 는 한 줄도 고치지 않는다. 이게 "느슨한 결합" 이다.
    //   → 나중에 매니저가 총을 잠가도(라운드당 한 발) 화살표는 매니저 → 총 한 방향 그대로다.
    //
    // 왜 [DefaultExecutionOrder(-100)] 인가?
    //   씬이 시작될 때 유니티는 오브젝트마다 Awake → OnEnable 을 부르는데, 어느 오브젝트가 먼저인지는 정해져 있지 않다.
    //   구독자가 OnEnable 에서 CombatManager.Instance 를 부를 때 매니저의 Awake 가 아직이면, Instance 가 비어 있어서 터진다.
    //   그래서 이 스크립트를 "기본(0)보다 먼저(-100)" 돌게 해서, Instance 가 늘 먼저 채워지게 한다.
    //   (교안 HurtBox 가 OnEnable 대신 Start 에서 등록한 것과 같은 고민이다)
    //
    // 왜 교안처럼 OnDisable 에서 IsInitialized 를 확인하지 않나?
    //   게임을 끌 때 매니저가 먼저 파괴돼도, Instance 를 비우지 않으므로 C# 객체는 남아 있다.
    //   -= 는 유니티 기능이 아니라 C# 일이라서, 파괴된 매니저에 해도 터지지 않는다.
    //
    // 사용법
    //   ① 빈 오브젝트 CombatManager 를 만들고 이 스크립트를 붙인다. 씬에 하나만 둔다.
    //   ② Player Gun : 총 오브젝트(SM_Wep_Revolver_01)를 끌어다 넣는다 (꼭 넣는다)
    //
    // 구독하는 법 (교안 HUD 가 EconomyManager 를 구독하는 것과 같다)
    //   private void OnEnable()  { CombatManager.Instance.PlayerFired += HandlePlayerFired; }
    //   private void OnDisable() { CombatManager.Instance.PlayerFired -= HandlePlayerFired; }
    //   private void HandlePlayerFired(ShotInfo shot) { ... }
    [DefaultExecutionOrder(-100)]
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("연결")]
        [SerializeField] private Gun playerGun;

        // 플레이어가 한 발 쐈다. 맞았든 빗나갔든 쏠 때마다 한 번 온다.
        // 맞았는지는 받는 쪽이 shot.IsHit 로 본다.
        public event Action<ShotInfo> PlayerFired;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            // 총의 Fired 를 구독한다. 총의 Awake 가 아직이어도 괜찮다 (이벤트는 Awake 전부터 있다)
            playerGun.Fired += HandleGunFired;
        }

        private void OnDisable()
        {
            playerGun.Fired -= HandleGunFired;
        }

        // 총이 한 발 쐈다 → 구독자 모두에게 다시 알린다
        private void HandleGunFired(ShotInfo shot)
        {
            // 구독자가 하나도 없으면 PlayerFired 가 null 이라 ?. 로 건너뛴다. (구독자가 없는 것도 정상이다)
            PlayerFired?.Invoke(shot);
        }
    }
}
