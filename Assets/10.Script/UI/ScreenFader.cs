using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game
{
    // 암전 (화면 전체를 검게 덮었다 걷는다). 씬을 오갈 때 소품 · 플레이어가 자리 잡는 모습을 가린다.
    //   씬이 열리면 검은 화면으로 시작한다 → 배치가 끝나면 FadeIn 으로 걷는다 (StageManager · MainMenu 가 부른다)
    //   씬을 떠날 때는 LoadScene 으로 연다 → 검게 덮은 다음 씬을 바꾼다
    //
    //   씬에 따로 놓지 않는다. 씬이 열릴 때마다 Main Camera 에 저절로 붙는다
    //     (sceneLoaded 는 Awake 다음, Start · 첫 화면보다 먼저 온다 → 첫 프레임부터 검다)
    //   VR 은 Screen Space - Overlay 캔버스가 안 보여서, 카메라 바로 앞에 붙는 Screen Space - Camera 캔버스로 덮는다
    public class ScreenFader : MonoBehaviour
    {
        private const float FadeTime = 0.5f;   // 덮는 · 걷는 시간 (초)

        private static ScreenFader instance;

        private CanvasGroup group;
        private Coroutine fade;
        private bool isLoading;   // 씬을 바꾸는 중. 버튼을 두 번 눌러도 한 번만 연다

        // 게임이 켜질 때 한 번 : "씬이 열릴 때마다 알려 줘" 라고 등록한다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        // 씬이 열렸다 → 그 씬의 Main Camera 에 붙인다
        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning($"[ScreenFader] {scene.name} 씬에 MainCamera 태그가 붙은 카메라가 없어 암전을 쓰지 않습니다.");
                return;
            }

            cam.gameObject.AddComponent<ScreenFader>();
        }

        private void Awake()
        {
            instance = this;

            // 카메라 바로 앞(near clip 조금 뒤)에 화면을 꽉 채우는 검은 판을 만든다. 처음엔 다 덮은 상태
            Camera cam = GetComponent<Camera>();

            GameObject canvasObject = new GameObject("ScreenFader", typeof(Canvas), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.01f;
            canvas.sortingOrder = short.MaxValue;   // 다른 UI 보다 위

            group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ---------------------------------------------------------------- API

        // 배치가 끝났다 → 검은 화면을 걷는다
        //   텔레포트는 요청한 다음 프레임에 옮겨지므로, 한 프레임 기다렸다가 걷기 시작한다
        public static void FadeIn()
        {
            if (instance == null) return;
            instance.StartFade(0f, 1);
        }

        // 화면을 검게 덮은 다음 sceneName 씬을 연다
        public static void LoadScene(string sceneName)
        {
            if (instance == null)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
            if (instance.isLoading) return;

            instance.isLoading = true;
            instance.StartCoroutine(instance.FadeOutAndLoad(sceneName));
        }

        // ---------------------------------------------------------------- 내부

        private IEnumerator FadeOutAndLoad(string sceneName)
        {
            StartFade(1f, 0);
            yield return fade;
            SceneManager.LoadScene(sceneName);
        }

        private void StartFade(float target, int delayFrames)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine(target, delayFrames));
        }

        // alpha 를 target 까지 FadeTime 초에 걸쳐 바꾼다
        //   일시 정지(timeScale 0) 중에 메인으로 가도 멈추지 않게 unscaledDeltaTime 을 쓴다
        private IEnumerator FadeRoutine(float target, int delayFrames)
        {
            for (int i = 0; i < delayFrames; i++)
                yield return null;

            float start = group.alpha;
            float t = 0f;
            while (t < FadeTime)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, t / FadeTime);
                yield return null;
            }
            group.alpha = target;
            fade = null;
        }
    }
}
