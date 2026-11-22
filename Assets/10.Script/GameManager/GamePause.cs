using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    // [일시 정지] 게임 시간 · 소리를 멈추고 푸는 곳. GameSettings · SoundManager 처럼 static 이라 씬에 놓지 않는다
    //   멈추는 일만 한다. 언제 멈출지(메뉴 버튼), 무엇을 보여 줄지(일시 정지 판)는 PauseMenu 가 정한다
    //
    //   시간 : Time.timeScale = 0
    //     StageManager 의 신호 시각 · 반응속도는 Time.time 으로 잰다 → 멈춘 동안 Time.time 도 멈춰서 공정하다
    //     WaitForSeconds 로 기다리는 일(적 격발 · 신음 · 쓰러지는 소리 ...)도 같이 멈춘다
    //     UI(레이 · 버튼 · 슬라이더)는 시간과 상관없이 움직인다
    //   소리 : AudioListener.pause = true → 나던 소리가 그 자리에서 멈췄다가 풀면 이어서 난다
    //     UI 소리는 멈추면 안 되므로 UISound 가 ignoreListenerPause 를 켠다
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        public static void Resume()
        {
            if (IsPaused == false) return;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        // 멈춘 채 씬이 바뀌면(메인으로 · 다시 결투) 새 씬도 멈춰 있게 된다 → 씬이 열릴 때마다 푼다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            IsPaused = false;
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                IsPaused = true;   // Resume 이 확실히 되돌리게
                Resume();
            };
        }
    }
}
