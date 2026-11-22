using System;
using System.Collections;
using Game.Table;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;   // InputActionReference, InputAction (컨트롤러 트리거)
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game
{
    // 메인화면 (Title 씬). 난이도를 고르고 홀스터 자리를 맞춘 뒤, 결투 씬(Town)을 연다.
    //   화면은 여섯 장이고, 한 번에 한 장만 켠다
    //     첫 화면     : '게임 시작' · '홀스터 조정' · '음량 조절'
    //     음량 조절   : 슬라이더 넷 (전체 · 배경음악 · 효과음 · 환경음) · '뒤로'. 슬라이더는 VolumeSlider 가 SoundManager 와 잇는다
    //     게임 시작   : 난이도 셋 · '뒤로' · '결투 시작'
    //     홀스터 조정 : 조정 버튼들 · '처음 자리' · '빠른 조정' · '뒤로'
    //     주 손 고르기 : '왼손잡이' · '오른손잡이' · '뒤로'        (빠른 조정 1단계)
    //     손 따라가기 : 안내 글자만. 주 손 트리거를 누르면 확정    (빠른 조정 2단계)
    //   이 오브젝트가 서 있는 자리 = 플레이어가 설 자리. 판(Panel)은 자식이라 그 앞에 떠 있다
    //   고른 값은 GameSettings 에 넣어 두면 Town 이 꺼내 쓴다
    public class MainMenu : MonoBehaviour
    {
        // 결투 씬 이름. Build Profiles 의 Scene List 에 들어 있어야 열 수 있다
        private const string DuelSceneName = "Town";

        [Header("연결")]
        [SerializeField] private PlayerPlacer playerPlacer;     // 플레이어를 이 자리에 세운다 (홀스터도 허리춤에 붙는다)
        [SerializeField] private HolsterAnchor holsterAnchor;   // 조정 버튼을 누르면 홀스터를 옮긴다

        [Header("연결 - 컨트롤러 (빠른 조정)")]
        [SerializeField] private Transform leftController;            // XR Origin → Camera Offset → Left Controller
        [SerializeField] private Transform rightController;           // XR Origin → Camera Offset → Right Controller
        [SerializeField] private InputActionReference leftTrigger;    // XRI Left Interaction/Activate (왼손 트리거)
        [SerializeField] private InputActionReference rightTrigger;   // XRI Right Interaction/Activate (오른손 트리거)

        [Header("연결 - 첫 화면")]
        [SerializeField] private GameObject homePage;
        [SerializeField] private Button duelMenuButton;      // 게임 시작 → 게임 시작 화면
        [SerializeField] private Button holsterMenuButton;   // 홀스터 조정 → 홀스터 조정 화면
        [SerializeField] private Button volumeMenuButton;    // 음량 조절 → 음량 조절 화면

        [Header("연결 - 게임 시작 화면")]
        [SerializeField] private GameObject duelPage;
        [SerializeField] private Toggle easyToggle;          // 셋 다 같은 Toggle Group 에 들어 있다 → 하나만 켜진다
        [SerializeField] private Toggle normalToggle;
        [SerializeField] private Toggle hardToggle;
        [SerializeField] private Button startButton;         // 결투 시작
        [SerializeField] private Button duelBackButton;      // 뒤로 → 첫 화면

        [Header("연결 - 홀스터 조정 화면")]
        [SerializeField] private GameObject holsterPage;
        [SerializeField] private TMP_Text holsterText;       // 지금 홀스터 자리 (머리에서 아래 60cm · 오른쪽 20cm · 앞 6cm)
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;
        [SerializeField] private Button forwardButton;
        [SerializeField] private Button backwardButton;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private Button resetButton;         // 처음 자리
        [SerializeField] private Button quickButton;         // 빠른 조정 → 주 손 고르기 화면
        [SerializeField] private Button holsterBackButton;   // 뒤로 → 첫 화면

        [Header("연결 - 빠른 조정 화면")]
        [SerializeField] private GameObject handPage;        // 주 손 고르기
        [SerializeField] private Button leftHandButton;      // 왼손잡이
        [SerializeField] private Button rightHandButton;     // 오른손잡이
        [SerializeField] private Button handBackButton;      // 뒤로 → 홀스터 조정 화면
        [SerializeField] private GameObject followPage;      // 손 따라가기
        [SerializeField] private TMP_Text followText;        // "오른손을 홀스터를 둘 자리에 대고 ..."

        [Header("연결 - 음량 조절 화면")]
        [SerializeField] private GameObject volumePage;      // 슬라이더는 VolumeSlider 가 알아서 하므로 여기서 다루지 않는다
        [SerializeField] private Button volumeBackButton;    // 뒤로 → 첫 화면

        [Header("설정")]
        [SerializeField] private float step = 0.02f;         // 버튼 한 번에 홀스터를 옮기는 거리 (m). 0.02 = 2cm

        // 빠른 조정 2단계(손 따라가기) 중인가? 이때만 Update 에서 홀스터가 주 손을 따라간다
        private bool isFollowing;
        private Transform followHand;        // 따라갈 손 (주 손 컨트롤러)
        private InputAction confirmTrigger;  // 그 손의 트리거. 누르면 확정

        private void OnEnable()
        {
            duelMenuButton.onClick.AddListener(HandleDuelMenuClicked);
            holsterMenuButton.onClick.AddListener(HandleHolsterMenuClicked);
            volumeMenuButton.onClick.AddListener(HandleVolumeMenuClicked);
            volumeBackButton.onClick.AddListener(HandleBackClicked);

            startButton.onClick.AddListener(HandleStartClicked);
            duelBackButton.onClick.AddListener(HandleBackClicked);

            upButton.onClick.AddListener(HandleUpClicked);
            downButton.onClick.AddListener(HandleDownClicked);
            forwardButton.onClick.AddListener(HandleForwardClicked);
            backwardButton.onClick.AddListener(HandleBackwardClicked);
            leftButton.onClick.AddListener(HandleLeftClicked);
            rightButton.onClick.AddListener(HandleRightClicked);
            resetButton.onClick.AddListener(HandleResetClicked);
            quickButton.onClick.AddListener(HandleQuickClicked);
            holsterBackButton.onClick.AddListener(HandleBackClicked);

            leftHandButton.onClick.AddListener(HandleLeftHandClicked);
            rightHandButton.onClick.AddListener(HandleRightHandClicked);
            handBackButton.onClick.AddListener(HandleHandBackClicked);
        }

        private void OnDisable()
        {
            duelMenuButton.onClick.RemoveListener(HandleDuelMenuClicked);
            holsterMenuButton.onClick.RemoveListener(HandleHolsterMenuClicked);
            volumeMenuButton.onClick.RemoveListener(HandleVolumeMenuClicked);
            volumeBackButton.onClick.RemoveListener(HandleBackClicked);

            startButton.onClick.RemoveListener(HandleStartClicked);
            duelBackButton.onClick.RemoveListener(HandleBackClicked);

            upButton.onClick.RemoveListener(HandleUpClicked);
            downButton.onClick.RemoveListener(HandleDownClicked);
            forwardButton.onClick.RemoveListener(HandleForwardClicked);
            backwardButton.onClick.RemoveListener(HandleBackwardClicked);
            leftButton.onClick.RemoveListener(HandleLeftClicked);
            rightButton.onClick.RemoveListener(HandleRightClicked);
            resetButton.onClick.RemoveListener(HandleResetClicked);
            quickButton.onClick.RemoveListener(HandleQuickClicked);
            holsterBackButton.onClick.RemoveListener(HandleBackClicked);

            leftHandButton.onClick.RemoveListener(HandleLeftHandClicked);
            rightHandButton.onClick.RemoveListener(HandleRightHandClicked);
            handBackButton.onClick.RemoveListener(HandleHandBackClicked);
        }

        private void Start()
        {
            // ① 첫 화면만 켠다
            ShowPage(homePage);

            // ② 홀스터 자리를 글자로 적어 둔다 (홀스터는 HolsterAnchor 가 Awake 에서 이미 이 자리로 옮겨 놨다)
            //    꺼진 화면의 글자도 바꿀 수 있다. 화면을 켜면 바뀐 글자가 보인다
            UpdateHolsterText();

            // ③ 플레이어를 이 자리에 세우고 판 쪽을 보게 한다
            //    PlayerPlacer 가 머리 추적이 잡힐 때까지 기다렸다가 세운다 (Town 의 StageManager 와 같은 방법)
            StartCoroutine(PlaceAndFadeIn());
        }

        // 플레이어를 세운 다음 암전을 걷는다 (자리 잡는 모습이 안 보이게)
        private IEnumerator PlaceAndFadeIn()
        {
            yield return playerPlacer.PlaceFirst(transform.position, transform.rotation);
            ScreenFader.FadeIn();
        }

        private void Update()
        {
            // 빠른 조정 중이 아니면 할 일이 없다
            if (isFollowing == false) return;

            // ① 홀스터가 주 손을 따라간다 (총도 소켓을 따라 같이 온다)
            holsterAnchor.MoveHolsterTo(followHand.position);

            // ② 주 손 트리거를 "막 누른" 프레임에 확정한다
            //    WasPressedThisFrame : 누르고 있는 동안 내내가 아니라, 눌린 그 한 프레임만 true
            //    '오른손잡이' 버튼을 레이로 누를 때 쓴 트리거는 이미 뗀 뒤라서 확정으로 세지 않는다
            //    (UI 버튼은 트리거를 "뗄 때" 눌린다)
            if (confirmTrigger.WasPressedThisFrame())
                ConfirmFollow();
        }

        // ---------------------------------------------------------------- 화면 바꾸기

        // page 한 장만 켜고 나머지는 끈다
        //   page == homePage 는 true / false 다 → 같은 화면이면 켜고(true), 다르면 끈다(false)
        private void ShowPage(GameObject page)
        {
            homePage.SetActive(page == homePage);
            duelPage.SetActive(page == duelPage);
            holsterPage.SetActive(page == holsterPage);
            handPage.SetActive(page == handPage);
            followPage.SetActive(page == followPage);
            volumePage.SetActive(page == volumePage);
        }

        // '게임 시작' : 게임 시작 화면을 펼친다. 지금 난이도(처음엔 보통)가 골라진 채로 연다
        //   화면을 켠 "다음에" 골라야 한다. Toggle Group 은 켜져 있을 때만 "나머지 둘을 끄는" 일을 한다
        //   그래서 고르다가 뒤로 가면 고르던 것은 취소되고, 다시 열면 지금 난이도로 돌아온다
        private void HandleDuelMenuClicked()
        {
            ShowPage(duelPage);
            GetToggle(GameSettings.Difficulty).isOn = true;
        }

        // '홀스터 조정' : 홀스터 조정 화면을 펼친다
        private void HandleHolsterMenuClicked()
        {
            ShowPage(holsterPage);
        }

        // '음량 조절' : 음량 조절 화면을 펼친다. 슬라이더는 켜질 때 저장된 음량을 스스로 읽어 온다 (VolumeSlider.OnEnable)
        private void HandleVolumeMenuClicked()
        {
            ShowPage(volumePage);
        }

        // '뒤로' : 첫 화면으로 (게임 시작 · 홀스터 조정 · 음량 조절 화면의 뒤로 버튼이 같이 쓴다)
        //   음량 조절 화면을 닫으면 VolumeSlider.OnDisable 이 음량을 저장한다
        private void HandleBackClicked()
        {
            ShowPage(homePage);
        }

        // ---------------------------------------------------------------- 게임 시작 화면

        // '결투 시작' : 고른 난이도를 넣어 두고 결투 씬을 연다
        private void HandleStartClicked()
        {
            GameSettings.Difficulty = GetSelectedDifficulty();
            Debug.Log($"[MainMenu] {GameSettings.Difficulty} 난이도로 결투 시작");
            ScreenFader.LoadScene(DuelSceneName);   // 암전한 뒤 연다
        }

        // 난이도 → 그 난이도의 Toggle
        private Toggle GetToggle(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    return easyToggle;
                case Difficulty.Normal:
                    return normalToggle;
                case Difficulty.Hard:
                    return hardToggle;
                default:
                    // Difficulty 에 난이도를 새로 추가하고 여기를 안 고쳤다
                    throw new Exception($"[MainMenu] {difficulty} 난이도의 Toggle 이 없습니다.");
            }
        }

        // 켜져 있는 Toggle → 난이도
        private Difficulty GetSelectedDifficulty()
        {
            if (easyToggle.isOn) return Difficulty.Easy;
            if (normalToggle.isOn) return Difficulty.Normal;
            if (hardToggle.isOn) return Difficulty.Hard;

            // Toggle Group 의 Allow Switch Off 가 켜져 있으면 셋 다 꺼질 수 있다
            throw new Exception("[MainMenu] 켜진 난이도 Toggle 이 없습니다. DifficultyGroup 의 Toggle Group → Allow Switch Off 를 꺼 주세요.");
        }

        // ---------------------------------------------------------------- 홀스터 조정 화면

        // 조정 버튼 : 누른 쪽으로 step 만큼 옮긴다
        //   방향은 HolsterAnchor 기준 (플레이어가 보는 쪽이 앞)
        //   Vector3.up = (0, 1, 0) 위,  Vector3.forward = (0, 0, 1) 앞,  Vector3.right = (1, 0, 0) 오른쪽
        private void HandleUpClicked() => MoveHolster(Vector3.up);
        private void HandleDownClicked() => MoveHolster(Vector3.down);
        private void HandleForwardClicked() => MoveHolster(Vector3.forward);
        private void HandleBackwardClicked() => MoveHolster(Vector3.back);
        private void HandleLeftClicked() => MoveHolster(Vector3.left);
        private void HandleRightClicked() => MoveHolster(Vector3.right);

        // '처음 자리' : 기본 자리로 되돌린다
        private void HandleResetClicked()
        {
            ApplyHolster(GameSettings.DefaultHolsterOffset);
        }

        // 지금 자리에서 direction 쪽으로 step 만큼
        private void MoveHolster(Vector3 direction)
        {
            ApplyHolster(GameSettings.HolsterOffset + direction * step);
        }

        // 홀스터를 offset 자리로 옮기고, 저장하고, 글자를 고친다
        private void ApplyHolster(Vector3 offset)
        {
            GameSettings.SetHolsterOffset(offset);   // 저장 (Town 의 HolsterAnchor 가 이 값을 쓴다)
            holsterAnchor.SetOffset(offset);          // 눈앞의 홀스터를 옮긴다 (뽑아 둔 총도 다시 꽂힌다)
            UpdateHolsterText();
        }

        // 홀스터 자리 글자 : m 를 cm 로 바꿔서(× 100) 소수점 없이 쓴다
        //   y 는 머리보다 아래라서 - 값이다 → - 를 한 번 더 붙여 "아래 60cm" 처럼 + 로 쓴다
        //   x 는 + 면 오른쪽, - 면 왼쪽 (왼손잡이는 홀스터가 왼쪽 허리에 온다). z 도 같은 식으로 앞 / 뒤
        private void UpdateHolsterText()
        {
            holsterText.SetText(FormatHolsterOffset(GameSettings.HolsterOffset));
        }

        // 홀스터 자리 → "머리에서 아래 60cm · 오른쪽 20cm · 앞 6cm" (일시 정지 판의 홀스터 조정도 같이 쓴다)
        public static string FormatHolsterOffset(Vector3 offset)
        {
            string side;
            if (offset.x >= 0f)
                side = $"오른쪽 {offset.x * 100f:0}cm";
            else
                side = $"왼쪽 {-offset.x * 100f:0}cm";

            string depth;
            if (offset.z >= 0f)
                depth = $"앞 {offset.z * 100f:0}cm";
            else
                depth = $"뒤 {-offset.z * 100f:0}cm";

            return $"머리에서 아래 {-offset.y * 100f:0}cm · {side} · {depth}";
        }

        // ---------------------------------------------------------------- 빠른 조정

        // '빠른 조정' : 주 손 고르기 화면으로
        private void HandleQuickClicked()
        {
            ShowPage(handPage);
        }

        // '뒤로' (주 손 고르기 화면) : 홀스터 조정 화면으로
        private void HandleHandBackClicked()
        {
            ShowPage(holsterPage);
        }

        // '왼손잡이' / '오른손잡이' : 그 손을 따라가기 시작한다
        private void HandleLeftHandClicked()
        {
            StartFollow(leftController, leftTrigger, "왼손");
        }

        private void HandleRightHandClicked()
        {
            StartFollow(rightController, rightTrigger, "오른손");
        }

        // 빠른 조정 2단계 시작 : 이제부터 홀스터가 hand 를 따라가고, trigger 를 누르면 확정한다
        private void StartFollow(Transform hand, InputActionReference trigger, string handName)
        {
            // 트리거 입력이 꺼져 있으면 영영 확정할 수 없어서 이 화면에 갇힌다 → 바로 알린다
            //   (XR Origin 의 Input Action Manager 가 XRI Default Input Actions 를 켠다)
            if (trigger.action.enabled == false)
                throw new Exception($"[MainMenu] {trigger.action.actionMap.name}/{trigger.action.name} 입력이 꺼져 있습니다. XR Origin 의 Input Action Manager → Action Assets 에 XRI Default Input Actions 가 들어 있는지 확인해 주세요.");

            // ① 허리춤 기준을 지금 머리로 다시 잡는다
            //    지금은 판을 보고 있어서 머리 방향 = 몸 정면이다. Town 도 정면을 볼 때 기준을 잡으므로 둘이 맞는다
            //    (확정할 때는 허리를 내려다보느라 머리가 앞으로 숙여져 있다. 그때 잡으면 기준이 어긋난다)
            holsterAnchor.ResetPosition();

            // ② 따라갈 손과 확정 트리거를 기억하고, 안내 글자를 바꾼다
            //    "왼손" · "오른손" 은 둘 다 받침이 있어서 뒤에 '을' 이 붙는다
            followHand = hand;
            confirmTrigger = trigger.action;
            isFollowing = true;
            followText.SetText($"{handName}을 홀스터를 둘 자리에 대고\n{handName} 트리거를 누르세요");
            ShowPage(followPage);
            Debug.Log($"[MainMenu] 빠른 조정 : 홀스터가 {handName}을 따라갑니다");
        }

        // 빠른 조정 확정 : 손이 있던 자리를 저장하고 홀스터 조정 화면으로 돌아간다
        private void ConfirmFollow()
        {
            isFollowing = false;

            // 홀스터는 Update 에서 손 자리로 옮겨 놨다 → 그 자리(허리춤 기준)를 그대로 저장한다
            //   ApplyHolster 가 저장 · 총 다시 꽂기 · 글자 고치기를 한 번에 한다 (조정 버튼과 같은 길)
            ApplyHolster(holsterAnchor.GetOffset());
            Debug.Log($"[MainMenu] 빠른 조정 확정 : 홀스터 자리 {GameSettings.HolsterOffset:F2}");

            ShowPage(holsterPage);   // 확정한 자리를 버튼으로 조금씩 더 다듬을 수 있다
        }
    }
}
