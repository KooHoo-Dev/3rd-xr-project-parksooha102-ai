using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game
{
    // 소리 갈래. 갈래마다 음량을 따로 조절한다 (Master 는 전체)
    public enum SoundChannel
    {
        Master,     // 전체
        Bgm,        // 배경음악 : 타이틀 BGM · 결투 BGM · 승리/패배 효과음
        Sfx,        // 효과음 : 총성 · 신음 · 타격 · 발소리 · 홀스터 · UI · 종 · 야유 ...
        Ambience,   // 환경음 : 바람 · 멀리서 나는 마을 소리
    }

    // [사운드 매니저] 갈래별 음량을 Audio Mixer 로 조절하고, PlayerPrefs 에 저장한다.
    //   GameSettings 처럼 static 이라 씬에 놓지 않는다. Title · Town 어디서든 SoundManager.SetVolume(...) 으로 부른다
    //
    //   Audio Mixer 를 쓰는 이유 : AudioSource 마다 볼륨을 곱하면 소리를 내는 스크립트가 다 매니저를 알아야 하고,
    //     PlayOneShot 으로 이미 나고 있는 소리는 바꿀 수 없다. Mixer 는 갈래(그룹) 하나의 볼륨만 바꾸면
    //     그 그룹으로 나가는 모든 소리가 한 번에, 나고 있는 소리까지 바뀐다
    //
    //   Mixer 에셋 (직접 만든다 : Project 창 → Create → Audio Mixer)
    //     위치 : Assets/Resources/Audio/MainMixer.mixer  (Resources 라서 코드가 이름으로 꺼내 쓴다)
    //     그룹 : Master 아래에 BGM · SFX · Ambience  (이름 그대로)
    //     노출 : 각 그룹 Volume 을 우클릭 → Expose → Exposed Parameters 이름을 아래 상수와 똑같이
    //            MasterVolume · BGMVolume · SFXVolume · AmbienceVolume
    //
    //   AudioSource 를 그룹에 잇는 일 : 소리를 내는 스크립트가 Awake 에서 SoundManager.Route(소리, 갈래) 를 부른다.
    //     코드로 만드는 소리(스치는 총알 · UI · 마을 소리 ...)도 만들 때 Route 한다.
    //     스크립트 없는 AudioSource(Title 의 TitleBGM)만 Inspector 의 Output 칸에 BGM 그룹을 직접 꽂는다
    public static class SoundManager
    {
        private const string MixerPath = "Audio/MainMixer";   // Resources/Audio/MainMixer.mixer

        // 갈래 → (그룹 이름, 노출한 파라미터 이름, PlayerPrefs 이름). SoundChannel 순서와 같다
        private static readonly string[] GroupNames = { "Master", "BGM", "SFX", "Ambience" };
        private static readonly string[] ParamNames = { "MasterVolume", "BGMVolume", "SFXVolume", "AmbienceVolume" };
        private static readonly string[] PrefKeys = { "VolumeMaster", "VolumeBgm", "VolumeSfx", "VolumeAmbience" };

        private const float MinDb = -80f;   // Mixer 볼륨의 가장 작은 값 (= 소리 없음)

        private static AudioMixer mixer;
        private static readonly AudioMixerGroup[] groups = new AudioMixerGroup[4];
        private static readonly float[] volumes = new float[4];   // 0 ~ 1 (슬라이더 값)

        // 앱이 켜질 때(첫 씬을 열기 전) Mixer 를 찾고 저장한 음량을 읽는다 (GameSettings.Boot 와 같은 방식)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            mixer = Resources.Load<AudioMixer>(MixerPath);
            if (mixer == null)
                throw new Exception($"[SoundManager] Assets/Resources/{MixerPath}.mixer 가 없습니다. Project 창 → Create → Audio Mixer 로 만들고 이름을 MainMixer 로 해 주세요.");

            for (int i = 0; i < groups.Length; i++)
            {
                // FindMatchingGroups 는 이름이 들어간 그룹을 다 준다 (Master 는 맨 위, 나머지는 Master 아래)
                AudioMixerGroup[] found = mixer.FindMatchingGroups(GroupNames[i]);
                if (found.Length == 0)
                    throw new Exception($"[SoundManager] MainMixer 에 {GroupNames[i]} 그룹이 없습니다. Master 아래에 BGM · SFX · Ambience 그룹을 만들어 주세요.");
                groups[i] = found[0];

                // 노출한 파라미터가 있나? (GetFloat 는 없는 이름이면 false)
                if (mixer.GetFloat(ParamNames[i], out _) == false)
                    throw new Exception($"[SoundManager] MainMixer 에 노출한 파라미터 {ParamNames[i]} 가 없습니다. {GroupNames[i]} 그룹 Volume 을 우클릭 → Expose 하고, Exposed Parameters 에서 이름을 {ParamNames[i]} 로 바꿔 주세요.");

                volumes[i] = PlayerPrefs.GetFloat(PrefKeys[i], 1f);   // 저장한 적 없으면 100%
            }

            // Mixer 값은 씬이 바뀌어도 남지만, 첫 씬의 Awake 전에 넣은 값은 무시될 때가 있다 (Unity 알려진 문제)
            //   → 씬이 열린 뒤마다 다시 넣는다
            SceneManager.sceneLoaded += (scene, mode) => ApplyAll();
        }

        // ---------------------------------------------------------------- API

        // 갈래 음량 (0 ~ 1)
        public static float GetVolume(SoundChannel channel)
        {
            return volumes[(int)channel];
        }

        // 갈래 음량을 바꾼다 (0 ~ 1). 저장은 Save 에서 한 번에 (슬라이더를 끄는 동안 매 프레임 파일에 쓰지 않게)
        public static void SetVolume(SoundChannel channel, float volume)
        {
            int i = (int)channel;
            volumes[i] = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefKeys[i], volumes[i]);
            Apply(i);
        }

        // 바꾼 음량을 파일에 쓴다. 슬라이더를 놓을 때 · 설정 화면을 닫을 때 부른다
        //   Quest 는 헤드셋을 벗으면 그냥 꺼질 때가 많아서 앱 종료 때 저장을 믿지 않는다 (GameSettings 와 같은 이유)
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        // AudioSource 를 갈래 그룹으로 잇는다. 소리를 내는 스크립트가 Awake 에서 · 코드로 소리를 만들 때 부른다
        public static void Route(AudioSource source, SoundChannel channel)
        {
            source.outputAudioMixerGroup = groups[(int)channel];
        }

        // AudioSource.PlayClipAtPoint 와 같지만 갈래 그룹을 거친다 (원래 것은 그룹을 고를 수 없어서 음량 조절이 안 먹는다)
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position, SoundChannel channel, float volume = 1f)
        {
            if (clip == null) return;

            GameObject voice = new GameObject($"OneShot ({clip.name})");
            voice.transform.position = position;
            AudioSource source = voice.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            Route(source, channel);
            source.Play();
            Object.Destroy(voice, clip.length + 0.1f);
        }

        // ---------------------------------------------------------------- 내부

        private static void ApplyAll()
        {
            for (int i = 0; i < volumes.Length; i++)
                Apply(i);
        }

        // 0 ~ 1 → 데시벨. 귀는 곱셈으로 듣기 때문에 슬라이더 가운데(0.5)가 "절반 크기"로 들리려면 로그로 바꿔야 한다
        //   1 → 0dB (원래 크기),  0.5 → -6dB,  0.1 → -20dB,  0 → -80dB (소리 없음)
        private static void Apply(int i)
        {
            float v = volumes[i];
            float db = v <= 0.0001f ? MinDb : Mathf.Max(MinDb, 20f * Mathf.Log10(v));
            mixer.SetFloat(ParamNames[i], db);
        }
    }
}
