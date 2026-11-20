using System;
using System.IO;
using TMPro;
using Unity.XR.CoreUtils;        // XROrigin (메인화면 판 : 컨트롤러 찾기)
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;   // InputActionReference (메인화면 판 : 트리거 입력 연결)
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // [에디터 도구] 결과판(ResultBoard) · 라운드 쪽지(RoundBoard) · 메인화면 판(MainMenu)을 수배지 모양으로 정렬한다.
    // 자리 · 크기 · 색을 바꾸고 싶으면 아래 숫자만 고치고 메뉴를 다시 누른다.
    public static class WantedPosterLayout
    {
        private const string UndoName = "수배지 모양으로 정렬";
        private const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";
        private const string PaperFolder = "Assets/30.UI";
        private const string PaperPath = PaperFolder + "/WantedPaper.png";

        // XRI 입력 파일 (XRI Starter Assets 를 가져올 때 같이 온다). 메인화면 판의 트리거 입력을 여기서 꺼낸다
        private const string InputActionsPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/XRI Default Input Actions.inputactions";

        // 크기 (캔버스 칸. 1칸 = 1mm)
        private const int BoardWidth = 600;
        private const int BoardHeight = 900;   // 종이 790 + 아래 버튼 자리
        private const int PaperHeight = 790;
        private const int LineWidth = 500;     // 가로줄 길이
        private const int NoteWidth = 600;     // 라운드 쪽지
        private const int NoteHeight = 300;
        private const int MenuWidth = 700;         // 메인화면 판
        private const int MenuHeight = 815;        // 종이 710 + 아래 버튼 자리 (가장 긴 홀스터 조정 화면에 맞췄다)
        private const int MenuPaperHeight = 710;
        private const int RowHeight = 64;          // 홀스터 조정 줄 (이름표 | 버튼 | 버튼)
        private const int RowLabelX = -215;        // 줄 안에서 가로 자리 (가운데가 0, 오른쪽이 +)
        private const int RowLeftX = -10;
        private const int RowRightX = 200;

        // 메인화면 판을 띄우는 자리 (m). MainMenu 오브젝트(= 플레이어가 설 바닥 자리) 기준
        private const float MenuDistance = 1.5f;   // 앞으로. 결과판처럼 손을 뻗지 않고 레이로 누르는 거리
        private const float MenuCenterHeight = 1.4f;   // 바닥에서 판 가운데까지

        // 잉크 색
        private static readonly Color InkBrown = new Color32(59, 36, 22, 255);     // 짙은 갈색 : 가운데 글자, 가로줄
        private static readonly Color InkRed = new Color32(110, 36, 24, 255);      // 붉은 갈색 : 맨 위 결과 문구, REWARD
        private static readonly Color AccentRed = new Color32(163, 46, 34, 255);   // 빨강 : 가운데 줄 (결투 결과)

        // 종이 색
        private static readonly Color PaperLight = new Color32(233, 212, 166, 255);   // 밝은 종이
        private static readonly Color PaperStain = new Color32(201, 164, 108, 255);   // 얼룩
        private static readonly Color PaperBurnt = new Color32(90, 54, 28, 255);      // 그을린 가장자리

        // 버튼 색 (나무 간판)
        private static readonly Color WoodNormal = new Color32(92, 58, 33, 255);
        private static readonly Color WoodHighlighted = new Color32(130, 86, 50, 255);   // 레이가 올라갔을 때
        private static readonly Color WoodPressed = new Color32(60, 37, 20, 255);        // 눌렀을 때
        private static readonly Color ButtonTextColor = new Color32(243, 230, 200, 255);

        // ResultBoard 의 ⋮ 메뉴. "CONTEXT/컴포넌트이름/메뉴이름" 으로 적으면 그 컴포넌트의 ⋮ 메뉴에 붙는다
        [MenuItem("CONTEXT/ResultBoard/수배지 모양으로 정렬")]
        private static void ArrangeFromContext(MenuCommand command)
        {
            // command.context = ⋮ 를 누른 그 컴포넌트
            Arrange((ResultBoard)command.context);
        }

        [MenuItem("Tools/정오의 결투/결과판 수배지 모양으로 정렬")]
        private static void ArrangeFromTools()
        {
            ResultBoard board = Object.FindFirstObjectByType<ResultBoard>(FindObjectsInactive.Include);
            if (board == null)
                throw new Exception("[WantedPosterLayout] 열려 있는 씬에 ResultBoard 가 없습니다. 빈 오브젝트에 ResultBoard 를 먼저 붙여 주세요.");

            Arrange(board);
        }

        private static void Arrange(ResultBoard board)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("[WantedPosterLayout] Play 를 멈춘 뒤에 눌러 주세요. Play 중에 바꾼 것은 멈추면 사라집니다.");

            // 되돌리기(Ctrl+Z) 준비 : 지금 ResultBoard 아래 모습을 통째로 기억해 둔다
            Undo.RegisterFullObjectHierarchyUndo(board.gameObject, UndoName);

            // ① 재료
            TMP_FontAsset font = LoadFont();
            Sprite paper = LoadOrCreatePaper();

            // ② Panel (캔버스). 캔버스는 붙일 것이 많아서 XR > UI Canvas 로 직접 만들게 한다
            Transform panelTransform = board.transform.Find("Panel");
            if (panelTransform == null)
                throw new Exception("[WantedPosterLayout] ResultBoard 아래에 Panel 이 없습니다. ResultBoard 우클릭 > XR > UI Canvas 로 만들고 이름을 Panel 로 바꿔 주세요.");

            RectTransform panel = (RectTransform)panelTransform;
            panel.localPosition = new Vector3(0f, -0.1f, 0f);          // 눈높이보다 10cm 아래 (m)
            panel.localRotation = Quaternion.identity;
            panel.localScale = new Vector3(0.001f, 0.001f, 0.001f);    // 캔버스 1칸 = 1mm
            panel.sizeDelta = new Vector2(BoardWidth, BoardHeight);

            // ③④ 자식들 (위에서부터 차례로)
            Image background = GetImage(panel, "Background");
            PlaceFromTop(background.rectTransform, 0, BoardWidth, PaperHeight);
            background.sprite = paper;
            background.type = Image.Type.Simple;
            background.color = Color.white;      // 흰색 = 그림 색 그대로
            background.raycastTarget = true;     // 레이가 종이에 닿아 멈추게 (어디를 가리키는지 보인다)

            //    맨 위 WANTED 는 바뀌지 않는다. 여기서 한 번 써 두면 끝이다
            TextMeshProUGUI title = GetText(panel, "Title");
            PlaceFromTop(title.rectTransform, 45, 520, 150);
            SetText(title, font, "WANTED", 130, InkRed, FontStyles.Bold);
            title.characterSpacing = 6;
            title.enableAutoSizing = true;       // 글자가 칸보다 길면 칸에 맞게 줄인다 (SetText 가 끄므로 그 뒤에 켠다)
            title.fontSizeMin = 60;
            title.fontSizeMax = 130;

            Image ruleTop = GetImage(panel, "RuleTop");
            PlaceLine(ruleTop, 205);

            //    가운데 줄 = 결투 결과. 결과를 담으므로 꾸밈 글자였을 때(30)보다 크게 쓴다
            //    Subtitle, Detail, Reward 의 글자는 편집할 때 보이는 예시다. Play 하면 ResultBoard 가 결과로 바꾼다
            TextMeshProUGUI subtitle = GetText(panel, "Subtitle");
            PlaceFromTop(subtitle.rectTransform, 213, 520, 70);
            SetText(subtitle, font, "•  •  •   WIN   •  •  •", 48, AccentRed, FontStyles.Bold);
            subtitle.characterSpacing = 8;
            subtitle.enableAutoSizing = true;    // COWARD 처럼 긴 단어는 칸에 맞게 줄인다
            subtitle.fontSizeMin = 30;
            subtitle.fontSizeMax = 48;

            Image ruleMiddle = GetImage(panel, "RuleMiddle");
            PlaceLine(ruleMiddle, 291);

            //    예시는 승리의 점수 내역 (ResultBoard.ShowPlayerWin 과 같은 모양)
            TextMeshProUGUI detail = GetText(panel, "Detail");
            PlaceFromTop(detail.rectTransform, 315, 500, 295);
            SetText(detail, font,
                "<align=left><indent=6%>" +
                "기본<pos=70%>+1,000\n" +
                "반응속도 0.82초<pos=70%>+680\n" +
                "머리 명중<pos=70%>+500\n" +
                "2라운드<pos=70%>+400",
                40, InkBrown, FontStyles.Normal);
            detail.lineSpacing = 30;
            detail.enableAutoSizing = true;      // 줄이 길거나 많으면(승리는 4줄) 칸에 맞게 줄인다
            detail.fontSizeMin = 28;
            detail.fontSizeMax = 40;

            Image ruleBottom = GetImage(panel, "RuleBottom");
            PlaceLine(ruleBottom, 620);

            TextMeshProUGUI reward = GetText(panel, "Reward");
            PlaceFromTop(reward.rectTransform, 632, 520, 100);
            SetText(reward, font, "REWARD $ 2,580", 64, InkRed, FontStyles.Bold);
            reward.characterSpacing = 4;

            //    아래 버튼 둘을 나란히 : 왼쪽 메인으로, 오른쪽 다시 결투 (메인화면의 뒤로 · 결투 시작과 같은 줄 모양)
            Button mainMenuButton = GetButton(panel, "MainMenuButton");
            PlaceFromTopAt((RectTransform)mainMenuButton.transform, -145, 810, 270, 80);
            ArrangeButton(mainMenuButton, font, "메인으로", 40);

            Button retryButton = GetButton(panel, "RetryButton");
            PlaceFromTopAt((RectTransform)retryButton.transform, 145, 810, 270, 80);
            ArrangeButton(retryButton, font, "다시 결투", 40);

            //    그리는 순서 : Hierarchy 에서 위에 있을수록 먼저 그린다 (= 뒤에 깔린다)
            //    종이를 맨 먼저 그려야 글자와 줄이 종이 위에 보인다
            background.transform.SetSiblingIndex(0);
            title.transform.SetSiblingIndex(1);
            ruleTop.transform.SetSiblingIndex(2);
            subtitle.transform.SetSiblingIndex(3);
            ruleMiddle.transform.SetSiblingIndex(4);
            detail.transform.SetSiblingIndex(5);
            ruleBottom.transform.SetSiblingIndex(6);
            reward.transform.SetSiblingIndex(7);
            retryButton.transform.SetSiblingIndex(8);
            mainMenuButton.transform.SetSiblingIndex(9);

            // ⑤ ResultBoard 의 칸에 연결한다 (Inspector 칸에 끌어다 넣는 일을 코드로 한다)
            //    칸이 private 이라 board.resultText = subtitle 로는 못 넣는다.
            //    SerializedObject 는 "Inspector 가 보는 그 칸들" 을 이름으로 꺼내 주는 창구다.
            SerializedObject boardData = new SerializedObject(board);
            SetField(boardData, "panel", panel.gameObject);
            SetField(boardData, "resultText", subtitle);
            SetField(boardData, "detailText", detail);
            SetField(boardData, "rewardText", reward);
            SetField(boardData, "retryButton", retryButton);
            SetField(boardData, "mainMenuButton", mainMenuButton);
            boardData.ApplyModifiedProperties();

            // 씬이 바뀌었다고 표시한다 (씬 이름 옆에 * 가 붙고, Ctrl+S 로 저장할 수 있다)
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            Debug.Log("[WantedPosterLayout] 결과판을 수배지 모양으로 정렬했습니다. Ctrl+S 로 씬을 저장해 주세요.");
        }

        // ---------------------------------------------------------------- 라운드 쪽지 (RoundBoard)

        [MenuItem("CONTEXT/RoundBoard/쪽지 모양으로 정렬")]
        private static void ArrangeNoteFromContext(MenuCommand command)
        {
            ArrangeNote((RoundBoard)command.context);
        }

        // 결과판(Arrange)과 같은 순서다 : 재료 → Panel → 자식 → 그리는 순서 → 칸 연결
        private static void ArrangeNote(RoundBoard board)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("[WantedPosterLayout] Play 를 멈춘 뒤에 눌러 주세요. Play 중에 바꾼 것은 멈추면 사라집니다.");

            Undo.RegisterFullObjectHierarchyUndo(board.gameObject, UndoName);

            // ① 재료
            TMP_FontAsset font = LoadFont();
            Sprite paper = LoadOrCreatePaper();

            // ② Panel (캔버스). 없으면 만든다
            RectTransform panel = GetOrCreateCanvas(board.transform, "Panel");
            panel.localPosition = new Vector3(0f, -0.35f, 0f);         // 눈높이보다 35cm 아래 (m) → 적을 가리지 않는다
            panel.localRotation = Quaternion.identity;
            panel.localScale = new Vector3(0.001f, 0.001f, 0.001f);    // 캔버스 1칸 = 1mm
            panel.sizeDelta = new Vector2(NoteWidth, NoteHeight);

            // ③④ 자식들
            Image background = GetImage(panel, "Background");
            PlaceFromTop(background.rectTransform, 0, NoteWidth, NoteHeight);
            background.sprite = paper;           // 세로로 눌려서 들어간다 (790 → 300)
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.raycastTarget = false;    // 누를 것이 없다. 텔레포트 레이도 막지 않게

            TextMeshProUGUI title = GetText(panel, "Title");
            PlaceFromTop(title.rectTransform, 28, 540, 70);
            SetText(title, font, "1라운드 · 무승부", 56, InkRed, FontStyles.Bold);

            //    Detail 의 글자는 편집할 때 보이는 예시다. Play 하면 RoundBoard 가 결과로 바꾼다
            TextMeshProUGUI detail = GetText(panel, "Detail");
            PlaceFromTop(detail.rectTransform, 105, 540, 170);
            SetText(detail, font, "나  빗나감 0.95초\n적  빗나감 0.87초\n<color=#A32E22>총을 꽂고 앞의 표식으로 이동</color>", 34, InkBrown, FontStyles.Normal);
            detail.lineSpacing = 10;
            detail.enableAutoSizing = true;      // 줄이 많으면(무승부는 3줄) 칸에 맞게 줄인다
            detail.fontSizeMin = 24;
            detail.fontSizeMax = 34;

            background.transform.SetSiblingIndex(0);   // 종이가 맨 뒤
            title.transform.SetSiblingIndex(1);
            detail.transform.SetSiblingIndex(2);

            // ⑤ RoundBoard 의 칸에 연결한다
            SerializedObject boardData = new SerializedObject(board);
            SetField(boardData, "panel", panel.gameObject);
            SetField(boardData, "titleText", title);
            SetField(boardData, "detailText", detail);
            boardData.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            Debug.Log("[WantedPosterLayout] 라운드 쪽지를 정렬했습니다. Ctrl+S 로 씬을 저장해 주세요.");
        }

        // parent 바로 아래에서 name 인 캔버스를 찾는다. 없으면 World Space 캔버스를 만든다
        //   (쪽지는 버튼이 없어서 Canvas 만 있으면 된다. 레이로 누를 때 필요한 Raycaster 는 붙이지 않는다)
        private static RectTransform GetOrCreateCanvas(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null)
                return (RectTransform)found;

            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            Undo.RegisterCreatedObjectUndo(canvasObject, UndoName);
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.transform.SetParent(parent, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;   // 화면이 아니라 세상 속에 놓인 판
            return (RectTransform)canvasObject.transform;
        }

        // ---------------------------------------------------------------- 메인화면 판 (MainMenu)

        [MenuItem("CONTEXT/MainMenu/메인화면 판 정렬")]
        private static void ArrangeMenuFromContext(MenuCommand command)
        {
            ArrangeMenu((MainMenu)command.context);
        }

        // 결과판(Arrange)과 같은 순서다 : 재료 → Panel → 자식 → 칸 연결
        //   다른 점 : 판 위의 것을 화면 다섯 장(첫 화면 / 게임 시작 / 홀스터 조정 / 주 손 고르기 / 손 따라가기)으로 나눠 담는다
        private static void ArrangeMenu(MainMenu menu)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("[WantedPosterLayout] Play 를 멈춘 뒤에 눌러 주세요. Play 중에 바꾼 것은 멈추면 사라집니다.");

            Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, UndoName);

            // ① 재료
            TMP_FontAsset font = LoadFont();
            Sprite paper = LoadOrCreatePaper();

            // ② Panel (캔버스). 버튼을 레이로 눌러야 해서 결과판처럼 XR > UI Canvas 로 직접 만들게 한다
            Transform panelTransform = menu.transform.Find("Panel");
            if (panelTransform == null)
                throw new Exception("[WantedPosterLayout] MainMenu 아래에 Panel 이 없습니다. MainMenu 우클릭 > XR > UI Canvas 로 만들고 이름을 Panel 로 바꿔 주세요.");

            //    MainMenu 오브젝트 = 플레이어가 설 바닥 자리. 판은 그 앞 1.5m, 높이 1.4m 에 띄운다
            RectTransform panel = (RectTransform)panelTransform;
            panel.localPosition = new Vector3(0f, MenuCenterHeight, MenuDistance);
            panel.localRotation = Quaternion.identity;                 // 플레이어와 같은 쪽을 본다 → 플레이어에게 앞면이 보인다
            panel.localScale = new Vector3(0.001f, 0.001f, 0.001f);    // 캔버스 1칸 = 1mm
            panel.sizeDelta = new Vector2(MenuWidth, MenuHeight);

            //    판 위의 것은 이 도구가 늘 처음부터 다시 만든다 → 예전에 만든 자식은 모두 지운다
            //    (화면 구성이 바뀌어도 옛 버튼이 남지 않는다. 지운 것도 Ctrl+Z 하면 되살아난다)
            //    지우면 칸 번호가 당겨지므로 뒤에서부터 지운다 (HolsterSlot.PutBack 의 for 와 같은 이유)
            for (int i = panel.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(panel.GetChild(i).gameObject);

            // ③ 모든 화면이 같이 쓰는 것 : 종이 · 제목 · 윗줄
            //    종이를 맨 먼저 만들어야 맨 뒤에 깔린다 (Hierarchy 에서 위에 있을수록 먼저 그린다)
            Image background = GetImage(panel, "Background");
            PlaceFromTop(background.rectTransform, 0, MenuWidth, MenuPaperHeight);
            background.sprite = paper;           // 조금 늘어나서 들어간다 (600 × 790 → 700 × 710)
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.raycastTarget = true;     // 레이가 종이에 닿아 멈추게 (어디를 가리키는지 보인다)

            TextMeshProUGUI title = GetText(panel, "Title");
            PlaceFromTop(title.rectTransform, 45, 600, 110);
            SetText(title, font, "정오의 결투", 96, InkRed, FontStyles.Bold);
            title.characterSpacing = 6;

            Image ruleTop = GetImage(panel, "RuleTop");
            PlaceLine(ruleTop, 170);

            // ④ 화면 다섯 장. 화면마다 빈 오브젝트 하나에 모아 두고, MainMenu 가 한 장씩 켜고 끈다
            //    화면은 Panel 을 꽉 채우므로, 화면 안의 자리는 Panel 에 놓을 때와 같은 숫자를 쓴다
            //    아래 버튼(뒤로 · 결투 시작)은 결과판의 '다시 결투' 처럼 종이 아래에 매단다

            //    [첫 화면] 큰 버튼 둘
            RectTransform homePage = CreatePage(panel, "HomePage");

            Button duelMenuButton = GetButton(homePage, "DuelMenuButton");
            PlaceFromTop((RectTransform)duelMenuButton.transform, 300, 400, 110);
            ArrangeButton(duelMenuButton, font, "게임 시작", 52);

            Button holsterMenuButton = GetButton(homePage, "HolsterMenuButton");
            PlaceFromTop((RectTransform)holsterMenuButton.transform, 450, 400, 110);
            ArrangeButton(holsterMenuButton, font, "홀스터 조정", 52);

            //    [게임 시작 화면] 난이도 셋을 세로로 + 뒤로 · 결투 시작
            RectTransform duelPage = CreatePage(panel, "DuelPage");

            TextMeshProUGUI difficultyLabel = GetText(duelPage, "DifficultyLabel");
            PlaceFromTop(difficultyLabel.rectTransform, 190, 600, 50);
            SetText(difficultyLabel, font, "난이도", 38, InkBrown, FontStyles.Bold);

            //    난이도 : Toggle 셋을 한 묶음(Toggle Group)에 넣는다 → 하나를 켜면 나머지가 꺼진다
            GameObject groupObject = GetOrCreateChild(duelPage, "DifficultyGroup");
            RectTransform groupRect = (RectTransform)groupObject.transform;
            PlaceFromTop(groupRect, 255, 600, 310);
            ToggleGroup group = Undo.AddComponent<ToggleGroup>(groupObject);   // 방금 만든 빈 오브젝트라 붙어 있는 것이 없다
            group.allowSwitchOff = false;        // 셋 다 꺼질 수 없게 (늘 하나는 골라져 있다)

            //    Toggle 의 자리는 DifficultyGroup 의 위쪽 가운데 기준이다
            Toggle easyToggle = GetToggle(groupRect, "EasyToggle");
            PlaceFromTop((RectTransform)easyToggle.transform, 0, 360, 90);
            ArrangeToggle(easyToggle, group, font, "쉬움");

            Toggle normalToggle = GetToggle(groupRect, "NormalToggle");
            PlaceFromTop((RectTransform)normalToggle.transform, 110, 360, 90);
            ArrangeToggle(normalToggle, group, font, "보통");

            Toggle hardToggle = GetToggle(groupRect, "HardToggle");
            PlaceFromTop((RectTransform)hardToggle.transform, 220, 360, 90);
            ArrangeToggle(hardToggle, group, font, "어려움");

            //    편집할 때는 보통이 골라져 있게 한다 (Play 하면 MainMenu 가 지금 난이도로 다시 고른다)
            //    보통을 먼저 켜야 한다. 하나도 안 켜진 채로 끄려고 하면 Toggle Group 이 못 끄게 막는다
            normalToggle.isOn = true;
            easyToggle.isOn = false;
            hardToggle.isOn = false;

            Button duelBackButton = GetButton(duelPage, "DuelBackButton");
            PlaceFromTopAt((RectTransform)duelBackButton.transform, -150, 725, 280, 90);
            ArrangeButton(duelBackButton, font, "뒤로", 44);

            Button startButton = GetButton(duelPage, "StartButton");
            PlaceFromTopAt((RectTransform)startButton.transform, 150, 725, 280, 90);
            ArrangeButton(startButton, font, "결투 시작", 44);

            //    [홀스터 조정 화면] 지금 자리 글자 + 방향 버튼 세 줄 + 처음 자리 · 빠른 조정 + 뒤로
            //    방향 버튼 이름은 '~쪽' 으로 맞췄다. '뒤로' 는 화면을 돌아가는 버튼이라 헷갈리지 않게
            //    좌우는 '왼쪽 · 오른쪽' 이다. 왼손잡이는 홀스터가 왼쪽 허리라서 '안쪽 · 바깥쪽' 이 손마다 반대가 된다
            RectTransform holsterPage = CreatePage(panel, "HolsterPage");

            TextMeshProUGUI holsterLabel = GetText(holsterPage, "HolsterLabel");
            PlaceFromTop(holsterLabel.rectTransform, 190, 600, 50);
            SetText(holsterLabel, font, "홀스터 위치", 38, InkBrown, FontStyles.Bold);

            //    글자는 편집할 때 보이는 예시다. Play 하면 MainMenu 가 저장된 자리로 바꾼다
            TextMeshProUGUI holsterText = GetText(holsterPage, "HolsterText");
            PlaceFromTop(holsterText.rectTransform, 245, 600, 40);
            SetText(holsterText, font, "머리에서 아래 60cm · 오른쪽 20cm · 앞 6cm", 28, InkBrown, FontStyles.Normal);
            holsterText.enableAutoSizing = true;   // 숫자가 길어지면 칸에 맞게 줄인다 (SetText 가 끄므로 그 뒤에 켠다)
            holsterText.fontSizeMin = 20;
            holsterText.fontSizeMax = 28;

            PlaceRowLabel(holsterPage, font, "HeightLabel", "높이", 300);
            Button upButton = PlaceRowButton(holsterPage, font, "UpButton", "위쪽", RowLeftX, 300);
            Button downButton = PlaceRowButton(holsterPage, font, "DownButton", "아래쪽", RowRightX, 300);

            PlaceRowLabel(holsterPage, font, "DepthLabel", "앞뒤", 376);
            Button forwardButton = PlaceRowButton(holsterPage, font, "ForwardButton", "앞쪽", RowLeftX, 376);
            Button backwardButton = PlaceRowButton(holsterPage, font, "BackwardButton", "뒤쪽", RowRightX, 376);

            PlaceRowLabel(holsterPage, font, "SideLabel", "좌우", 452);
            Button leftButton = PlaceRowButton(holsterPage, font, "LeftButton", "왼쪽", RowLeftX, 452);
            Button rightButton = PlaceRowButton(holsterPage, font, "RightButton", "오른쪽", RowRightX, 452);

            Button resetButton = GetButton(holsterPage, "ResetButton");
            PlaceFromTopAt((RectTransform)resetButton.transform, -130, 532, 240, 60);
            ArrangeButton(resetButton, font, "처음 자리", 32);

            Button quickButton = GetButton(holsterPage, "QuickButton");
            PlaceFromTopAt((RectTransform)quickButton.transform, 130, 532, 240, 60);
            ArrangeButton(quickButton, font, "빠른 조정", 32);

            TextMeshProUGUI holsterHint = GetText(holsterPage, "HolsterHint");
            PlaceFromTop(holsterHint.rectTransform, 610, 600, 36);
            SetText(holsterHint, font, "버튼을 누르면 총이 홀스터로 돌아옵니다. 뽑아 보며 맞추세요", 24, InkBrown, FontStyles.Normal);

            Button holsterBackButton = GetButton(holsterPage, "HolsterBackButton");
            PlaceFromTop((RectTransform)holsterBackButton.transform, 725, 280, 90);
            ArrangeButton(holsterBackButton, font, "뒤로", 44);

            //    [주 손 고르기 화면] 빠른 조정 1단계
            RectTransform handPage = CreatePage(panel, "HandPage");

            TextMeshProUGUI handLabel = GetText(handPage, "HandLabel");
            PlaceFromTop(handLabel.rectTransform, 190, 600, 50);
            SetText(handLabel, font, "빠른 조정", 38, InkBrown, FontStyles.Bold);

            TextMeshProUGUI handText = GetText(handPage, "HandText");
            PlaceFromTop(handText.rectTransform, 260, 600, 90);
            SetText(handText, font, "주로 쓰는 손을 고르세요\n홀스터가 그 손을 따라갑니다", 30, InkBrown, FontStyles.Normal);
            handText.lineSpacing = 10;

            Button leftHandButton = GetButton(handPage, "LeftHandButton");
            PlaceFromTopAt((RectTransform)leftHandButton.transform, -150, 390, 260, 110);
            ArrangeButton(leftHandButton, font, "왼손잡이", 44);

            Button rightHandButton = GetButton(handPage, "RightHandButton");
            PlaceFromTopAt((RectTransform)rightHandButton.transform, 150, 390, 260, 110);
            ArrangeButton(rightHandButton, font, "오른손잡이", 44);

            Button handBackButton = GetButton(handPage, "HandBackButton");
            PlaceFromTop((RectTransform)handBackButton.transform, 725, 280, 90);
            ArrangeButton(handBackButton, font, "뒤로", 44);

            //    [손 따라가기 화면] 빠른 조정 2단계. 버튼 없이 안내 글자만 둔다
            //    여기에 버튼(취소 등)을 두면, 주 손으로 그 버튼을 누르려는 트리거가 확정이 되어 버린다
            RectTransform followPage = CreatePage(panel, "FollowPage");

            TextMeshProUGUI followLabel = GetText(followPage, "FollowLabel");
            PlaceFromTop(followLabel.rectTransform, 190, 600, 50);
            SetText(followLabel, font, "빠른 조정", 38, InkBrown, FontStyles.Bold);

            //    글자는 편집할 때 보이는 예시다. Play 하면 MainMenu 가 고른 손에 맞게 바꾼다
            TextMeshProUGUI followText = GetText(followPage, "FollowText");
            PlaceFromTop(followText.rectTransform, 280, 600, 160);
            SetText(followText, font, "오른손을 홀스터를 둘 자리에 대고\n오른손 트리거를 누르세요", 40, InkRed, FontStyles.Bold);
            followText.lineSpacing = 20;
            followText.enableAutoSizing = true;   // 한 줄이 칸보다 길면 줄인다 (SetText 가 끄므로 그 뒤에 켠다)
            followText.fontSizeMin = 28;
            followText.fontSizeMax = 40;

            TextMeshProUGUI followHint = GetText(followPage, "FollowHint");
            PlaceFromTop(followHint.rectTransform, 480, 600, 80);
            SetText(followHint, font, "홀스터가 손을 따라옵니다\n확정한 뒤 버튼으로 더 다듬을 수 있습니다", 26, InkBrown, FontStyles.Normal);
            followHint.lineSpacing = 10;

            //    편집할 때는 첫 화면만 보이게 한다 (Play 하면 MainMenu 가 어차피 첫 화면부터 연다)
            //    다른 화면을 보고 싶으면 Hierarchy 에서 그 화면을 잠깐 켜서 본다
            //    난이도 Toggle 을 고른 "뒤에" 끈다. 꺼진 화면에서는 Toggle Group 이 나머지를 못 끈다
            duelPage.gameObject.SetActive(false);
            holsterPage.gameObject.SetActive(false);
            handPage.gameObject.SetActive(false);
            followPage.gameObject.SetActive(false);

            // ⑤ MainMenu 의 칸에 연결한다
            //    PlayerPlacer · HolsterAnchor 는 씬에 하나씩만 있어서 찾아서 넣는다 (Town 을 복제한 씬이면 XR Origin 에 붙어 있다)
            PlayerPlacer placer = Object.FindFirstObjectByType<PlayerPlacer>();
            if (placer == null)
                throw new Exception("[WantedPosterLayout] 씬에 PlayerPlacer 가 없습니다. Town 씬을 복제해서 XR Origin 을 그대로 써 주세요.");

            HolsterAnchor anchor = Object.FindFirstObjectByType<HolsterAnchor>();
            if (anchor == null)
                throw new Exception("[WantedPosterLayout] 씬에 HolsterAnchor 가 없습니다. Town 씬을 복제해서 XR Origin 을 그대로 써 주세요.");

            //    컨트롤러 : XR Origin → Camera Offset 아래의 Left Controller · Right Controller
            XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null)
                throw new Exception("[WantedPosterLayout] 씬에 XR Origin 이 없습니다. Town 씬을 복제해서 XR Origin 을 그대로 써 주세요.");

            Transform cameraOffset = origin.CameraFloorOffsetObject.transform;
            Transform leftController = FindController(cameraOffset, "Left Controller");
            Transform rightController = FindController(cameraOffset, "Right Controller");

            //    트리거 : XRI 입력 파일의 Activate (트리거 버튼에 묶여 있다. 잡은 물건을 '쓰는' 입력)
            InputActionReference leftTrigger = LoadInputAction("XRI Left Interaction", "Activate");
            InputActionReference rightTrigger = LoadInputAction("XRI Right Interaction", "Activate");

            SerializedObject menuData = new SerializedObject(menu);
            SetField(menuData, "playerPlacer", placer);
            SetField(menuData, "holsterAnchor", anchor);

            SetField(menuData, "leftController", leftController);
            SetField(menuData, "rightController", rightController);
            SetField(menuData, "leftTrigger", leftTrigger);
            SetField(menuData, "rightTrigger", rightTrigger);

            SetField(menuData, "homePage", homePage.gameObject);
            SetField(menuData, "duelMenuButton", duelMenuButton);
            SetField(menuData, "holsterMenuButton", holsterMenuButton);

            SetField(menuData, "duelPage", duelPage.gameObject);
            SetField(menuData, "easyToggle", easyToggle);
            SetField(menuData, "normalToggle", normalToggle);
            SetField(menuData, "hardToggle", hardToggle);
            SetField(menuData, "startButton", startButton);
            SetField(menuData, "duelBackButton", duelBackButton);

            SetField(menuData, "holsterPage", holsterPage.gameObject);
            SetField(menuData, "holsterText", holsterText);
            SetField(menuData, "upButton", upButton);
            SetField(menuData, "downButton", downButton);
            SetField(menuData, "forwardButton", forwardButton);
            SetField(menuData, "backwardButton", backwardButton);
            SetField(menuData, "leftButton", leftButton);
            SetField(menuData, "rightButton", rightButton);
            SetField(menuData, "resetButton", resetButton);
            SetField(menuData, "quickButton", quickButton);
            SetField(menuData, "holsterBackButton", holsterBackButton);

            SetField(menuData, "handPage", handPage.gameObject);
            SetField(menuData, "leftHandButton", leftHandButton);
            SetField(menuData, "rightHandButton", rightHandButton);
            SetField(menuData, "handBackButton", handBackButton);
            SetField(menuData, "followPage", followPage.gameObject);
            SetField(menuData, "followText", followText);
            menuData.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            Debug.Log("[WantedPosterLayout] 메인화면 판을 정렬했습니다. Ctrl+S 로 씬을 저장해 주세요.");
        }

        // cameraOffset 바로 아래에서 name 인 컨트롤러를 찾는다. 없으면 바로 멈춘다
        private static Transform FindController(Transform cameraOffset, string name)
        {
            Transform controller = cameraOffset.Find(name);
            if (controller == null)
                throw new Exception($"[WantedPosterLayout] {cameraOffset.name} 아래에 {name} 이 없습니다. XR Origin 의 컨트롤러 이름을 바꿨으면 여기도 고쳐 주세요.");
            return controller;
        }

        // XRI 입력 파일 안에서 mapName/actionName 입력의 참조(InputActionReference)를 꺼낸다
        //   .inputactions 파일 안에는 입력마다 "참조" 가 하나씩 들어 있다 (Project 창에서 파일의 ▶ 를 펼치면 보이는 것들)
        //   Inspector 의 InputActionReference 칸에 끌어다 넣는 것이 바로 이 참조다
        private static InputActionReference LoadInputAction(string mapName, string actionName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath);
            if (assets.Length == 0)
                throw new Exception($"[WantedPosterLayout] XRI 입력 파일을 찾지 못했습니다 → {InputActionsPath}");

            foreach (Object asset in assets)
            {
                // as : 그 종류(InputActionReference)면 꺼내고, 아니면 null. 파일 자체 같은 다른 것은 건너뛴다
                InputActionReference reference = asset as InputActionReference;
                if (reference == null) continue;

                if (reference.action.actionMap.name == mapName && reference.action.name == actionName)
                    return reference;
            }

            throw new Exception($"[WantedPosterLayout] {InputActionsPath} 안에 {mapName}/{actionName} 입력이 없습니다.");
        }

        // 화면 한 장 = Panel 을 꽉 채우는 빈 UI 오브젝트. 그 화면의 버튼 · 글자를 자식으로 모은다
        private static RectTransform CreatePage(RectTransform panel, string name)
        {
            RectTransform page = (RectTransform)GetOrCreateChild(panel, name).transform;
            Stretch(page);
            return page;
        }

        // 홀스터 조정 줄의 왼쪽 이름표 (높이 / 앞뒤 / 좌우)
        private static void PlaceRowLabel(RectTransform parent, TMP_FontAsset font, string name, string content, float top)
        {
            TextMeshProUGUI label = GetText(parent, name);
            PlaceFromTopAt(label.rectTransform, RowLabelX, top, 130, RowHeight);
            SetText(label, font, content, 32, InkBrown, FontStyles.Bold);
        }

        // 홀스터 조정 버튼 (작은 나무 간판)
        private static Button PlaceRowButton(RectTransform parent, TMP_FontAsset font, string name, string content, float x, float top)
        {
            Button button = GetButton(parent, name);
            PlaceFromTopAt((RectTransform)button.transform, x, top, 190, RowHeight);
            ArrangeButton(button, font, content, 32);
            return button;
        }

        // ---------------------------------------------------------------- 자식 찾기 / 만들기

        // parent 바로 아래에서 name 인 자식을 찾는다. 없으면 빈 UI 오브젝트를 만든다
        private static GameObject GetOrCreateChild(RectTransform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null)
                return found.gameObject;

            GameObject child = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(child, UndoName);   // Ctrl+Z 하면 만든 것도 지운다
            child.layer = parent.gameObject.layer;             // UI 는 부모와 같은 레이어
            child.transform.SetParent(parent, false);          // false : 부모 안에서의 자리 · 크기를 그대로 쓴다
            return child;
        }

        // name 인 자식의 Image. 없으면 붙인다
        private static Image GetImage(RectTransform parent, string name)
        {
            GameObject child = GetOrCreateChild(parent, name);
            Image image = child.GetComponent<Image>();
            if (image == null)
                image = Undo.AddComponent<Image>(child);
            return image;
        }

        // name 인 자식의 글자 (TextMeshPro - Text (UI)). 없으면 붙인다
        private static TextMeshProUGUI GetText(RectTransform parent, string name)
        {
            GameObject child = GetOrCreateChild(parent, name);
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text == null)
                text = Undo.AddComponent<TextMeshProUGUI>(child);
            return text;
        }

        // name 인 자식의 Button. 없으면 바탕 Image 와 Button 을 붙인다
        private static Button GetButton(RectTransform parent, string name)
        {
            GameObject child = GetOrCreateChild(parent, name);

            Image image = child.GetComponent<Image>();
            if (image == null)
            {
                image = Undo.AddComponent<Image>(child);
                // UI > Button 으로 만들 때와 같은 유니티 기본 둥근 사각형
                image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                image.type = Image.Type.Sliced;
            }

            Button button = child.GetComponent<Button>();
            if (button == null)
                button = Undo.AddComponent<Button>(child);

            button.targetGraphic = image;   // 상태(가리킴 · 누름)에 따라 색이 바뀌는 그림
            return button;
        }

        // name 인 자식의 Toggle. 없으면 바탕 Image 와 Toggle 을 붙인다 (GetButton 과 같은 모양)
        //   켜짐 표시(Checked) 와 글자(Label) 는 ArrangeToggle 이 만든다
        private static Toggle GetToggle(RectTransform parent, string name)
        {
            GameObject child = GetOrCreateChild(parent, name);

            Image image = child.GetComponent<Image>();
            if (image == null)
            {
                image = Undo.AddComponent<Image>(child);
                image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                image.type = Image.Type.Sliced;
            }

            Toggle toggle = child.GetComponent<Toggle>();
            if (toggle == null)
                toggle = Undo.AddComponent<Toggle>(child);

            toggle.targetGraphic = image;
            return toggle;
        }

        // ---------------------------------------------------------------- 자리 · 모양 정하기

        // Panel 위쪽 가운데를 기준으로 놓는다
        //   top    : Panel 맨 위에서 몇 칸 아래에 내 윗변이 오나
        //   width  : 가로 크기,  height : 세로 크기
        private static void PlaceFromTop(RectTransform rect, float top, float width, float height)
        {
            PlaceFromTopAt(rect, 0, top, width, height);   // 가로는 가운데
        }

        // PlaceFromTop 과 같은데, 가로 자리(x)도 정한다. 가운데가 0, 오른쪽이 + (한 줄에 버튼 여러 개를 놓을 때)
        private static void PlaceFromTopAt(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);   // 기준점 : 부모(Panel)의 위쪽 가운데
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);       // 내 위쪽 가운데를 기준점에 맞춘다
            rect.anchoredPosition3D = new Vector3(x, -top, 0f);   // UI 는 위쪽이 + 라서, 아래로 내리려면 - 를 붙인다
            rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        // 가로줄 : 짙은 갈색 얇은 막대
        private static void PlaceLine(Image line, float top)
        {
            PlaceFromTop(line.rectTransform, top, LineWidth, 4);
            line.sprite = null;            // 그림 없이 색만 칠한다
            line.color = InkBrown;
            line.raycastTarget = false;
        }

        // 글자의 공통 설정
        private static void SetText(TextMeshProUGUI text, TMP_FontAsset font, string content, float size, Color color, FontStyles style)
        {
            text.font = font;              // 한글 글꼴 (기본 글꼴 LiberationSans 에는 한글이 없다)
            text.text = content;
            text.fontSize = size;
            text.enableAutoSizing = false;    // 크기를 자동으로 줄이는 건 필요한 글자(Title)만 따로 켠다
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;          // 가로 · 세로 가운데
            text.textWrappingMode = TextWrappingModes.NoWrap;      // 줄은 \n 으로만 바꾼다
            text.characterSpacing = 0;
            text.lineSpacing = 0;
            text.raycastTarget = false;    // 글자가 레이를 막아서 버튼이 안 눌리는 일이 없게
        }

        // 나무 간판 버튼 (다시 결투 · 결투 시작 · 홀스터 조정) : 나무 간판 색 + 크림색 글자
        private static void ArrangeButton(Button button, TMP_FontAsset font, string content, float fontSize)
        {
            // 바탕 색은 Button 의 Color Tint 가 정한다. Image 색은 흰색(그대로)으로 둔다
            //   화면에 보이는 색 = Image 색 × 상태 색 이라서,
            //   Image 를 나무색으로 두면 상태 색으로 더 밝게 만들 수 없다 (곱하면 어두워지기만 한다)
            Image image = button.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;    // 버튼은 레이에 맞아야 눌린다
            SetWoodColors(button);

            // 버튼 글자. Button - TextMeshPro 로 만들었으면 자식 Text (TMP) 가 이미 있다
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                GameObject labelObject = GetOrCreateChild((RectTransform)button.transform, "Text (TMP)");
                label = Undo.AddComponent<TextMeshProUGUI>(labelObject);
            }

            Stretch(label.rectTransform);   // 글자 칸이 버튼을 꽉 채우게
            SetText(label, font, content, fontSize, ButtonTextColor, FontStyles.Bold);
        }

        // 난이도 Toggle : 평소엔 나무 간판, 골라지면 빨간 간판
        private static void ArrangeToggle(Toggle toggle, ToggleGroup group, TMP_FontAsset font, string content)
        {
            RectTransform toggleRect = (RectTransform)toggle.transform;

            // ① 바탕 = 나무 간판 (버튼과 같은 색, 같은 이유로 Image 는 흰색)
            Image background = (Image)toggle.targetGraphic;
            background.color = Color.white;
            background.raycastTarget = true;    // 레이에 맞아야 눌린다
            SetWoodColors(toggle);

            // ② 켜짐 표시 = 바탕을 꽉 덮는 빨간 판 (Toggle 의 Graphic 칸)
            //    Toggle 은 켜지면 Graphic 을 보이고, 꺼지면 숨긴다 → 고른 난이도만 빨갛게 보인다
            Image check = GetImage(toggleRect, "Checked");
            Stretch(check.rectTransform);
            check.sprite = background.sprite;   // 바탕과 같은 둥근 사각형
            check.type = Image.Type.Sliced;
            check.color = AccentRed;
            check.raycastTarget = false;        // 레이는 바탕이 받는다
            toggle.graphic = check;
            toggle.toggleTransition = Toggle.ToggleTransition.None;   // 서서히 말고 바로 바뀌게

            // ③ 글자. 빨간 판보다 뒤 순서여야(나중에 그려야) 판 위에 보인다
            TextMeshProUGUI label = GetText(toggleRect, "Label");
            Stretch(label.rectTransform);
            SetText(label, font, content, 36, ButtonTextColor, FontStyles.Bold);
            check.transform.SetSiblingIndex(0);
            label.transform.SetSiblingIndex(1);

            // ④ 같은 묶음에 넣는다 → 셋 중 하나만 켜진다
            toggle.group = group;
        }

        // 나무 간판 색 (평소 · 가리킴 · 누름). 버튼과 난이도 Toggle 이 같이 쓴다
        private static void SetWoodColors(Selectable selectable)
        {
            selectable.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = selectable.colors;
            colors.normalColor = WoodNormal;
            colors.highlightedColor = WoodHighlighted;
            colors.pressedColor = WoodPressed;
            colors.selectedColor = WoodNormal;   // 누른 뒤 "선택됨" 으로 밝은 채 남지 않게
            selectable.colors = colors;
        }

        // 부모를 꽉 채운다 (Anchor 를 stretch 로, 여백 0)
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        // 보드 스크립트(ResultBoard · RoundBoard · MainMenu)의 칸 하나에 넣는다. 칸 이름(필드 이름)이 틀리면 바로 멈춘다
        private static void SetField(SerializedObject data, string fieldName, Object value)
        {
            SerializedProperty field = data.FindProperty(fieldName);
            if (field == null)
                throw new Exception($"[WantedPosterLayout] {data.targetObject.GetType().Name} 에 {fieldName} 칸이 없습니다. 필드 이름이 바뀌었으면 여기도 고쳐 주세요.");

            field.objectReferenceValue = value;
        }

        // ---------------------------------------------------------------- 재료

        private static TMP_FontAsset LoadFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new Exception($"[WantedPosterLayout] 한글 글꼴을 찾지 못했습니다 → {FontPath}");
            return font;
        }

        // 종이 그림을 불러온다. 파일이 없으면 먼저 만든다
        private static Sprite LoadOrCreatePaper()
        {
            if (File.Exists(PaperPath) == false)
                CreatePaperPicture();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
            if (sprite == null)
                throw new Exception($"[WantedPosterLayout] {PaperPath} 를 Sprite 로 불러오지 못했습니다. 그림의 Texture Type 을 Sprite (2D and UI) 로 바꿔 주세요.");
            return sprite;
        }

        // 종이 그림을 만들어서 PNG 파일로 저장한다
        private static void CreatePaperPicture()
        {
            // ① 점을 담을 빈 그림 (가로 600 × 세로 790 점. 캔버스 1칸에 점 하나)
            int width = BoardWidth;
            int height = PaperHeight;
            Texture2D picture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // ② 점마다 색을 계산한다
            //    Color[] 는 한 줄로 늘어선 점 목록이다. (x, y) 점은 y * width + x 번째 칸
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = GetPaperPixel(x, y, width, height);
                }
            }
            picture.SetPixels(pixels);
            picture.Apply();

            // ③ PNG 파일로 저장한다
            if (AssetDatabase.IsValidFolder(PaperFolder) == false)
                AssetDatabase.CreateFolder("Assets", "30.UI");
            File.WriteAllBytes(PaperPath, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);   // 계산용 그림은 다 썼으니 지운다 (파일은 남는다)

            // ④ 유니티에게 새 파일을 알리고, UI 에 쓸 수 있게 Sprite 로 가져오게 한다
            AssetDatabase.ImportAsset(PaperPath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(PaperPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;   // 찢어진 가장자리(투명)의 테두리가 하얗게 번지지 않게
            importer.mipmapEnabled = true;         // 멀리서 볼 때 지글거리지 않게
            importer.SaveAndReimport();

            Debug.Log($"[WantedPosterLayout] 종이 그림을 만들었습니다 → {PaperPath}");
        }

        // 종이 그림의 점 하나 (x, y) 의 색
        //   Mathf.PerlinNoise(a, b) : 0 ~ 1 사이를 부드럽게 오르내리는 값. 구름 · 얼룩 무늬를 만들 때 쓴다
        //     곱하는 수가 작을수록 무늬가 크고(0.006 → 큰 얼룩), 클수록 잘다(0.05 → 잔 무늬)
        //     더하는 수(13.1, 7.3 ...)는 무늬마다 다른 곳을 쓰려고 옮긴 것이다
        private static Color GetPaperPixel(int x, int y, int width, int height)
        {
            // ① 얼룩 : 큰 무늬와 잔 무늬를 섞고, 0.45 보다 진한 곳만 얼룩색으로 물들인다
            float big = Mathf.PerlinNoise(x * 0.006f + 13.1f, y * 0.006f + 7.3f);
            float small = Mathf.PerlinNoise(x * 0.05f + 41.7f, y * 0.05f + 3.9f);
            float stain = Mathf.Clamp01((big * 0.75f + small * 0.25f - 0.45f) * 2f);
            Color color = Color.Lerp(PaperLight, PaperStain, stain);

            // ② 가장자리까지 거리 : 네 변(왼 · 오른 · 아래 · 위) 중 가장 가까운 변까지 몇 점
            float edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));

            // ③ 찢어진 가장자리 : 4 ~ 20점 사이로 들쭉날쭉한 선을 만들고, 그 바깥은 투명하게 뚫는다
            float tear = 4f + Mathf.PerlinNoise(x * 0.04f + 90.2f, y * 0.04f + 55.5f) * 16f;
            if (edge < tear)
                return Color.clear;

            // ④ 그을림 : 찢어진 선에 가까울수록 탄 색. 선에서 안쪽으로 50점 들어가면 원래 종이색
            //    burn 을 제곱하면 가장자리에서만 진하고 안쪽으로 빨리 옅어진다
            float burn = 1f - Mathf.Clamp01((edge - tear) / 50f);
            return Color.Lerp(color, PaperBurnt, burn * burn);
        }
    }
}
