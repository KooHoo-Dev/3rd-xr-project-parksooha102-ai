using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    // 음량 슬라이더 (탄띠). Slider 오브젝트에 붙이고 갈래(channel)만 고르면 SoundManager 와 이어진다.
    //   가죽 탄띠에 샷건 쉘이 음량만큼 꽂힌다. 10발 = 100%, 한 발 = 10%
    //   값도 10% 씩 딱딱 맞춘다 → 꽂힌 쉘 수와 숫자가 늘 같다 (쉘 반 발 같은 어중간한 값이 없다)
    //   0% 는 숫자 대신 "끔"
    //
    //   켜질 때 저장된 음량을 보여 주고, 끄는 동안 바로 소리에 반영한다
    //   놓을 때(트리거를 뗄 때) 한 번 저장한다. 끄는 동안 매 프레임 파일에 쓰지 않게
    //   쉘 · 글자 칸은 WantedPosterLayout(메인화면 판 정렬)이 만들어 꽂는다
    [RequireComponent(typeof(Slider))]
    public class VolumeSlider : MonoBehaviour, IPointerUpHandler
    {
        private const string MutedText = "끔";

        [Header("설정")]
        [SerializeField] private SoundChannel channel = SoundChannel.Master;

        [Header("연결")]
        [SerializeField] private TMP_Text valueText;     // "70%" · "끔" (비워 두면 숫자 표시 없음)
        [SerializeField] private GameObject[] shells;    // 탄띠 고리에 꽂힌 쉘. 왼쪽부터. 개수 = 칸 수 (보통 10)

        private Slider slider;

        // 칸 수 (쉘 수). 쉘을 안 꽂았으면 10칸으로 본다
        private int Steps => shells != null && shells.Length > 0 ? shells.Length : 10;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }

        private void OnEnable()
        {
            // 알림 없이 값만 넣는다 (넣자마자 HandleValueChanged 가 불려 저장하지 않게)
            float saved = Snap(SoundManager.GetVolume(channel));
            slider.SetValueWithoutNotify(saved);
            Show(saved);
            slider.onValueChanged.AddListener(HandleValueChanged);
        }

        private void OnDisable()
        {
            slider.onValueChanged.RemoveListener(HandleValueChanged);
            SoundManager.Save();   // 설정 화면을 닫을 때도 저장
        }

        // 끄는 대로 가장 가까운 칸에 맞춘다. 칸이 바뀔 때만 소리 · 그림을 고친다
        private void HandleValueChanged(float value)
        {
            float snapped = Snap(value);
            if (Mathf.Approximately(snapped, value) == false)
                slider.SetValueWithoutNotify(snapped);

            if (Mathf.Approximately(snapped, SoundManager.GetVolume(channel))) return;

            SoundManager.SetVolume(channel, snapped);
            Show(snapped);
        }

        // 레이 트리거를 뗐다 = 다 맞췄다 → 저장
        public void OnPointerUp(PointerEventData eventData)
        {
            SoundManager.Save();
        }

        private float Snap(float value)
        {
            return Mathf.Round(value * Steps) / Steps;
        }

        // 쉘을 음량만큼 꽂고, 숫자를 고친다
        private void Show(float value)
        {
            int filled = Mathf.RoundToInt(value * Steps);
            if (shells != null)
            {
                for (int i = 0; i < shells.Length; i++)
                    shells[i].SetActive(i < filled);
            }

            if (valueText != null)
                valueText.SetText(filled == 0 ? MutedText : $"{Mathf.RoundToInt(value * 100f)}%");
        }
    }
}
