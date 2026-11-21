using System.Collections;
using UnityEngine;

namespace Game
{
    // 결투 BGM : 대치가 시작되면 결투 BGM 을 처음부터 틀고, 정오 신호가 오면 끈다.
    //   종은 NoonBell 이 친다. 이 스크립트는 BGM 만 맡는다 (같은 신호를 둘 다 듣고 각자 할 일을 한다)
    //   NoonSun · NoonBell 과 같은 방식으로 StageManager 의 방송만 듣는다
    //   AudioSource : AudioClip = Duel, Play On Awake 끔, Spatial Blend 0(2D), Volume 0.6 쯤 (종이 더 크게 들리게)
    //
    //   1라운드 고조 : NoonSun 처럼 신호 시각(SignalAt)까지 BGM 을 점점 키우고 밝게 만든다
    //     처음에는 작고 먹먹하게 (멀리서 들리듯) → 신호 순간 가장 크고 쨍하게 → 종이 터진다
    //     먹먹함은 같은 오브젝트의 Audio Low Pass Filter (Unity 기본 컴포넌트)로 만든다
    //     2라운드부터는 신호를 예측하기 쉬워지지 않게 고조 없이 그대로 튼다 (해도 이미 정점이다)
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(AudioLowPassFilter))]
    public class DuelSound : MonoBehaviour
    {
        // 필터를 다 열었을 때 값. 사람 귀로 들을 수 있는 가장 높은 소리(약 20000Hz)보다 높아서 필터가 없는 것과 같다
        private const float OpenCutoff = 22000f;

        [Header("설정")]
        [SerializeField] private bool playBgmEveryRound = true;    // 끄면 1라운드 대치에만 BGM 을 튼다

        [Header("설정 - 1라운드 고조")]
        [SerializeField] private float startVolumeRate = 0.4f;     // 시작 볼륨 = AudioSource 볼륨 × 이 값. 신호 때 AudioSource 볼륨까지 커진다
        [SerializeField] private float startCutoff = 600f;         // 시작 때 먹먹함 (Hz). 낮을수록 더 먹먹하다
        [SerializeField] private float buildPower = 2f;            // 고조 곡선. 1 = 일정하게, 2 = 처음엔 천천히 · 끝에 몰아서

        private AudioSource audioSource;
        private AudioLowPassFilter lowPass;
        private float fullVolume;   // 씬에 놓인 AudioSource 볼륨 (고조의 끝 값)

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            lowPass = GetComponent<AudioLowPassFilter>();
            fullVolume = audioSource.volume;
            lowPass.cutoffFrequency = OpenCutoff;
        }

        private void OnEnable()
        {
            StageManager.Instance.RoundStarted += HandleRoundStarted;
            StageManager.Instance.DrawSignaled += HandleDrawSignaled;
            StageManager.Instance.RoundEnded += HandleRoundEnded;
        }

        private void OnDisable()
        {
            StageManager.Instance.RoundStarted -= HandleRoundStarted;
            StageManager.Instance.DrawSignaled -= HandleDrawSignaled;
            StageManager.Instance.RoundEnded -= HandleRoundEnded;
        }

        // ① 대치 시작 → BGM 을 튼다. Play 는 늘 처음(0초)부터 튼다
        //    1라운드는 신호 시각까지 고조시킨다 (StageManager 는 이 방송 전에 SignalAt 을 정해 둔다)
        private void HandleRoundStarted(int round)
        {
            if (round != 1 && playBgmEveryRound == false) return;

            StopAllCoroutines();
            if (round == 1)
            {
                StartCoroutine(BuildRoutine(StageManager.Instance.SignalAt));
            }
            else
            {
                SetBuild(1f);   // 고조 없이 다 연 채로
            }

            audioSource.Play();
        }

        // ② 정오 신호 → BGM 을 끈다 (같은 순간 NoonBell 이 종을 친다)
        private void HandleDrawSignaled()
        {
            StopBgm();
        }

        // ③ 판정 끝 → 신호 전에 반칙으로 끝났으면 BGM 이 아직 나오고 있다. 그때도 끈다
        private void HandleRoundEnded(RoundResult result)
        {
            StopBgm();
        }

        private void StopBgm()
        {
            StopAllCoroutines();
            audioSource.Stop();
            SetBuild(1f);   // 다음에 틀 때를 위해 원래대로
        }

        // 지금부터 신호 시각(signalAt)까지 BGM 을 작고 먹먹한 소리 → 크고 쨍한 소리로 바꾼다
        //   대치 시간은 매번 다르다 (1라운드 5~8초). 그래서 고조 빠르기도 매번 다르다 (NoonSun 의 RiseRoutine 과 같은 방식)
        private IEnumerator BuildRoutine(float signalAt)
        {
            float buildStart = Time.time;
            float buildTime = signalAt - buildStart;

            while (Time.time < signalAt)
            {
                float t = (Time.time - buildStart) / buildTime;   // 0 → 1
                SetBuild(t);
                yield return null;
            }
        }

        // 고조 정도 t (0 = 시작, 1 = 신호 순간) 를 볼륨과 먹먹함에 반영한다
        private void SetBuild(float t)
        {
            float curve = Mathf.Pow(t, buildPower);
            audioSource.volume = Mathf.Lerp(fullVolume * startVolumeRate, fullVolume, curve);

            // 주파수는 귀에 곱셈으로 들린다 (600 → 1200 과 6000 → 12000 이 비슷하게 들린다)
            //   그래서 그냥 Lerp 하면 앞에서 확 밝아지고 끝은 변화가 없다. 로그 단위로 섞는다
            float logCutoff = Mathf.Lerp(Mathf.Log(startCutoff), Mathf.Log(OpenCutoff), curve);
            lowPass.cutoffFrequency = Mathf.Exp(logCutoff);
        }
    }
}
