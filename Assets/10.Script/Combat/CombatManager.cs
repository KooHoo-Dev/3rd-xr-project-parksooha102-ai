using System;
using UnityEngine;

namespace Game
{
    // [전투 매니저] 전투에서 일어난 일(플레이어가 쐈다 · 뽑았다)을 모아서 "방송" 하는 곳.
    [DefaultExecutionOrder(-100)]   // 구독자(기본 0)보다 먼저 Instance 를 채운다
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("연결")]
        [SerializeField] private Gun playerGun;
        [SerializeField] private HolsterSlot playerHolster;

        // 플레이어가 한 발 쐈다. 맞았든 빗나갔든 쏠 때마다 한 번 온다.
        // 맞았는지는 받는 쪽이 shot.IsHit 로 본다.
        public event Action<ShotInfo> PlayerFired;

        // 플레이어가 홀스터에서 총을 뽑았다. 반칙인지는 받는 쪽(StageManager)이 정한다.
        public event Action PlayerDrew;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            // 총의 Fired 를 구독한다. 총의 Awake 가 아직이어도 괜찮다 (이벤트는 Awake 전부터 있다)
            playerGun.Fired += HandleGunFired;
            playerHolster.Drawn += HandleHolsterDrawn;
        }

        private void OnDisable()
        {
            playerGun.Fired -= HandleGunFired;
            playerHolster.Drawn -= HandleHolsterDrawn;
        }

        // 홀스터에서 총이 빠졌다 → 구독자 모두에게 다시 알린다
        private void HandleHolsterDrawn()
        {
            PlayerDrew?.Invoke();
        }

        // 플레이어 총을 홀스터에 꽂는다 (손에 들려 있으면 빼앗아서)
        public void HolsterPlayerGun()
        {
            playerHolster.PutBack();
        }

        // 플레이어 총이 지금 홀스터에 꽂혀 있나? (무승부 뒤 "꽂아야 다음 라운드" 에 쓴다)
        public bool IsPlayerGunHolstered()
        {
            return playerHolster.IsHolstered();
        }

        // 총이 한 발 쐈다 → 구독자 모두에게 다시 알린다
        private void HandleGunFired(ShotInfo shot)
        {
            // 구독자가 하나도 없으면 PlayerFired 가 null 이라 ?. 로 건너뛴다. (구독자가 없는 것도 정상이다)
            PlayerFired?.Invoke(shot);
        }

        // 플레이어 총 잠그기 / 풀기 (StageManager 가 라운드에 맞춰 부른다)
        // 다른 스크립트는 총을 모른다. 총을 아는 이 매니저를 통해서만 잠근다.
        public void LockPlayerGun()
        {
            playerGun.Lock();
        }

        public void UnlockPlayerGun()
        {
            playerGun.Unlock();
        }
    }
}
