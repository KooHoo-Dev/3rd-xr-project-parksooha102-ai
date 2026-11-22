using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    // UI 소리. Canvas 맨 위에 하나만 붙이면 그 아래 모든 버튼 · 토글에 소리가 난다.
    //   누름   : 해머 젖히는 "철컥" (RevolverCocking)
    //   올려놓음 : 실린더 한 칸 "틱" (CylinderSpinClick). 레이가 버튼 위에 올라갈 때
    //   버튼마다 따로 붙이지 않는 이유 : 메인화면만 버튼이 열댓 개다. 새 버튼을 만들어도 저절로 소리가 난다
    //
    //   AudioSource 는 필요 없다. 소리를 낼 때마다 잠깐 쓰는 소리 오브젝트를 만든다
    //     '결투 시작' 을 누르면 바로 씬이 바뀌어서 Canvas 가 사라진다.
    //     Canvas 의 AudioSource 로 내면 "철컥" 이 시작하자마자 끊긴다 → 씬이 바뀌어도 남는 오브젝트로 낸다
    public class UISound : MonoBehaviour
    {
        [Header("클립")]
        [SerializeField] private AudioClip clickClip;   // RevolverCocking
        [SerializeField] private AudioClip hoverClip;   // CylinderSpinClick (비워 두면 올려놓음 소리 없음)

        [Header("설정")]
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.3f;   // 레이를 쓸고 지나가면 연달아 나니까 작게
        [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        private int lastClickFrame = -1;   // 같은 프레임에 두 번 나지 않게 (아래 HandleToggleChanged 참고)

        private void Awake()
        {
            // 꺼져 있는 화면(게임 시작 · 홀스터 조정 ...)의 버튼까지 다 찾는다 (true = 꺼진 것도 포함)
            foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
            {
                if (selectable is Button button)
                    button.onClick.AddListener(PlayClick);
                else if (selectable is Toggle toggle)
                    toggle.onValueChanged.AddListener(HandleToggleChanged);

                // 올려놓음은 버튼마다 Event Trigger(Unity 기본 컴포넌트)를 달아서 PointerEnter 를 듣는다
                //   (Button 에는 "올려놓았다" 이벤트가 없다)
                if (hoverClip != null)
                    AddHoverListener(selectable);
            }
        }

        // selectable 에 레이가 올라오면 PlayHover 를 부르게 한다
        private void AddHoverListener(Selectable selectable)
        {
            if (selectable.TryGetComponent(out EventTrigger trigger) == false)
                trigger = selectable.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener(_ => PlayHover(selectable));
            trigger.triggers.Add(entry);
        }

        // 토글은 켜질 때만. Toggle Group 이라 하나를 켜면 다른 하나가 꺼지는데, 그때까지 소리가 나면 두 번 난다
        //   MainMenu 가 '게임 시작' 을 누를 때 코드로 난이도를 켜는 것도 여기로 온다 → 같은 프레임이라 한 번만 난다
        private void HandleToggleChanged(bool isOn)
        {
            if (isOn) PlayClick();
        }

        public void PlayClick()
        {
            if (lastClickFrame == Time.frameCount) return;
            lastClickFrame = Time.frameCount;

            PlayDetached(clickClip, clickVolume);
        }

        public void PlayHover(Selectable target)
        {
            // 눌 수 없는 버튼(Interactable 꺼짐) 위에서는 소리 없음
            if (target.IsInteractable() == false) return;

            PlayDetached(hoverClip, hoverVolume);
        }

        // 씬이 바뀌어도 끝까지 나는 소리. 다 나면 스스로 사라진다
        private void PlayDetached(AudioClip clip, float volume)
        {
            if (clip == null) return;

            GameObject voice = new GameObject($"UISound ({clip.name})");
            DontDestroyOnLoad(voice);

            AudioSource source = voice.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.spatialBlend = 0f;   // 2D. 판이 눈앞이라 방향은 필요 없다
            source.ignoreListenerPause = true;   // 일시 정지(GamePause)로 소리가 멈춰도 메뉴 버튼 소리는 난다
            SoundManager.Route(source, SoundChannel.Sfx);
            source.Play();

            // 피치를 낮추면 소리가 길어진다 → 길이 ÷ 피치 만큼 기다렸다가 지운다
            Destroy(voice, clip.length / source.pitch + 0.1f);
        }
    }
}
