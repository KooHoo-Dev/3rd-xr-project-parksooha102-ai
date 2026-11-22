using System.Collections;
using UnityEngine;

namespace Game
{
    // 마을 환경음. 바람 소리를 계속 깔고, 가끔 멀리서 마을 소리(말 울음 · 까마귀 · 삐걱이는 간판 ...)가 난다.
    //   NoonBell · DuelSound 와 같은 방식으로 StageManager 의 방송만 듣는다. 씬마다 하나씩 둔다 (Title · Town)
    //     Title 에는 StageManager 가 없어서 대치 연동 없이 바람 · 마을 소리만 난다
    //     DontDestroyOnLoad 로 씬을 건너 살리지 않는 이유 : Town 을 다시 열 때마다 StageManager 가 새로 생겨서
    //     옛 StageManager 를 구독한 채로 남으면 대치 중 조용해지기가 끊긴다. 씬이 바뀔 때 3초 페이드 인이 이음새를 덮는다
    //   AudioSource : AudioClip = 바람 루프, Play On Awake 끔, Loop 켬, Spatial Blend 0(2D), Volume 0.5 쯤
    //
    //   ① 바람 (loop) : 씬이 열리면 천천히 커지며 계속 난다. BGM 이 꺼진 정오 직후의 정적을 채운다
    //   ② 마을 소리 (oneShots) : 플레이어 둘레 먼 곳 아무 데서나 3D 로. 간격도 매번 다르다
    //
    //   대치 ~ 판정 (RoundStarted → RoundEnded) 동안은
    //     · 마을 소리를 내지 않는다 : 신호(종)를 기다리는 귀에 엉뚱한 소리가 들리면 신호로 착각해 반칙할 수 있다
    //     · 바람을 조금 줄인다 : 결투 BGM 이 고조되는 걸 가리지 않게 (판정이 끝나면 다시 원래대로)
    [RequireComponent(typeof(AudioSource))]
    public class TownAmbience : MonoBehaviour
    {
        [Header("마을 소리 (비워 두면 바람만)")]
        [SerializeField] private AudioClip[] oneShots;
        [SerializeField] private Vector2 oneShotInterval = new Vector2(8f, 18f);   // 다음 소리까지 (최소, 최대 초)
        [SerializeField] private Vector2 oneShotDistance = new Vector2(15f, 40f);  // 플레이어에게서 (최소, 최대 m)
        [SerializeField, Range(0f, 1f)] private float oneShotVolume = 0.7f;
        [SerializeField] private Vector2 oneShotPitch = new Vector2(0.9f, 1.1f);

        [Header("바람")]
        [SerializeField] private float fadeInTime = 3f;                            // 씬 시작 때 커지는 시간 (초)
        [SerializeField, Range(0f, 1f)] private float standoffVolumeRate = 0.6f;   // 대치 중 바람 = 원래 볼륨 × 이 값
        [SerializeField] private float standoffFadeTime = 1.5f;                    // 줄었다 · 돌아오는 시간 (초)

        private AudioSource windSource;
        private float fullVolume;        // 씬에 놓인 AudioSource 볼륨
        private bool isQuiet;            // 대치 ~ 판정 중 (마을 소리 쉼)
        private Coroutine windFade;
        private AudioClip lastOneShot;

        private void Awake()
        {
            windSource = GetComponent<AudioSource>();
            SoundManager.Route(windSource, SoundChannel.Ambience);
            fullVolume = windSource.volume;
        }

        // Title 씬에는 StageManager 가 없다 → 대치 연동 없이 바람 · 마을 소리만 낸다
        //   Instance 는 static 이라 Town → Title 로 돌아오면 사라진 Town 의 StageManager 를 가리킬 수 있다.
        //   == null 은 Unity 에서 "파괴된 오브젝트" 도 null 로 본다 → 그때도 건너뛴다
        private StageManager stage;   // 구독한 StageManager (해제할 때 같은 것을 쓴다)

