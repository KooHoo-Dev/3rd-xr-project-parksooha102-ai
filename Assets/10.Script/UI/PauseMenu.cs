using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    // 일시 정지 판 (Town 씬). 왼손 메뉴 버튼(에디터에서는 Esc)으로 열고 닫는다.
    //   화면은 세 장이고, 한 번에 한 장만 켠다 (MainMenu 와 같은 방식)
    //     첫 화면     : 간판 '일시 정지' 아래 '계속' · '홀스터 조정' · '음량 조절' · '메인으로'
    //     홀스터 조정 : 조정 버튼들 · '처음 자리' · '빠른 조정' · '뒤로'
    //     주 손 고르기 : '왼손잡이' · '오른손잡이' · '뒤로'        (빠른 조정 1단계)
    //     손 따라가기 : 안내 글자만. 주 손 트리거를 누르면 확정    (빠른 조정 2단계)
    //     음량 조절   : 탄띠 슬라이더 넷 · '뒤로'           (슬라이더는 VolumeSlider 가 SoundManager 와 잇는다)
    //   빠른 조정은 메인화면(MainMenu)과 같은 흐름이다. 다른 점 하나 : 시작할 때 총을 홀스터에 꽂는다
    //     → 주 손에 총을 들고 있었어도 확정 트리거가 격발로 이어지지 않는다 (홀스터에 꽂힌 총은 트리거를 읽지 않는다)
    //   판 모양은 WantedPosterLayout 의 "일시 정지 판 정렬" 이 만든다
    //
    //   멈추기 · 풀기는 GamePause 가 한다 (시간 · 소리). 여기는 언제 멈출지와 판을 맡는다
    //     드로우 중(신호 ~ 판정)에는 열리지 않는다 : 멈춘 채 손을 준비하고 풀면 반응속도를 속일 수 있다
    //     멈춘 동안 총을 뽑으면 도로 꽂는다 : 대치 중에 뽑으면 반칙이라서 (CombatManager 도 뽑기를 알리지 않는다)
    public class PauseMenu : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform head;                // XR Origin 의 Main Camera. 판을 이 앞에 띄운다
        [SerializeField] private HolsterAnchor holsterAnchor;   // 홀스터 조정 버튼이 홀스터를 옮긴다
        [SerializeField] private GameObject panel;              // 판 전체 (캔버스). 멈출 때만 켠다

        [Header("연결 - 첫 화면")]
        [SerializeField] private GameObject homePage;
        [SerializeField] private Button resumeButton;          // 계속
        [SerializeField] private Button holsterMenuButton;     // 홀스터 조정 → 홀스터 조정 화면
        [SerializeField] private Button volumeMenuButton;      // 음량 조절 → 음량 조절 화면
        [SerializeField] private Button mainMenuButton;        // 메인으로 (Title 씬)

        [Header("연결 - 홀스터 조정 화면")]
        [SerializeField] private GameObject holsterPage;
        [SerializeField] private TMP_Text holsterText;
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;
        [SerializeField] private Button forwardButton;
        [SerializeField] private Button backwardButton;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button quickButton;         // 빠른 조정 → 주 손 고르기 화면
        [SerializeField] private Button holsterBackButton;

        [Header("연결 - 빠른 조정 화면")]
        [SerializeField] private GameObject handPage;        // 주 손 고르기
        [SerializeField] private Button leftHandButton;
        [SerializeField] private Button rightHandButton;
        [SerializeField] private Button handBackButton;      // 뒤로 → 홀스터 조정 화면
        [SerializeField] private GameObject followPage;      // 손 따라가기
        [SerializeField] private TMP_Text followText;

        [Header("연결 - 컨트롤러 (빠른 조정)")]
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private InputActionReference leftTrigger;    // XRI Left Interaction/Activate
        [SerializeField] private InputActionReference rightTrigger;   // XRI Right Interaction/Activate

        [Header("연결 - 음량 조절 화면")]
        [SerializeField] private GameObject volumePage;
        [SerializeField] private Button volumeBackButton;

        [Header("설정")]
        [SerializeField] private float distance = 1.3f;   // 머리에서 판까지 (m). 레이로 누르는 거리
        [SerializeField] private float step = 0.02f;      // 홀스터 버튼 한 번에 옮기는 거리 (m). 메인화면과 같다

        private InputAction pauseAction;    // 왼손 메뉴 버튼 · Esc
        private bool gunWasHolstered;       // 멈출 때 총이 홀스터에 있었나 (있었으면 멈춘 동안 뽑아도 도로 꽂는다)

        // 빠른 조정 2단계(손 따라가기) 중인가? 이때만 Update 에서 홀스터가 주 손을 따라간다
        private bool isFollowing;
        private Transform followHand;
        private InputAction confirmTrigger;

        private void Awake()
        {
            if (head == null || holsterAnchor == null || panel == null)
                throw new Exception($"[PauseMenu] {name} 의 연결 칸이 비어 있습니다. PauseMenu ⋮ → 일시 정지 판 정렬 을 눌러 주세요.");

            // 메뉴 버튼은 XRI 기본 입력에 없어서 여기서 만든다
            //   왼손 메뉴 버튼 (Quest 왼쪽 컨트롤러의 ≡). 오른쪽 Oculus 버튼은 시스템이 쓴다
            //   이름이 기기마다 달라서 셋 다 묶는다 : 쓰임(usage) MenuButton · 이름 menu · menuButton
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            pauseAction.AddBinding("<XRController>{LeftHand}/menu");
            pauseAction.AddBinding("<XRController>{LeftHand}/menuButton");
            pauseAction.AddBinding("<Keyboard>/escape");   // 에디터 (XR Device Simulator)

            panel.SetActive(false);
        }

        private void OnEnable()
        {
            pauseAction.performed += HandlePausePressed;
            pauseAction.Enable();

            resumeButton.onClick.AddListener(Resume);
            holsterMenuButton.onClick.AddListener(HandleHolsterMenuClicked);
            volumeMenuButton.onClick.AddListener(HandleVolumeMenuClicked);
            mainMenuButton.onClick.AddListener(HandleMainMenuClicked);

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

            volumeBackButton.onClick.AddListener(HandleBackClicked);
        }

        private void OnDisable()
        {
            pauseAction.performed -= HandlePausePressed;
            pauseAction.Disable();

            resumeButton.onClick.RemoveListener(Resume);
            holsterMenuButton.onClick.RemoveListener(HandleHolsterMenuClicked);
            volumeMenuButton.onClick.RemoveListener(HandleVolumeMenuClicked);
            mainMenuButton.onClick.RemoveListener(HandleMainMenuClicked);

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

            volumeBackButton.onClick.RemoveListener(HandleBackClicked);

            // 멈춘 채 이 판이 사라지면(씬 닫기 등) 게임이 멈춘 채 남지 않게
            if (GamePause.IsPaused)
                GamePause.Resume();
        }

        private void OnDestroy()
        {
            pauseAction?.Dispose();
        }

        private void Update()
        {
            if (GamePause.IsPaused == false) return;

            // 멈춘 동안 홀스터에서 총을 뽑았다 → 도로 꽂는다 (손에서 빼앗아서)
            if (gunWasHolstered && CombatManager.Instance.IsPlayerGunHolstered() == false)
                CombatManager.Instance.HolsterPlayerGun();

            // 빠른 조정 2단계 : 홀스터가 주 손을 따라가고, 주 손 트리거를 "막 누른" 프레임에 확정 (MainMenu 와 같다)
            //   UI 와 입력은 멈춘 동안(시간 0)에도 돌아간다
            if (isFollowing)
            {
                holsterAnchor.MoveHolsterTo(followHand.position);
                if (confirmTrigger.WasPressedThisFrame())
                    ConfirmFollow();
            }
        }

        // ---------------------------------------------------------------- 열기 · 닫기

        private void HandlePausePressed(InputAction.CallbackContext context)
        {
            if (GamePause.IsPaused)
                Resume();
            else
                Pause();
        }

        private void Pause()
        {
            // 드로우 중에는 열지 않는다 (반응속도를 속일 수 있다)
            if (StageManager.Instance.Phase == DuelPhase.Draw)
            {
                Debug.Log("[PauseMenu] 드로우 중에는 멈출 수 없습니다");
                return;
            }

            gunWasHolstered = CombatManager.Instance.IsPlayerGunHolstered();
            GamePause.Pause();

            // 머리 앞 distance(m) 에 판을 띄운다. 위아래로 고개를 숙였어도 판은 똑바로 서게 수평 방향만 쓴다
            //   판은 플레이어와 같은 쪽을 본다 → 플레이어에게 앞면이 보인다 (결과판 · 메인화면 판과 같다)
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = transform.forward;   // 바로 위나 아래를 보고 있으면 지난번 방향 그대로
            forward.Normalize();
            transform.SetPositionAndRotation(head.position + forward * distance, Quaternion.LookRotation(forward));

            UpdateHolsterText();
            ShowPage(homePage);
            panel.SetActive(true);
        }

        // '계속' · 메뉴 버튼 다시 누름
        private void Resume()
        {
            // 손 따라가기 도중에 풀었다 → 확정하지 않은 것으로 보고 저장된 자리로 되돌린다
            if (isFollowing)
            {
                isFollowing = false;
                holsterAnchor.SetOffset(GameSettings.HolsterOffset);
            }

            panel.SetActive(false);   // 음량 조절 화면이 켜져 있었으면 VolumeSlider.OnDisable 이 음량을 저장한다
            SoundManager.Save();
            GamePause.Resume();
        }

        // '메인으로' : 풀고 나서 Title 씬으로 (결과판의 '메인으로' 와 같은 길)
        private void HandleMainMenuClicked()
        {
            Resume();
            StageManager.Instance.ReturnToMainMenu();
        }

        // ---------------------------------------------------------------- 화면 바꾸기

        private void ShowPage(GameObject page)
        {
            homePage.SetActive(page == homePage);
            holsterPage.SetActive(page == holsterPage);
            handPage.SetActive(page == handPage);
            followPage.SetActive(page == followPage);
            volumePage.SetActive(page == volumePage);
        }

        private void HandleHolsterMenuClicked() => ShowPage(holsterPage);
        private void HandleVolumeMenuClicked() => ShowPage(volumePage);
        private void HandleBackClicked() => ShowPage(homePage);

        // ---------------------------------------------------------------- 홀스터 조정 (MainMenu 와 같은 일)

        private void HandleUpClicked() => MoveHolster(Vector3.up);
        private void HandleDownClicked() => MoveHolster(Vector3.down);
        private void HandleForwardClicked() => MoveHolster(Vector3.forward);
        private void HandleBackwardClicked() => MoveHolster(Vector3.back);
        private void HandleLeftClicked() => MoveHolster(Vector3.left);
        private void HandleRightClicked() => MoveHolster(Vector3.right);

        private void HandleResetClicked()
        {
            ApplyHolster(GameSettings.DefaultHolsterOffset);
        }

        private void MoveHolster(Vector3 direction)
        {
            ApplyHolster(GameSettings.HolsterOffset + direction * step);
        }

        // 저장하고, 눈앞의 홀스터를 옮기고, 글자를 고친다
        private void ApplyHolster(Vector3 offset)
        {
            GameSettings.SetHolsterOffset(offset);
            holsterAnchor.SetOffset(offset);
            UpdateHolsterText();
        }

        private void UpdateHolsterText()
        {
            holsterText.SetText(MainMenu.FormatHolsterOffset(GameSettings.HolsterOffset));
        }

        // ---------------------------------------------------------------- 빠른 조정 (MainMenu 와 같은 흐름)

        private void HandleQuickClicked() => ShowPage(handPage);
        private void HandleHandBackClicked() => ShowPage(holsterPage);

        private void HandleLeftHandClicked()
        {
            StartFollow(leftController, leftTrigger, "왼손");
        }

        private void HandleRightHandClicked()
        {
            StartFollow(rightController, rightTrigger, "오른손");
        }

        // 빠른 조정 2단계 시작 : 홀스터가 hand 를 따라가고, trigger 를 누르면 확정한다
        private void StartFollow(Transform hand, InputActionReference trigger, string handName)
        {
            if (trigger.action.enabled == false)
                throw new Exception($"[PauseMenu] {trigger.action.actionMap.name}/{trigger.action.name} 입력이 꺼져 있습니다. XR Origin 의 Input Action Manager → Action Assets 에 XRI Default Input Actions 가 들어 있는지 확인해 주세요.");

            // ① 총을 홀스터에 꽂는다 : 주 손에 총을 들고 있으면 확정 트리거가 격발이 된다
            //    (홀스터에 꽂힌 총은 트리거를 읽지 않는다. 꽂힌 총은 홀스터를 따라 손으로 같이 온다)
            CombatManager.Instance.HolsterPlayerGun();
            gunWasHolstered = true;   // 이제부터 멈춘 동안 뽑아도 도로 꽂는다

            // ② 허리춤 기준을 지금 머리로 다시 잡는다 (판을 보고 있어서 머리 방향 = 몸 정면)
            holsterAnchor.ResetPosition();

            // ③ 따라갈 손과 확정 트리거를 기억하고, 안내 글자를 바꾼다
            followHand = hand;
            confirmTrigger = trigger.action;
            isFollowing = true;
            followText.SetText($"{handName}을 홀스터를 둘 자리에 대고\n{handName} 트리거를 누르세요");
            ShowPage(followPage);
        }

        // 빠른 조정 확정 : 손이 있던 자리를 저장하고 홀스터 조정 화면으로 돌아간다
        private void ConfirmFollow()
        {
            isFollowing = false;
            ApplyHolster(holsterAnchor.GetOffset());
            ShowPage(holsterPage);
        }
    }
}