        private void OnEnable()
        {
            stage = StageManager.Instance;
            if (stage == null) return;

            stage.RoundStarted += HandleRoundStarted;
            stage.RoundEnded += HandleRoundEnded;
        }

        private void OnDisable()
        {
            if (stage == null) return;

            stage.RoundStarted -= HandleRoundStarted;
            stage.RoundEnded -= HandleRoundEnded;
            stage = null;
        }

        private void Start()
        {
            // ① 바람 : 0 에서 천천히 커진다. 루프 클립의 같은 자리에서 매번 시작하지 않게 아무 데서나 튼다
            if (windSource.clip != null)
            {
                windSource.loop = true;
                windSource.time = Random.Range(0f, windSource.clip.length);
                windSource.volume = 0f;
                windSource.Play();
                FadeWind(fullVolume, fadeInTime);
            }

            // ② 마을 소리
            if (oneShots != null && oneShots.Length > 0)
                StartCoroutine(OneShotRoutine());
        }

        // 대치 시작 → 마을 소리 쉬고, 바람 줄이기
        private void HandleRoundStarted(int round)
        {
            isQuiet = true;
            FadeWind(fullVolume * standoffVolumeRate, standoffFadeTime);
        }

        // 판정 끝 → 다시 원래대로
        private void HandleRoundEnded(RoundResult result)
        {
            isQuiet = false;
            FadeWind(fullVolume, standoffFadeTime);
        }

        private void FadeWind(float target, float time)
        {
            if (windSource.clip == null) return;
            if (windFade != null) StopCoroutine(windFade);
            windFade = StartCoroutine(FadeRoutine(target, time));
        }

        private IEnumerator FadeRoutine(float target, float time)
        {
            float from = windSource.volume;
            float timer = 0f;
            while (timer < time)
            {
                timer += Time.deltaTime;
                windSource.volume = Mathf.Lerp(from, target, timer / time);
                yield return null;
            }
            windSource.volume = target;
        }

        // 무작위 간격으로 먼 곳에서 마을 소리. 대치 중이면 그 차례는 건너뛴다
        private IEnumerator OneShotRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(oneShotInterval.x, oneShotInterval.y));
                if (isQuiet) continue;
                PlayOneShotFar();
            }
        }

        // 플레이어(Main Camera) 둘레 아무 방향 · 먼 거리에서 3D 로 한 번
        private void PlayOneShotFar()
        {
            AudioClip clip = PickOneShot();
            if (clip == null) return;

            Camera head = Camera.main;
            Vector3 center = head != null ? head.transform.position : transform.position;
            Vector2 dir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(oneShotDistance.x, oneShotDistance.y);
            Vector3 position = center + new Vector3(dir.x, 0f, dir.y) * distance;

            GameObject voice = new GameObject($"TownAmbience ({clip.name})");
            voice.transform.position = position;

            AudioSource source = voice.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = oneShotVolume;
            source.pitch = Random.Range(oneShotPitch.x, oneShotPitch.y);
            source.spatialBlend = 1f;
            source.minDistance = 10f;               // 15 ~ 40m 에서 너무 작아지지 않게 (10m 까지는 제 볼륨)
            source.maxDistance = 80f;
            source.rolloffMode = AudioRolloffMode.Linear;   // 먼 소리라 로그 감쇠면 거의 안 들린다
            source.dopplerLevel = 0f;
            SoundManager.Route(source, SoundChannel.Ambience);
            source.Play();

            Destroy(voice, clip.length / source.pitch + 0.1f);
        }

        // 바로 앞과 다른 클립을 무작위로. 하나뿐이면 그것
        private AudioClip PickOneShot()
        {
            if (oneShots.Length == 1) return oneShots[0];

            AudioClip picked = oneShots[Random.Range(0, oneShots.Length)];
            for (int i = 0; i < 4 && picked == lastOneShot; i++)
                picked = oneShots[Random.Range(0, oneShots.Length)];

            lastOneShot = picked;
            return picked;
        }
    }
}
