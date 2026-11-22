using System;
using System.Linq;              // FirstOrDefault (일시 정지 판 : 씬의 다른 UISound 찾기)
using Game.Table;              // EnemyTable (몽타주를 찍을 적 프리팹 · 이름)
using TMPro;
using Unity.XR.CoreUtils;       // XROrigin (메인화면 판 : 컨트롤러 찾기)
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

        // XRI 입력 파일 (XRI Starter Assets 를 가져올 때 같이 온다). 메인화면 판의 트리거 입력을 여기서 꺼낸다
        private const string InputActionsPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/XRI Default Input Actions.inputactions";

        // 크기 (캔버스 칸. 1칸 = 1mm)
        private const int BoardWidth = 600;
        private const int PaperHeight = 980;   // 수배지 종이 (가로는 BoardWidth)
        private const int ResultBoardWidth = 720;    // 수배지를 박은 나무 판 (둘레 3D 틀은 이 바깥에 붙는다)
        private const int ResultBoardHeight = 1180;
        private const int PosterTopMargin = 40;      // 판 위 가장자리에서 수배지까지
        private const int ButtonTop = 1044;          // 수배지 아래, 판에 박은 버튼 줄 (40 + 980 + 24)
        private const int MontageSize = 320;   // 몽타주 칸 (가로 = 세로)
        private const float StampAngle = 11f;  // 도장이 기울어진 각도 (도. + 면 시계 반대 방향)
        private const int LineWidth = 480;     // 가로줄 길이
        private const int NoteWidth = 600;     // 라운드 쪽지 (탄피 카드)
        private const int NoteHeight = 300;
        private const int NoteBoardWidth = 680;        // 쪽지를 박은 나무 판 (둘레 3D 틀은 이 바깥에 붙는다)
        private const int NoteBoardHeight = 380;
        private const int NoteMargin = 40;             // 판 가장자리에서 쪽지까지
        private const int CylinderDisplaySize = 150;   // 쪽지 위 실린더 크기 (칸)
        private const int ChamberCount = 6;            // 리볼버 6발 = 최대 6라운드

        // 무승부 뒤 할 일을 보여 주는 것들 (m)
        private const float HolsterGlowSize = 0.2f;    // 홀스터 빛 구 지름
        private const float SignSideOffset = 0f;       // 전진 표식 가운데에서 팻말까지 (오른쪽 +)
        private const float SignForwardOffset = 0.5f;  // 전진 표식 가운데에서 팻말까지 (앞 = 적 쪽 +). 씬에서 표식 바로 앞으로 옮겼다
        private const float SignPostHeight = 1.2f;     // 말뚝 높이
        private const float SignBoardY = 1.05f;        // 팻말 판 가운데 높이
        private const float SignBoardWidth = 0.5f;
        private const float SignBoardHeight = 0.2f;
        private const int MenuWidth = 1000;        // 메인화면 판 = 나무 현상금 게시판
        private const int MenuHeight = 760;
        private const int PosterWidth = 200;       // 게시판에 붙은 작은 수배지
        private const int PosterHeight = 290;
        private const int NoteButtonWidth = 230;   // 누르는 쪽지
        private const int NoteButtonHeight = 100;
        private const int SheetWidth = 700;        // 홀스터 조정처럼 내용이 많은 화면을 담는 큰 종이 (메인화면 : 오른쪽에 쪽지 줄이 있어서 왼쪽에 붙인다)
        private const int SheetHeight = 520;
        private const int SheetX = -110;
        private const int SheetTop = 205;

        // 큰 종이 자리 · 크기. 종이 안의 내용(홀스터 조정 · 음량 조절)은 이 폭 · 높이에 맞춰 스스로 배치된다
        private struct SheetBox
        {
            public float X, Top, Width, Height;
            public SheetBox(float x, float top, float width, float height) { X = x; Top = top; Width = width; Height = height; }
        }

        private static readonly SheetBox MainSheet = new SheetBox(SheetX, SheetTop, SheetWidth, SheetHeight);
        private static readonly SheetBox PauseSheet = new SheetBox(0, 160, 860, 470);   // 일시 정지 판 : 쪽지 줄이 없어서 가운데 넓게

        // 일시 정지 판 자리
        private const int PauseTitleTop = 36;      // 간판 윗변 (메인화면보다 조금 올려 종이 자리를 낸다)
        private const int PauseBackTop = 650;      // 하위 화면 '뒤로' 쪽지 윗변 (종이 아래 가운데. 종이 끝 630)

        // 글자 크기 단계. 판 · 종이 · 버튼 어디서나 이 다섯 단계만 쓴다
        private const float TextTitle = 64f;       // 나무 간판
        private const float TextHeading = 40f;     // 큰 종이 제목 (홀스터 위치 · 음량)
        private const float TextButton = 34f;      // 버튼 글자 · 줄 이름표 · 음량 숫자
        private const float TextBody = 30f;        // 종이 본문 (홀스터 자리 글자 등)
        private const float TextHint = 24f;        // 종이 맨 아래 안내
        private const int RightColumnX = 350;      // 게시판 오른쪽 쪽지 줄의 가로 자리
        private const int PauseButtonTop = 170;    // 일시 정지 첫 화면 : 간판 아래 첫 쪽지 버튼 윗변 (간판 36 ~ 140)
        private const int PauseButtonGap = 130;    // 쪽지 버튼 사이 (버튼 110 + 틈 20) → 마지막 버튼 아래 끝 670
        private const int PauseButtonWidth = 360;  // 판에 버튼만 있어서 크게
        private const int PauseButtonHeight = 110;

        // 게시판 몸통 (3D 큐브, 캔버스 칸 = mm). Z 는 + 가 뒤(플레이어 반대쪽)
        private const float FrameWidth = 50f;      // 틀 막대 폭
        private const float FrameDepth = 70f;      // 틀 막대 두께
        private const float FrameBackZ = 42f;      // 틀 뒷면 자리 → 앞면은 42 - 70 = -28 (판자 그림보다 2.8cm 앞으로 튀어나온다)
        private const float BackFrontZ = 2f;       // 뒤판 앞면 자리 (판자 그림 2mm 뒤 → 그림과 겹쳐 깜빡이지 않게)
        private const float BackDepth = 44f;       // 뒤판 두께 → 뒷면 46 (틀 뒷면 42 를 덮는다)

        // 수배지 셋 (쉬움 · 보통 · 어려움) 자리 · 기울기. 손으로 붙인 것처럼 높이 · 각도를 조금씩 다르게
        private static readonly float[] PosterX = { -330f, -110f, 110f };
        private static readonly float[] PosterTop = { 205f, 215f, 200f };
        private static readonly float[] PosterAngle = { -3f, 1.5f, -2f };
        //    수배지 못 자리 (수배지 위쪽 가운데 기준 x, 위에서 아래로 top). 씬에서 손으로 박은 자리
        private static readonly Vector2[] PosterPin = { new Vector2(2.3f, 11.5f), new Vector2(0.3f, 11.1f), new Vector2(2.2f, 14.1f) };

        // 공고 (게시판 아래쪽 종이 한 장). 씬에서 손으로 맞춘 자리 · 크기
        private const float NoticeX = 5f;
        private const float NoticeTop = 543f;
        private const int NoticeWidth = 750;
        private const int NoticeHeight = 200;

        private const int RowHeight = 64;         // 홀스터 조정 줄 (이름표 | 버튼 | 버튼)
        private const float BeltWidthBase = 300f;  // 음량 탄띠 크기의 기준 폭 (칸). 이 폭일 때 쉘 20 × 46 · 탄띠 높이 40
        private const float VolumeBeltMax = 440f;  // 음량 탄띠 최대 폭 (넓은 종이에서도 줄이 너무 커지지 않게)

        // 메인화면 판을 띄우는 자리 (m). MainMenu 오브젝트(= 플레이어가 설 바닥 자리) 기준
        private const float MenuDistance = 1.5f;   // 앞으로. 결과판처럼 손을 뻗지 않고 레이로 누르는 거리
        private const float MenuCenterHeight = 1.4f;   // 바닥에서 판 가운데까지

        // 잉크 색
        private static readonly Color InkBrown = new Color32(59, 36, 22, 255);     // 짙은 갈색 : 가운데 글자, 가로줄
        private static readonly Color InkRed = new Color32(110, 36, 24, 255);      // 붉은 갈색 : 맨 위 결과 문구, REWARD
        private static readonly Color AccentRed = new Color32(163, 46, 34, 255);   // 빨강 : 고른 난이도
        private static readonly Color StampRed = new Color32(176, 34, 28, 225);    // 도장 : 조금 비치는 빨강 (아래 몽타주가 보이게)

        // 버튼 색 (나무 간판). 나뭇결 그림에 곱하는 색이라 흰색 = 그림 색 그대로
        //   화면에 보이는 색 = 그림 색 × 상태 색. 곱하면 어두워지기만 하므로 평소를 조금 어둡게 두고, 가리키면 흰색(= 밝게)
        private static readonly Color WoodNormal = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color WoodHighlighted = Color.white;                      // 레이가 올라갔을 때
        private static readonly Color WoodPressed = new Color(0.6f, 0.6f, 0.6f, 1f);      // 눌렀을 때
        private static readonly Color WoodChecked = new Color(1f, 0.55f, 0.45f, 1f);      // 고른 난이도 : 빨갛게 칠한 간판
        private static readonly Color ButtonTextColor = new Color32(243, 230, 200, 255);

        // 게시판 위 종이
        private static readonly Color PaperShadow = new Color(0f, 0f, 0f, 0.35f);          // 종이 밑 그림자
        private static readonly Color PaperNormal = new Color(0.9f, 0.9f, 0.9f, 1f);       // 누르는 종이 : 평소 조금 어둡게, 가리키면 흰색(= 밝게)
        private static readonly Color PaperPressed = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color CircleRed = new Color32(190, 30, 24, 235);           // 고른 수배지에 친 동그라미

        // 그림 (정렬할 때마다 WantedPosterArt 가 새로 그린다). 버튼을 만드는 함수가 여러 곳이라 여기 담아 둔다
        private static Sprite woodSign;

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

            // ① 재료 : 글꼴 + 그림 (그림은 누를 때마다 새로 그린다 → WantedPosterArt 의 숫자를 고치면 바로 반영된다)
            TMP_FontAsset font = LoadFont();
            Sprite paper = WantedPosterArt.CreatePaper("PosterPaper.png", BoardWidth, PaperHeight, 30, 2);
            Sprite nail = WantedPosterArt.CreateNail("Nail.png");
            Sprite hole = WantedPosterArt.CreateBulletHole("BulletHole.png");
            Sprite stampFrame = WantedPosterArt.CreateStampFrame("StampFrame.png");
            Sprite boardPlanks = WantedPosterArt.CreateBoard("ResultBoardPlanks.png", ResultBoardWidth, ResultBoardHeight);
            Sprite notePaper = WantedPosterArt.CreatePaper("BoardNote.png", NoteButtonWidth, NoteButtonHeight, 0, 0);   // 메인화면 쪽지 버튼과 같은 종이

            //    몽타주 : 30.UI/MontageSource 의 초록 배경 얼굴 사진을 판화로 바꾼다 (난이도마다 나오는 적 하나씩)
            //    이름은 enemies.json 을 게임과 같은 표로 읽어서 쓴다
            EnemyTable enemies = new EnemyTable();
            enemies.Load();
            Sprite easyPortrait = WantedPosterArt.BakeMontage("MontageSource_Easy.png", "Montage_Easy.png");
            Sprite normalPortrait = WantedPosterArt.BakeMontage("MontageSource_Normal.png", "Montage_Normal.png");
            Sprite hardPortrait = WantedPosterArt.BakeMontage("MontageSource_Hard.png", "Montage_Hard.png");
            Sprite cowardPortrait = WantedPosterArt.CreateCowardSilhouette("Montage_Coward.png");

            // ② Panel (캔버스). 캔버스는 붙일 것이 많아서 XR > UI Canvas 로 직접 만들게 한다
            Transform panelTransform = board.transform.Find("Panel");
            if (panelTransform == null)
                throw new Exception("[WantedPosterLayout] ResultBoard 아래에 Panel 이 없습니다. ResultBoard 우클릭 > XR > UI Canvas 로 만들고 이름을 Panel 로 바꿔 주세요.");

            RectTransform panel = (RectTransform)panelTransform;
            panel.localPosition = new Vector3(0f, -0.1f, 0f);          // 눈높이보다 10cm 아래 (m)
            panel.localRotation = Quaternion.identity;
            panel.localScale = new Vector3(0.001f, 0.001f, 0.001f);    // 캔버스 1칸 = 1mm
            panel.sizeDelta = new Vector2(ResultBoardWidth, ResultBoardHeight);

            //    판 위의 것은 늘 처음부터 다시 만든다 (메인화면 판과 같다). 예전 모양(Subtitle, RuleTop ...)이 남지 않게
            //    지우면 칸 번호가 당겨지므로 뒤에서부터 지운다. 지운 것도 Ctrl+Z 하면 되살아난다
            for (int i = panel.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(panel.GetChild(i).gameObject);

            // ③ 나무 판 : 판자 그림 + 3D 몸통(뒤판 · 튀어나온 틀). 수배지는 이 판에 못으로 박혀 있다
            //    옆에서 봐도 종이 한 장이 아니라 나무 판으로 보이게 한다 (메인화면 게시판과 같은 방법)
            Image board3D = GetImage(panel, "Board");
            PlaceFromTop(board3D.rectTransform, 0, ResultBoardWidth, ResultBoardHeight);
            board3D.sprite = boardPlanks;
            board3D.type = Image.Type.Simple;
            board3D.color = Color.white;
            board3D.raycastTarget = true;        // 레이가 판에 닿아 멈추게 (어디를 가리키는지 보인다)
            PlaceBoardModel(panel, ResultBoardWidth, ResultBoardHeight);

            // ④ 수배지 : 판 위쪽에 붙인 종이 한 장. 아래 글자 · 그림은 모두 수배지의 자식이다
            //    → 자리는 수배지 위쪽 가운데 기준 (예전에 Panel 에 놓던 숫자 그대로)
            //    만드는 순서 = 그리는 순서 (먼저 만든 것이 뒤에 깔린다) → 종이부터, 도장은 맨 나중에
            Image posterPaper = PlacePaper(panel, "Poster", paper, 0, PosterTopMargin, BoardWidth, PaperHeight, 0f);
            RectTransform poster = posterPaper.rectTransform;

            //    종이를 벽에 박은 못 둘 (녹물이 아래로 흘렀다). 자리는 씬에서 손으로 박은 곳
            PlaceDecoration(poster, "NailLeft", nail, -258.2f, 19.7f, 40, 80);
            PlaceDecoration(poster, "NailRight", nail, 261f, 23.1f, 40, 80);

            //    맨 위 WANTED 는 바뀌지 않는다. 여기서 한 번 써 두면 끝이다
            TextMeshProUGUI title = GetText(poster, "Title");
            PlaceFromTop(title.rectTransform, 50, 500, 130);
            SetText(title, font, "WANTED", 130, InkRed, FontStyles.Bold);
            title.characterSpacing = 8;
            title.enableAutoSizing = true;       // 글자가 칸보다 길면 칸에 맞게 줄인다 (SetText 가 끄므로 그 뒤에 켠다)
            title.fontSizeMin = 60;
            title.fontSizeMax = 130;

            TextMeshProUGUI deadOrAlive = GetText(poster, "DeadOrAlive");
            PlaceFromTop(deadOrAlive.rectTransform, 182, 500, 40);
            SetText(deadOrAlive, font, "•  •  •   DEAD OR ALIVE   •  •  •", 28, InkBrown, FontStyles.Bold);
            deadOrAlive.characterSpacing = 6;

            //    몽타주 (현상범 얼굴). 그림은 편집할 때 보이는 예시(보통 난이도)다. Play 하면 ResultBoard 가 이번 적으로 바꾼다
            Image montage = GetImage(poster, "Montage");
            PlaceFromTop(montage.rectTransform, 228, MontageSize, MontageSize);
            montage.sprite = normalPortrait;
            montage.type = Image.Type.Simple;
            montage.color = Color.white;
            montage.raycastTarget = false;

            //    몽타주 위 총알 구멍 : 몽타주의 자식이라 자리가 몽타주 가운데 기준이다. 이겼을 때 맞힌 부위에 켠다
            Image bulletHole = GetImage(montage.rectTransform, "BulletHole");
            PlaceCenter(bulletHole.rectTransform, 14, 18, 56, 56);   // ResultBoard.HeadHolePosition 과 같은 자리 (편집용 예시)
            bulletHole.sprite = hole;
            bulletHole.color = Color.white;
            bulletHole.raycastTarget = false;

            //    종이에 난 총알 구멍 하나 (꾸밈)
            PlaceDecoration(poster, "StrayBulletHole", hole, 232, 470, 44, 44);

            //    현상범 이름. 글자는 편집할 때 보이는 예시다. Play 하면 ResultBoard 가 enemies.json 의 Name 으로 바꾼다
            TextMeshProUGUI nameText = GetText(poster, "Name");
            PlaceFromTop(nameText.rectTransform, 556, 480, 60);
            SetText(nameText, font, enemies.Get(Difficulty.Normal).Name, 46, InkRed, FontStyles.Bold);
            nameText.characterSpacing = 4;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 30;
            nameText.fontSizeMax = 46;

            Image ruleMiddle = GetImage(poster, "RuleMiddle");
            PlaceLine(ruleMiddle, 624);

            //    예시는 승리의 점수 내역 (ResultBoard.ShowPlayerWin 과 같은 모양)
            TextMeshProUGUI detail = GetText(poster, "Detail");
            PlaceFromTop(detail.rectTransform, 634, 480, 196);
            SetText(detail, font,
                "<align=left><indent=8%>" +
                "기본<pos=70%>+1,000\n" +
                "눈 깜짝할 새<pos=70%>+680\n" +      // 반응속도 0.82초
                "명사수<pos=70%>+500\n" +            // 머리 명중
                "치열한 공방<pos=70%>+500",          // 2라운드
                32, InkBrown, FontStyles.Normal);
            detail.lineSpacing = 8;
            detail.enableAutoSizing = true;      // 줄이 길거나 많으면(승리는 4줄) 칸에 맞게 줄인다
            detail.fontSizeMin = 22;
            detail.fontSizeMax = 32;

            Image ruleBottom = GetImage(poster, "RuleBottom");
            PlaceLine(ruleBottom, 838);

            TextMeshProUGUI reward = GetText(poster, "Reward");
            PlaceFromTop(reward.rectTransform, 846, 500, 86);
            SetText(reward, font, "REWARD $ 2,680", 60, InkRed, FontStyles.Bold);
            reward.characterSpacing = 4;
            reward.enableAutoSizing = true;      // 점수가 커서 글자가 길어지면 줄인다
            reward.fontSizeMin = 40;
            reward.fontSizeMax = 60;

            //    도장 : 몽타주 아래쪽에 비스듬히 찍는다. 테두리 그림 + 글자 (DEAD / AT LARGE / COWARD)
            Image stamp = GetImage(poster, "Stamp");
            PlaceFromTop(stamp.rectTransform, 420, 340, 120);
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, StampAngle);   // PlaceFromTop 이 바로 세우므로 그 뒤에 기울인다
            stamp.sprite = stampFrame;
            stamp.color = StampRed;              // 흰 그림 × 빨강 = 빨간 도장
            stamp.raycastTarget = false;

            //    도장 글자. 글자는 편집할 때 보이는 예시다. Play 하면 ResultBoard 가 결과로 바꾼다
            TextMeshProUGUI stampLabel = GetText(stamp.rectTransform, "Label");
            Stretch(stampLabel.rectTransform);
            stampLabel.rectTransform.offsetMin = new Vector2(18f, 12f);    // 테두리 안쪽으로 (왼쪽, 아래)
            stampLabel.rectTransform.offsetMax = new Vector2(-18f, -10f);  // (오른쪽, 위)
            SetText(stampLabel, font, "DEAD\n<size=42%>승리 · 현상범 처치</size>", 62, StampRed, FontStyles.Bold);
            stampLabel.characterSpacing = 8;
            stampLabel.lineSpacing = -18;
            stampLabel.enableAutoSizing = true;  // AT LARGE 처럼 긴 글자는 칸에 맞게 줄인다
            stampLabel.fontSizeMin = 36;
            stampLabel.fontSizeMax = 62;

            //    아래 버튼 둘을 나란히 : 왼쪽 메인으로, 오른쪽 다시 결투 (메인화면의 뒤로 · 결투 시작과 같은 줄 모양)
            //    메인화면 게시판과 같은 핀으로 박은 쪽지 버튼 (그림자 + 종이 + 핀, 조금씩 기울게)
            //    못 자리(뒤의 두 숫자)는 씬에서 손으로 박은 곳
            Button mainMenuButton = PlaceNoteButton(panel, font, "MainMenuButton", notePaper, nail, "메인으로", InkBrown, -145, ButtonTop, NoteButtonWidth, NoteButtonHeight, -2f, -0.5f, 15.2f);
            Button retryButton = PlaceNoteButton(panel, font, "RetryButton", notePaper, nail, "다시 결투", InkRed, 145, ButtonTop, NoteButtonWidth, NoteButtonHeight, 2f, 0f, 16.1f);

            // ⑤ ResultBoard 의 칸에 연결한다 (Inspector 칸에 끌어다 넣는 일을 코드로 한다)
            //    칸이 private 이라 board.resultText = stampLabel 로는 못 넣는다.
            //    SerializedObject 는 "Inspector 가 보는 그 칸들" 을 이름으로 꺼내 주는 창구다.
            SerializedObject boardData = new SerializedObject(board);
            SetField(boardData, "panel", panel.gameObject);
            SetField(boardData, "resultText", stampLabel);
            SetField(boardData, "nameText", nameText);
            SetField(boardData, "detailText", detail);
            SetField(boardData, "rewardText", reward);
            SetField(boardData, "montageImage", montage);
            SetField(boardData, "bulletHole", bulletHole.rectTransform);
            SetField(boardData, "retryButton", retryButton);
            SetField(boardData, "mainMenuButton", mainMenuButton);
            SetField(boardData, "easyPortrait", easyPortrait);
            SetField(boardData, "normalPortrait", normalPortrait);
            SetField(boardData, "hardPortrait", hardPortrait);
            SetField(boardData, "cowardPortrait", cowardPortrait);
            boardData.ApplyModifiedProperties();

            // 씬이 바뀌었다고 표시한다 (씬 이름 옆에 * 가 붙고, Ctrl+S 로 저장할 수 있다)
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            Debug.Log("[WantedPosterLayout] 결과판을 수배지 모양으로 정렬했습니다. Assets/30.UI 의 Montage_*.png 로 몽타주를 확인하고, Ctrl+S 로 씬을 저장해 주세요.");
        }

        // ---------------------------------------------------------------- 라운드 쪽지 (RoundBoard)

        [MenuItem("CONTEXT/RoundBoard/쪽지 모양으로 정렬")]
        private static void ArrangeNoteFromContext(MenuCommand command)
        {
            ArrangeNote((RoundBoard)command.context);
        }

        // 결과판(Arrange)과 같은 순서다 : 재료 → Panel → 자식 → 칸 연결
        //   쪽지(탄피 카드)뿐 아니라 무승부 뒤 할 일을 보여 주는 둘도 같이 만든다
        //     홀스터 빛    : 홀스터 칸 아래 HolsterGlow (총을 꽂을 때까지 빛난다)
        //     전진 표식 팻말 : AdvanceMarker 아래 Sign (표식과 같이 켜지고 꺼진다)
        private static void ArrangeNote(RoundBoard board)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("[WantedPosterLayout] Play 를 멈춘 뒤에 눌러 주세요. Play 중에 바꾼 것은 멈추면 사라집니다.");

            Undo.RegisterFullObjectHierarchyUndo(board.gameObject, UndoName);

            // ① 재료 (쪽지는 작아서 종이를 따로 그린다. 큰 종이를 눌러 넣으면 테두리가 찌그러진다)
            TMP_FontAsset font = LoadFont();
            Sprite paper = WantedPosterArt.CreatePaper("NotePaper.png", NoteWidth, NoteHeight, 22, 0);
            Sprite cylinder = WantedPosterArt.CreateCylinder("Cylinder.png");
            Sprite cartridge = WantedPosterArt.CreateCartridge("Cartridge.png");
            Sprite nail = WantedPosterArt.CreateNail("Nail.png");
            Sprite boardPlanks = WantedPosterArt.CreateBoard("NoteBoardPlanks.png", NoteBoardWidth, NoteBoardHeight);

            // ② Panel (캔버스). 없으면 만든다
            RectTransform panel = GetOrCreateCanvas(board.transform, "Panel");
            panel.localPosition = new Vector3(0f, -0.35f, 0f);         // 눈높이보다 35cm 아래 (m) → 적을 가리지 않는다
            panel.localRotation = Quaternion.identity;
            panel.localScale = new Vector3(0.001f, 0.001f, 0.001f);    // 캔버스 1칸 = 1mm
            panel.sizeDelta = new Vector2(NoteBoardWidth, NoteBoardHeight);

            //    판 위의 것은 늘 처음부터 다시 만든다 (예전 모양이 남지 않게). 뒤에서부터 지운다
            for (int i = panel.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(panel.GetChild(i).gameObject);

            // ③ 나무 판 : 판자 그림 + 3D 몸통(뒤판 · 튀어나온 틀). 결과판 · 메인화면 게시판과 같은 방법
            //    누를 것이 없어서 레이를 받지 않는다 (텔레포트 레이도 막지 않게)
            Image boardImage = GetImage(panel, "Board");
            PlaceFromTop(boardImage.rectTransform, 0, NoteBoardWidth, NoteBoardHeight);
            boardImage.sprite = boardPlanks;
            boardImage.type = Image.Type.Simple;
            boardImage.color = Color.white;
            boardImage.raycastTarget = false;
            PlaceBoardModel(panel, NoteBoardWidth, NoteBoardHeight);

            // ④ 쪽지 : 판 가운데에 핀으로 박은 종이 한 장. 아래 글자 · 그림은 모두 쪽지의 자식이다
            //    → 자리는 쪽지 위쪽 가운데 기준. 만드는 순서 = 그리는 순서
            Image notePaper = PlacePaper(panel, "Note", paper, 0, NoteMargin, NoteWidth, NoteHeight, -1f);
            RectTransform note = notePaper.rectTransform;
            PlaceDecoration(note, "PinLeft", nail, -267.2f, 21f, 24, 48);   // 씬에서 손으로 박은 자리
            PlaceDecoration(note, "PinRight", nail, 250f, 24.5f, 24, 48);

            //    맨 위 한 줄. 글자는 편집할 때 보이는 예시다. Play 하면 RoundBoard 가 결과로 바꾼다
            TextMeshProUGUI title = GetText(note, "Title");
            PlaceFromTop(title.rectTransform, 30, 520, 58);
            SetText(title, font, "둘 다 빗나감", 50, InkRed, FontStyles.Bold);
            title.characterSpacing = 4;

            //    왼쪽 실린더. 약실 6칸 위에 탄 6개를 겹친다 (탄은 실린더의 자식 → 자리는 실린더 가운데 기준)
            Image cylinderImage = GetImage(note, "Cylinder");
            PlaceFromTopAt(cylinderImage.rectTransform, -140, 100, CylinderDisplaySize, CylinderDisplaySize);
            cylinderImage.sprite = cylinder;
            cylinderImage.color = Color.white;
            cylinderImage.raycastTarget = false;

            float scale = (float)CylinderDisplaySize / WantedPosterArt.CylinderSize;   // 그림 점 → 캔버스 칸
            float ringRadius = WantedPosterArt.ChamberRingRadius * scale;
            float cartridgeSize = WantedPosterArt.ChamberRadius * 2f * scale;
            GameObject[] cartridges = new GameObject[ChamberCount];
            for (int i = 0; i < ChamberCount; i++)
            {
                // 맨 위 칸부터 시계 방향 (그림의 약실 순서와 같다)
                float angle = (90f - i * 60f) * Mathf.Deg2Rad;
                Image round = GetImage(cylinderImage.rectTransform, $"Cartridge{i + 1}");
                PlaceCenter(round.rectTransform, Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius, cartridgeSize, cartridgeSize);
                round.sprite = cartridge;
                round.color = Color.white;
                round.raycastTarget = false;
                cartridges[i] = round.gameObject;
            }

            //    편집할 때 예시 : 2라운드 무승부 → 2발 쏘고 4발 남음
            cartridges[0].SetActive(false);
            cartridges[1].SetActive(false);

            //    오른쪽 두 사람의 시간. 예시 글자다
            TextMeshProUGUI detail = GetText(note, "Detail");
            PlaceFromTopAt(detail.rectTransform, 100, 112, 280, 130);
            SetText(detail, font, "나   0.95초\n적   0.87초", 40, InkBrown, FontStyles.Bold);
            detail.lineSpacing = 16;

            // ⑤ 홀스터 빛 · 전진 표식 팻말 (씬에 하나씩 있는 것을 찾아서 그 아래에 만든다)
            HolsterGlow glow = PlaceHolsterGlow();
            PlaceAdvanceSign(font);

            // ⑥ RoundBoard 의 칸에 연결한다
            SerializedObject boardData = new SerializedObject(board);
            SetField(boardData, "panel", panel.gameObject);
            SetField(boardData, "titleText", title);
            SetField(boardData, "detailText", detail);
            SetField(boardData, "holsterGlow", glow);
            SetArrayField(boardData, "cartridges", cartridges);
            boardData.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            Debug.Log("[WantedPosterLayout] 라운드 쪽지 · 홀스터 빛 · 전진 표식 팻말을 만들었습니다. Ctrl+S 로 씬을 저장해 주세요.");
        }

        // 홀스터 칸 아래에 빛 구를 만든다. 있으면 지우고 다시 만든다
        private static HolsterGlow PlaceHolsterGlow()
        {
            HolsterSlot slot = Object.FindFirstObjectByType<HolsterSlot>(FindObjectsInactive.Include);
            if (slot == null)
                throw new Exception("[WantedPosterLayout] 씬에 HolsterSlot 이 없습니다. Town 씬의 XR Origin 아래 홀스터를 확인해 주세요.");

            Transform old = slot.transform.Find("HolsterGlow");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            // 구 (충돌체는 뗀다. 총을 꽂는 소켓 판정을 가로채지 않게)
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(sphere, UndoName);
            sphere.name = "HolsterGlow";
            Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());
            sphere.transform.SetParent(slot.transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localRotation = Quaternion.identity;
            SetWorldScale(sphere.transform, Vector3.one * HolsterGlowSize);   // 부모 크기와 상관없이 지름 HolsterGlowSize (m)

            MeshRenderer glowRenderer = sphere.GetComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = GetOrCreateGlowMaterial("HolsterGlow.mat");
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 빛 덩어리라 그림자를 만들지 않는다
            glowRenderer.receiveShadows = false;

            HolsterGlow glow = Undo.AddComponent<HolsterGlow>(sphere);
            SerializedObject glowData = new SerializedObject(glow);
            SetField(glowData, "glowRenderer", glowRenderer);
            glowData.ApplyModifiedProperties();
            return glow;
        }

        // 전진 표식 옆에 나무 말뚝 + 팻말을 세운다. 표식의 자식이라 표식과 같이 켜지고 꺼진다
        //   표식 = 플레이어가 설 자리. 플레이어는 표식 뒤에서 걸어오며 적 쪽(표식의 앞)을 본다
        //   → 팻말 글자는 표식의 뒤쪽(-Z, 플레이어 쪽)을 향하게 단다
        private static void PlaceAdvanceSign(TMP_FontAsset font)
        {
            AdvanceMarker marker = Object.FindFirstObjectByType<AdvanceMarker>(FindObjectsInactive.Include);
            if (marker == null)
                throw new Exception("[WantedPosterLayout] 씬에 AdvanceMarker 가 없습니다.");

            Transform old = marker.transform.Find("Sign");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            // ① 팻말 묶음 : 표식 바로 앞(적 쪽)에 세운다. 표식이 납작한 원기둥이라 크기가 찌그러져 있을 수 있어서
            //    월드 크기를 1 로 맞춰 둔다 → 아래 자식들은 m 단위로 바로 적을 수 있다
            GameObject sign = new GameObject("Sign");
            Undo.RegisterCreatedObjectUndo(sign, UndoName);
            sign.transform.SetParent(marker.transform, false);
            sign.transform.position = marker.transform.position
                                      + marker.transform.right * SignSideOffset
                                      + marker.transform.forward * SignForwardOffset;
            sign.transform.rotation = marker.transform.rotation;
            SetWorldScale(sign.transform, Vector3.one);

            // ② 말뚝과 팻말 판 : 게시판 틀과 같은 나무 재질
            Texture2D grain = WantedPosterArt.CreateWoodGrain("WoodGrain.png");
            Material wood = GetOrCreateMaterial("BoardFrame.mat", grain, Color.white);
            PlaceBlock(sign.transform, "Post", wood, new Vector3(0f, SignPostHeight * 0.5f, 0f), new Vector3(SignPostHeight, 0.06f, 0.06f), 90f);   // 가로 막대를 세운다 (나뭇결이 세로로)
            PlaceBlock(sign.transform, "Board", wood, new Vector3(0f, SignBoardY, -0.045f), new Vector3(SignBoardWidth, SignBoardHeight, 0.03f), 0f);

            // ③ 글자 : 팻말 판 앞면(-Z)에 붙인 3D 글자 (UI 가 아니라 TextMeshPro)
            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textObject, UndoName);
            textObject.transform.SetParent(sign.transform, false);
            TextMeshPro text = Undo.AddComponent<TextMeshPro>(textObject);
            text.rectTransform.localPosition = new Vector3(0f, SignBoardY, -0.062f);   // 판 앞면(-0.06)보다 2mm 앞
            text.rectTransform.localRotation = Quaternion.identity;                    // 글자 앞면이 -Z 를 본다
            text.rectTransform.sizeDelta = new Vector2(SignBoardWidth - 0.06f, SignBoardHeight - 0.05f);
            text.font = font;
            text.text = "한 걸음 앞으로";
            text.color = ButtonTextColor;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;        // 판 크기에 맞춰 글자 크기를 정한다 (3D 글자 크기 숫자는 감 잡기 어렵다)
            text.fontSizeMin = 0.1f;
            text.fontSizeMax = 10f;
        }

        // 부모 크기와 상관없이 월드에서 size 크기가 되게 localScale 을 정한다 (부모가 돌아가 있지 않다고 본다)
        //   lossyScale = 부모들의 크기를 다 곱한 실제 크기
        private static void SetWorldScale(Transform target, Vector3 size)
        {
            Vector3 parentScale = target.parent.lossyScale;
            target.localScale = new Vector3(size.x / parentScale.x, size.y / parentScale.y, size.z / parentScale.z);
        }

        // 빛 구 재질 : URP Unlit, 덧칠(Additive) 반투명. 어두운 곳일수록 밝게 보이고 뒤 물체를 가리지 않는다
        //   투명 설정은 키워드가 필요해서 에디터에서 재질 파일로 만들어 둔다 (빌드에 그 셰이더 변형이 들어가게)
        private static Material GetOrCreateGlowMaterial(string fileName)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new Exception("[WantedPosterLayout] URP Unlit 셰이더를 찾지 못했습니다. 프로젝트가 URP 인지 확인해 주세요.");

            string path = "Assets/30.UI/" + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetFloat("_Surface", 1f);   // 1 = Transparent
            material.SetFloat("_Blend", 2f);     // 2 = Additive
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);    // 깊이를 안 써서 뒤의 총 · 홀스터가 그대로 보인다
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(1f, 0.72f, 0.3f, 0.2f));   // HolsterGlow 가 실행 중에 투명도를 흔든다
            EditorUtility.SetDirty(material);
            return material;
        }

        // 보드 스크립트의 배열 칸에 넣는다 (cartridges 처럼 [ ] 로 된 칸)
        private static void SetArrayField(SerializedObject data, string fieldName, Object[] values)
        {
            SerializedProperty field = data.FindProperty(fieldName);
            if (field == null || field.isArray == false)
                throw new Exception($"[WantedPosterLayout] {data.targetObject.GetType().Name} 에 {fieldName} 배열 칸이 없습니다. 필드 이름이 바뀌었으면 여기도 고쳐 주세요.");

            field.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                field.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
        //   다른 점 : 판 위의 것을 화면 여섯 장(첫 화면 / 게임 시작 / 홀스터 조정 / 주 손 고르기 / 손 따라가기 / 음량 조절)으로 나눠 담는다
        private static void ArrangeMenu(MainMenu menu)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("[WantedPosterLayout] Play 를 멈춘 뒤에 눌러 주세요. Play 중에 바꾼 것은 멈추면 사라집니다.");

            Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, UndoName);

            // ① 재료 (그림은 누를 때마다 새로 그린다)
            //    게시판 위에 붙는 종이는 크기마다 따로 그린다 (큰 종이를 눌러 넣으면 테두리 · 그을림이 찌그러진다)
            TMP_FontAsset font = LoadFont();
            Sprite board = WantedPosterArt.CreateBoard("BountyBoard.png", MenuWidth, MenuHeight);
            Sprite posterPaper = WantedPosterArt.CreatePaper("BoardPoster.png", PosterWidth, PosterHeight, 12, 1);
            Sprite notePaper = WantedPosterArt.CreatePaper("BoardNote.png", NoteButtonWidth, NoteButtonHeight, 0, 0);
            Sprite noticePaper = WantedPosterArt.CreatePaper("BoardNotice.png", NoticeWidth, NoticeHeight, 10, 0);
            Sprite sheetPaper = WantedPosterArt.CreatePaper("BoardSheet.png", SheetWidth, SheetHeight, 20, 1);
            woodSign = WantedPosterArt.CreateWoodSign("WoodSign.png");
            Sprite nail = WantedPosterArt.CreateNail("Nail.png");
            Sprite circleMark = WantedPosterArt.CreateCircleMark("CircleMark.png");
            Sprite ammoBelt = WantedPosterArt.CreateAmmoBelt("AmmoBelt.png");            // 음량 조절 : 가죽 탄띠
            Sprite shotgunShell = WantedPosterArt.CreateShotgunShell("ShotgunShell.png");   // 음량 조절 : 탄띠에 꽂는 쉘 (한 발 = 10%)
            Sprite beltStrap = WantedPosterArt.CreateBeltStrap("BeltStrap.png");            // 음량 조절 : 쉘을 쥐는 가죽 고리 (늘 보인다)

            //    현상범 몽타주 · 이름 · 기본 점수 (결과판과 같은 그림, 같은 표)
            EnemyTable enemies = new EnemyTable();
            enemies.Load();
            Sprite easyPortrait = WantedPosterArt.BakeMontage("MontageSource_Easy.png", "Montage_Easy.png");
            Sprite normalPortrait = WantedPosterArt.BakeMontage("MontageSource_Normal.png", "Montage_Normal.png");
            Sprite hardPortrait = WantedPosterArt.BakeMontage("MontageSource_Hard.png", "Montage_Hard.png");

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

            // ③ 모든 화면이 같이 쓰는 것 : 나무 게시판 · 맨 위 간판 · 그 아래 종이 띠
            //    게시판을 맨 먼저 만들어야 맨 뒤에 깔린다 (Hierarchy 에서 위에 있을수록 먼저 그린다)
            Image background = GetImage(panel, "Background");
            PlaceFromTop(background.rectTransform, 0, MenuWidth, MenuHeight);
            background.sprite = board;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.raycastTarget = true;     // 레이가 게시판에 닿아 멈추게 (어디를 가리키는지 보인다)

            //    게시판 몸통 (3D) : 판자 그림 뒤의 두꺼운 뒤판 + 둘레를 감싸며 앞으로 튀어나온 나무 막대 넷
            PlaceBoardModel(panel, MenuWidth, MenuHeight);

            //    간판 : 나무 간판 그림에 크림색 글자 (버튼과 같은 그림이지만 누르는 것이 아니다)
            PlaceShadow(panel, "TitleSignShadow", woodSign, 0, 55, 540, 104, 0f);
            Image titleSign = GetImage(panel, "TitleSign");
            PlaceFromTop(titleSign.rectTransform, 55, 540, 104);   // 틀(30) 과 수배지(200) 사이 가운데쯤
            titleSign.sprite = woodSign;
            titleSign.type = Image.Type.Sliced;
            titleSign.color = Color.white;
            titleSign.raycastTarget = false;
            TextMeshProUGUI title = GetText(titleSign.rectTransform, "Title");
            Stretch(title.rectTransform);
            SetText(title, font, "정오의 결투", 64, ButtonTextColor, FontStyles.Bold);
            title.characterSpacing = 8;

            // ④ 화면 다섯 장. 화면마다 빈 오브젝트 하나에 모아 두고, MainMenu 가 한 장씩 켜고 끈다
            //    화면은 Panel 을 꽉 채우므로, 화면 안의 자리는 Panel 에 놓을 때와 같은 숫자를 쓴다
            //    게시판 왼쪽 2/3 = 수배지 · 공고, 오른쪽 기둥 자리 = 누르는 쪽지들

            //    [첫 화면] 수배지 셋(구경만) · 쪽지 버튼 둘 · 아래 공고 · 회중시계
            RectTransform homePage = CreatePage(panel, "HomePage");

            PlacePosterImage(homePage, font, "EasyPoster", posterPaper, nail, easyPortrait, enemies.Get(Difficulty.Easy), 0);
            PlacePosterImage(homePage, font, "NormalPoster", posterPaper, nail, normalPortrait, enemies.Get(Difficulty.Normal), 1);
            PlacePosterImage(homePage, font, "HardPoster", posterPaper, nail, hardPortrait, enemies.Get(Difficulty.Hard), 2);

            //    쪽지 못 자리(뒤의 두 숫자)는 씬에서 손으로 박은 자리다. 쪽지마다 조금씩 다르다
            Button duelMenuButton = PlaceNoteButton(homePage, font, "DuelMenuButton", notePaper, nail, "결투 신청", InkRed, RightColumnX, 215, NoteButtonWidth, NoteButtonHeight, 2f, 1.2f, 12.1f);
            Button holsterMenuButton = PlaceNoteButton(homePage, font, "HolsterMenuButton", notePaper, nail, "홀스터 조정", InkBrown, RightColumnX, 345, NoteButtonWidth, NoteButtonHeight, -2f, -0.3f, 14.1f);
            Button volumeMenuButton = PlaceNoteButton(homePage, font, "VolumeMenuButton", notePaper, nail, "음량 조절", InkBrown, 342, 437, NoteButtonWidth, NoteButtonHeight, 2f, 1.5f, 10.5f);   // 씬에서 손으로 맞춘 자리 · 크기
            volumeMenuButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;

            PlaceNotice(homePage, font, "Notice", noticePaper, nail,
                "<size=120%><b>공  고</b></size>\n수배자를 쓰러뜨린 자에게 현상금을 지급함\n단, 정오의 종보다 먼저 총을 뽑은 자 또한 무법자로 수배되니 주의할 것\n<size=80%>— 보안관 사무소</size>",
                new Vector2(-331f, 85f), new Vector2(323f, 88f));

            //    [게임 시작 화면] 수배지 셋이 곧 난이도 Toggle 이다 → 잡을 현상범을 고르면 빨간 동그라미가 쳐진다
            RectTransform duelPage = CreatePage(panel, "DuelPage");

            //    Toggle 셋을 한 묶음(Toggle Group)에 넣는다 → 하나를 켜면 나머지가 꺼진다
            //    묶음은 화면을 꽉 채우는 빈 오브젝트라서, 안의 자리도 Panel 에 놓을 때와 같은 숫자를 쓴다
            GameObject groupObject = GetOrCreateChild(duelPage, "DifficultyGroup");
            RectTransform groupRect = (RectTransform)groupObject.transform;
            Stretch(groupRect);
            ToggleGroup group = Undo.AddComponent<ToggleGroup>(groupObject);   // 방금 만든 빈 오브젝트라 붙어 있는 것이 없다
            group.allowSwitchOff = false;        // 셋 다 꺼질 수 없게 (늘 하나는 골라져 있다)

            Toggle easyToggle = PlacePosterToggle(groupRect, group, font, "EasyToggle", posterPaper, nail, circleMark, easyPortrait, enemies.Get(Difficulty.Easy), 0);
            Toggle normalToggle = PlacePosterToggle(groupRect, group, font, "NormalToggle", posterPaper, nail, circleMark, normalPortrait, enemies.Get(Difficulty.Normal), 1);
            Toggle hardToggle = PlacePosterToggle(groupRect, group, font, "HardToggle", posterPaper, nail, circleMark, hardPortrait, enemies.Get(Difficulty.Hard), 2);

            //    편집할 때는 보통이 골라져 있게 한다 (Play 하면 MainMenu 가 지금 난이도로 다시 고른다)
            //    보통을 먼저 켜야 한다. 하나도 안 켜진 채로 끄려고 하면 Toggle Group 이 못 끄게 막는다
            normalToggle.isOn = true;
            easyToggle.isOn = false;
            hardToggle.isOn = false;

            Image pickNote = PlacePaper(duelPage, "PickNote", notePaper, RightColumnX, 210, NoteButtonWidth, 110, -1f);
            PlaceDecoration(pickNote.rectTransform, "Pin", nail, 2.6f, 8.8f, 24, 48);
            TextMeshProUGUI pickText = GetText(pickNote.rectTransform, "Text");
            Stretch(pickText.rectTransform);
            SetText(pickText, font, "잡을 현상범의\n수배지를 고르세요", 22, InkBrown, FontStyles.Bold);
            pickText.lineSpacing = 6;

            Button startButton = PlaceNoteButton(duelPage, font, "StartButton", notePaper, nail, "결투 시작", InkRed, RightColumnX, 350, NoteButtonWidth, NoteButtonHeight, 2f, -1.9f, 15.3f);
            Button duelBackButton = PlaceNoteButton(duelPage, font, "DuelBackButton", notePaper, nail, "뒤로", InkBrown, RightColumnX, 475, 190, 84, -2f, 2.4f, 10.5f);

            PlaceNotice(duelPage, font, "Notice", noticePaper, nail,
                "<size=120%><b>포상 안내</b></size>\n빠른 손 · 머리 명중 · 빠른 결판에는 추가 포상\n<size=80%>— 보안관 사무소</size>",
                new Vector2(-325.9f, 89.3f), new Vector2(321.2f, 91f));


            //    [홀스터 조정 화면] 게시판에 붙인 큰 종이 한 장(Sheet) 위에 조정 버튼들. 뒤로는 오른쪽 쪽지
            //    방향 버튼 이름은 '~쪽' 으로 맞췄다. '뒤로' 는 화면을 돌아가는 버튼이라 헷갈리지 않게
            //    좌우는 '왼쪽 · 오른쪽' 이다. 왼손잡이는 홀스터가 왼쪽 허리라서 '안쪽 · 바깥쪽' 이 손마다 반대가 된다
            //    Sheet 안의 자리는 Sheet 위쪽 가운데 기준이다
            RectTransform holsterPage = CreatePage(panel, "HolsterPage");
            HolsterSheet holster = PlaceHolsterSheet(holsterPage, font, sheetPaper, nail, true, MainSheet);

            Button holsterBackButton = PlaceNoteButton(holsterPage, font, "HolsterBackButton", notePaper, nail, "뒤로", InkBrown, RightColumnX, 610, 190, 84, -2f, 0.3f, 8.6f);

            //    [주 손 고르기] · [손 따라가기] : 빠른 조정 1 · 2단계 (일시 정지 판과 같은 종이)
            QuickPages quick = PlaceQuickPages(panel, font, sheetPaper, nail, MainSheet);
            RectTransform handPage = quick.HandPage;
            RectTransform followPage = quick.FollowPage;
            Button leftHandButton = quick.LeftHand;
            Button rightHandButton = quick.RightHand;
            TextMeshProUGUI followText = quick.FollowText;
            Button handBackButton = PlaceNoteButton(handPage, font, "HandBackButton", notePaper, nail, "뒤로", InkBrown, RightColumnX, 610, 190, 84, -2f, 0.3f, 8.6f);   // 홀스터 조정의 뒤로와 같은 자리

            //    [음량 조절 화면] 홀스터 조정처럼 큰 종이 한 장 위에 줄 넷 (이름표 | 슬라이더 | %). 뒤로는 오른쪽 쪽지
            //    슬라이더 = 가죽 탄띠. 음량만큼 샷건 쉘이 꽂힌다 (10발 = 100%)
            RectTransform volumePage = CreatePage(panel, "VolumePage");
            PlaceVolumeSheet(volumePage, font, sheetPaper, nail, ammoBelt, shotgunShell, beltStrap, MainSheet);

            Button volumeBackButton = PlaceNoteButton(volumePage, font, "VolumeBackButton", notePaper, nail, "뒤로", InkBrown, RightColumnX, 610, 190, 84, -2f, 0.3f, 8.6f);   // 홀스터 조정의 뒤로와 같은 자리


            //    편집할 때는 첫 화면만 보이게 한다 (Play 하면 MainMenu 가 어차피 첫 화면부터 연다)
            //    다른 화면을 보고 싶으면 Hierarchy 에서 그 화면을 잠깐 켜서 본다
            //    난이도 Toggle 을 고른 "뒤에" 끈다. 꺼진 화면에서는 Toggle Group 이 나머지를 못 끈다
            duelPage.gameObject.SetActive(false);
            holsterPage.gameObject.SetActive(false);
            handPage.gameObject.SetActive(false);
            followPage.gameObject.SetActive(false);
            volumePage.gameObject.SetActive(false);

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
            SetHolsterFields(menuData, holster);
            SetField(menuData, "quickButton", holster.Quick);
            SetField(menuData, "holsterBackButton", holsterBackButton);

            SetField(menuData, "handPage", handPage.gameObject);
            SetField(menuData, "leftHandButton", leftHandButton);
            SetField(menuData, "rightHandButton", rightHandButton);
            SetField(menuData, "handBackButton", handBackButton);
            SetField(menuData, "followPage", followPage.gameObject);
            SetField(menuData, "followText", followText);

            SetField(menuData, "volumeMenuButton", volumeMenuButton);
            SetField(menuData, "volumePage", volumePage.gameObject);
            SetField(menuData, "volumeBackButton", volumeBackButton);
            menuData.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            Debug.Log("[WantedPosterLayout] 메인화면 판을 정렬했습니다. Ctrl+S 로 씬을 저장해 주세요.");
        }

        // ---------------------------------------------------------------- 일시 정지 판 (PauseMenu, Town 씬)

        [MenuItem("CONTEXT/PauseMenu/일시 정지 판 정렬")]
        private static void ArrangePauseFromContext(MenuCommand command)
        {
            ArrangePause((PauseMenu)command.context);
        }

        // 메인화면 판(ArrangeMenu)과 같은 게시판 · 종이 · 쪽지로 만든다. 순서도 같다 : 재료 → Panel → 자식 → 칸 연결
        //   화면 다섯 장 : 첫 화면(간판 '일시 정지' + 아래 쪽지 버튼 넷) / 홀스터 조정 / 주 손 고르기 / 손 따라가기 / 음량 조절
        //   PauseMenu 오브젝트는 멈출 때 머리 앞으로 옮겨진다 → Panel 은 그 자리 바로(눈높이 10cm 아래)에 둔다
        private static void ArrangePause(PauseMenu menu)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("[WantedPosterLayout] Play 를 멈춘 뒤에 눌러 주세요. Play 중에 바꾼 것은 멈추면 사라집니다.");

            Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, UndoName);

            // ① 재료 (메인화면 판과 같은 그림 파일을 다시 그려 쓴다)
            TMP_FontAsset font = LoadFont();
            Sprite board = WantedPosterArt.CreateBoard("BountyBoard.png", MenuWidth, MenuHeight);
            Sprite notePaper = WantedPosterArt.CreatePaper("BoardNote.png", NoteButtonWidth, NoteButtonHeight, 0, 0);
            Sprite sheetPaper = WantedPosterArt.CreatePaper("PauseSheet.png", (int)PauseSheet.Width, (int)PauseSheet.Height, 20, 1);   // 일시 정지 판 종이는 크기가 달라 따로 그린다
            Sprite pauseNote = WantedPosterArt.CreatePaper("PauseNote.png", PauseButtonWidth, PauseButtonHeight, 0, 0);           // 첫 화면 큰 쪽지 (작은 쪽지를 늘리면 테두리가 찌그러진다)
            woodSign = WantedPosterArt.CreateWoodSign("WoodSign.png");
            Sprite nail = WantedPosterArt.CreateNail("Nail.png");
            Sprite ammoBelt = WantedPosterArt.CreateAmmoBelt("AmmoBelt.png");
            Sprite shotgunShell = WantedPosterArt.CreateShotgunShell("ShotgunShell.png");
            Sprite beltStrap = WantedPosterArt.CreateBeltStrap("BeltStrap.png");

            // ② Panel (캔버스). 버튼을 레이로 눌러야 해서 XR > UI Canvas 로 직접 만들게 한다
            Transform panelTransform = menu.transform.Find("Panel");
            if (panelTransform == null)
                throw new Exception("[WantedPosterLayout] PauseMenu 아래에 Panel 이 없습니다. PauseMenu 우클릭 > XR > UI Canvas 로 만들고 이름을 Panel 로 바꿔 주세요.");

            RectTransform panel = (RectTransform)panelTransform;
            panel.localPosition = new Vector3(0f, -0.1f, 0f);         // 눈높이보다 10cm 아래 (m). 결과판과 같다
            panel.localRotation = Quaternion.identity;                 // 플레이어와 같은 쪽을 본다 → 앞면이 보인다
            panel.localScale = new Vector3(0.001f, 0.001f, 0.001f);    // 캔버스 1칸 = 1mm
            panel.sizeDelta = new Vector2(MenuWidth, MenuHeight);

            for (int i = panel.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(panel.GetChild(i).gameObject);

            // ③ 나무 게시판 + 맨 위 간판
            Image background = GetImage(panel, "Background");
            PlaceFromTop(background.rectTransform, 0, MenuWidth, MenuHeight);
            background.sprite = board;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.raycastTarget = true;     // 레이가 게시판에 닿아 멈추게
            PlaceBoardModel(panel, MenuWidth, MenuHeight);

            PlaceShadow(panel, "TitleSignShadow", woodSign, 0, PauseTitleTop, 540, 104, 0f);
            Image titleSign = GetImage(panel, "TitleSign");
            PlaceFromTop(titleSign.rectTransform, PauseTitleTop, 540, 104);
            titleSign.sprite = woodSign;
            titleSign.type = Image.Type.Sliced;
            titleSign.color = Color.white;
            titleSign.raycastTarget = false;
            TextMeshProUGUI title = GetText(titleSign.rectTransform, "Title");
            Stretch(title.rectTransform);
            SetText(title, font, "일시 정지", TextTitle, ButtonTextColor, FontStyles.Bold);
            title.characterSpacing = 8;

            // ④ [첫 화면] 간판 아래 가운데에 쪽지 버튼 넷을 세로로. 손으로 박은 것처럼 기울기 · 못 자리를 조금씩 다르게
            //    판에 버튼만 있어서 크게 (360 × 110, 글자는 종이 제목 크기)
            RectTransform homePage = CreatePage(panel, "HomePage");
            Button resumeButton = PlacePauseButton(homePage, font, pauseNote, nail, "ResumeButton", "계속", InkRed, 0, 1.5f, new Vector2(1.2f, 12.1f));
            Button holsterMenuButton = PlacePauseButton(homePage, font, pauseNote, nail, "HolsterMenuButton", "홀스터 조정", InkBrown, 1, -1.5f, new Vector2(-0.3f, 14.1f));
            Button volumeMenuButton = PlacePauseButton(homePage, font, pauseNote, nail, "VolumeMenuButton", "음량 조절", InkBrown, 2, 1f, new Vector2(1.5f, 10.5f));
            Button mainMenuButton = PlacePauseButton(homePage, font, pauseNote, nail, "MainMenuButton", "메인으로", InkBrown, 3, -1f, new Vector2(2.4f, 10.5f));

            // ⑤ [홀스터 조정] · [음량 조절] : 판 가운데 넓은 종이(PauseSheet) + 그 아래 가운데 '뒤로' 쪽지
            //    (메인화면은 오른쪽에 쪽지 줄이 있어서 종이를 왼쪽에 붙이지만, 이 판은 쪽지 줄이 없다)
            RectTransform holsterPage = CreatePage(panel, "HolsterPage");
            HolsterSheet holster = PlaceHolsterSheet(holsterPage, font, sheetPaper, nail, true, PauseSheet);
            Button holsterBackButton = PlacePauseBackButton(holsterPage, font, notePaper, nail, "HolsterBackButton");

            //    빠른 조정 1 · 2단계 (메인화면과 같은 화면). 손 따라가기에는 버튼을 두지 않는다 (트리거가 확정이라서)
            QuickPages quick = PlaceQuickPages(panel, font, sheetPaper, nail, PauseSheet);
            Button handBackButton = PlacePauseBackButton(quick.HandPage, font, notePaper, nail, "HandBackButton");

            RectTransform volumePage = CreatePage(panel, "VolumePage");
            PlaceVolumeSheet(volumePage, font, sheetPaper, nail, ammoBelt, shotgunShell, beltStrap, PauseSheet);
            Button volumeBackButton = PlacePauseBackButton(volumePage, font, notePaper, nail, "VolumeBackButton");

            holsterPage.gameObject.SetActive(false);
            volumePage.gameObject.SetActive(false);

            // ⑥ 버튼 소리 : Panel 에 UISound 가 없으면 붙이고, 씬의 다른 UISound(결과판 등)와 같은 클립을 꽂는다
            if (panel.GetComponent<UISound>() == null)
            {
                UISound uiSound = Undo.AddComponent<UISound>(panel.gameObject);
                UISound source = Object.FindObjectsByType<UISound>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(other => other != uiSound);
                if (source != null)
                    EditorUtility.CopySerialized(source, uiSound);
                else
                    Debug.Log("[WantedPosterLayout] 씬에 다른 UISound 가 없어서 버튼 소리 클립을 비워 둡니다. Panel 의 UISound 에 클립을 꽂아 주세요.");
            }

            // ⑦ PauseMenu 의 칸에 연결한다
            XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null)
                throw new Exception("[WantedPosterLayout] 씬에 XR Origin(카메라 포함)이 없습니다.");

            HolsterAnchor anchor = Object.FindFirstObjectByType<HolsterAnchor>();
            if (anchor == null)
                throw new Exception("[WantedPosterLayout] 씬에 HolsterAnchor 가 없습니다.");

            SerializedObject menuData = new SerializedObject(menu);
            SetField(menuData, "head", origin.Camera.transform);
            SetField(menuData, "holsterAnchor", anchor);
            SetField(menuData, "panel", panel.gameObject);

            SetField(menuData, "homePage", homePage.gameObject);
            SetField(menuData, "resumeButton", resumeButton);
            SetField(menuData, "holsterMenuButton", holsterMenuButton);
            SetField(menuData, "volumeMenuButton", volumeMenuButton);
            SetField(menuData, "mainMenuButton", mainMenuButton);

            SetField(menuData, "holsterPage", holsterPage.gameObject);
            SetHolsterFields(menuData, holster);
            SetField(menuData, "quickButton", holster.Quick);
            SetField(menuData, "holsterBackButton", holsterBackButton);

            SetField(menuData, "handPage", quick.HandPage.gameObject);
            SetField(menuData, "leftHandButton", quick.LeftHand);
            SetField(menuData, "rightHandButton", quick.RightHand);
            SetField(menuData, "handBackButton", handBackButton);
            SetField(menuData, "followPage", quick.FollowPage.gameObject);
            SetField(menuData, "followText", quick.FollowText);

            //    빠른 조정 : 컨트롤러 (XR Origin → Camera Offset 아래) · 트리거 (XRI 입력 파일의 Activate). 메인화면 판과 같다
            Transform cameraOffset = origin.CameraFloorOffsetObject.transform;
            SetField(menuData, "leftController", FindController(cameraOffset, "Left Controller"));
            SetField(menuData, "rightController", FindController(cameraOffset, "Right Controller"));
            SetField(menuData, "leftTrigger", LoadInputAction("XRI Left Interaction", "Activate"));
            SetField(menuData, "rightTrigger", LoadInputAction("XRI Right Interaction", "Activate"));

            SetField(menuData, "volumePage", volumePage.gameObject);
            SetField(menuData, "volumeBackButton", volumeBackButton);
            menuData.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            Debug.Log("[WantedPosterLayout] 일시 정지 판을 정렬했습니다. Ctrl+S 로 씬을 저장해 주세요.");
        }

        // 일시 정지 첫 화면 쪽지 버튼. index : 위에서 몇 번째 (0 부터)
        private static Button PlacePauseButton(RectTransform page, TMP_FontAsset font, Sprite notePaper, Sprite nail, string name, string content, Color textColor, int index, float angle, Vector2 pin)
        {
            Button button = PlaceNoteButton(page, font, name, notePaper, nail, content, textColor,
                0, PauseButtonTop + PauseButtonGap * index, PauseButtonWidth, PauseButtonHeight, angle, pin.x, pin.y);
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = TextHeading;
            return button;
        }

        // 일시 정지 하위 화면(홀스터 조정 · 음량 조절)의 '뒤로' 쪽지 : 종이 아래 가운데
        private static Button PlacePauseBackButton(RectTransform page, TMP_FontAsset font, Sprite notePaper, Sprite nail, string name)
        {
            Button button = PlaceNoteButton(page, font, name, notePaper, nail, "뒤로", InkBrown, 0, PauseBackTop, 240, 84, -1.5f, 0.3f, 8.6f);
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = TextButton;
            return button;
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

        // ---------------------------------------------------------------- 게시판 몸통 (메인화면, 3D)

        // 판자 그림 한 장(납작한 판)을 나무 덩어리로 보이게 한다 (메인화면 게시판 · 결과판이 같이 쓴다)
        //   width · height : 판자 그림 크기 (캔버스 칸)
        //   ① 뒤판 : 판자 그림 바로 뒤의 두꺼운 큐브
        //   ② 틀   : 판자 그림 둘레를 감싸고 앞으로 튀어나온 큐브 넷 (위 · 아래 · 왼쪽 · 오른쪽)
        //   큐브는 Panel 의 자식이다 → 자리 · 크기를 캔버스 칸(1칸 = 1mm) 으로 적는다 (Panel 크기가 0.001 이라서)
        //   캔버스 앞면은 -Z 쪽(플레이어 쪽)을 본다 → +Z 가 뒤, -Z 가 앞
        private static void PlaceBoardModel(RectTransform panel, float width, float height)
        {
            Texture2D grain = WantedPosterArt.CreateWoodGrain("WoodGrain.png");
            Material frameMaterial = GetOrCreateMaterial("BoardFrame.mat", grain, Color.white);
            Material backMaterial = GetOrCreateMaterial("BoardBack.mat", grain, new Color(0.6f, 0.6f, 0.6f, 1f));   // 뒤판은 그늘져서 더 어둡게

            // 판자 그림의 가운데 (Panel 의 Pivot 이 가운데가 아닐 수도 있어서 계산한다)
            //   Pivot (0.5, 0.5) 이면 (0, 0). Pivot 이 아래쪽(0.5, 0)이면 가운데는 위로 높이의 절반
            float centerX = (0.5f - panel.pivot.x) * width;
            float centerY = (0.5f - panel.pivot.y) * height;

            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float frameCenterZ = FrameBackZ - FrameDepth * 0.5f;   // 틀 뒷면이 뒤판 안에 묻히게

            // ① 뒤판 : 앞면이 판자 그림 2mm 뒤. 틀보다 5mm 작게 해서 틀 바깥 면과 겹쳐 깜빡이지 않게
            PlaceBlock(panel, "BoardBack", backMaterial,
                new Vector3(centerX, centerY, BackFrontZ + BackDepth * 0.5f),
                new Vector3(width + FrameWidth * 2f - 10f, height + FrameWidth * 2f - 10f, BackDepth), 0f);

            // ② 틀 : 위 · 아래는 모서리까지 길게, 왼쪽 · 오른쪽은 그 사이에 끼운다
            //    세로 막대는 가로 막대를 90도 돌려서 쓴다 → 나뭇결이 늘 막대 길이 방향으로 흐른다
            float topBottomLength = width + FrameWidth * 2f;
            PlaceBlock(panel, "FrameTop", frameMaterial,
                new Vector3(centerX, centerY + halfHeight + FrameWidth * 0.5f, frameCenterZ),
                new Vector3(topBottomLength, FrameWidth, FrameDepth), 0f);
            PlaceBlock(panel, "FrameBottom", frameMaterial,
                new Vector3(centerX, centerY - halfHeight - FrameWidth * 0.5f, frameCenterZ),
                new Vector3(topBottomLength, FrameWidth, FrameDepth), 0f);
            PlaceBlock(panel, "FrameLeft", frameMaterial,
                new Vector3(centerX - halfWidth - FrameWidth * 0.5f, centerY, frameCenterZ),
                new Vector3(height, FrameWidth, FrameDepth), 90f);
            PlaceBlock(panel, "FrameRight", frameMaterial,
                new Vector3(centerX + halfWidth + FrameWidth * 0.5f, centerY, frameCenterZ),
                new Vector3(height, FrameWidth, FrameDepth), 90f);
        }

        // 큐브 하나. size 는 (길이, 폭, 두께), angleZ 는 앞에서 볼 때 돌린 각도
        private static void PlaceBlock(Transform parent, string name, Material material, Vector3 center, Vector3 size, float angleZ)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(block, UndoName);
            block.name = name;
            block.layer = parent.gameObject.layer;

            // 충돌체는 뗀다. 튀어나온 틀이 레이를 가로채면 가장자리 쪽지 버튼이 안 눌린다
            Object.DestroyImmediate(block.GetComponent<BoxCollider>());

            block.transform.SetParent(parent, false);
            block.transform.localPosition = center;
            block.transform.localRotation = Quaternion.Euler(0f, 0f, angleZ);
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // Assets/30.UI 의 재질. 없으면 만들고, 있으면 그림 · 색만 고친다 (씬의 연결이 끊기지 않게 같은 파일을 쓴다)
        private static Material GetOrCreateMaterial(string fileName, Texture2D texture, Color tint)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new Exception("[WantedPosterLayout] URP Lit 셰이더를 찾지 못했습니다. 프로젝트가 URP 인지 확인해 주세요.");

            string path = "Assets/30.UI/" + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);   // URP Lit 의 바탕 그림 칸
            material.SetColor("_BaseColor", tint);      // 그림에 곱하는 색
            material.SetFloat("_Smoothness", 0.15f);    // 거의 안 반짝이게 (낡은 나무)
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- 게시판에 종이 붙이기 (메인화면)

        // 종이 그림자 : 같은 그림을 반투명 검정으로 오른쪽 아래에 비켜 깐다 → 종이가 게시판에서 살짝 떠 보인다
        //   그림자는 종이보다 "먼저" 만들어야 종이 밑에 깔린다
        private static void PlaceShadow(RectTransform parent, string name, Sprite sprite, float x, float top, float width, float height, float angle)
        {
            Image shadow = GetImage(parent, name);
            PlaceFromTopAt(shadow.rectTransform, x + 6f, top + 8f, width, height);
            shadow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            shadow.sprite = sprite;
            shadow.type = Image.Type.Simple;
            if (sprite.border != Vector4.zero)
                shadow.type = Image.Type.Sliced;   // 나무 간판처럼 양 끝을 안 늘리는 그림은 그림자도 같게
            shadow.color = PaperShadow;
            shadow.raycastTarget = false;
        }

        // 게시판에 붙인 종이 한 장 (그림자 + 종이). angle : 기울기 (도). 기준점이 위쪽 가운데라서 핀 자리를 중심으로 돈다
        private static Image PlacePaper(RectTransform parent, string name, Sprite sprite, float x, float top, float width, float height, float angle)
        {
            PlaceShadow(parent, name + "Shadow", sprite, x, top, width, height, angle);

            Image paper = GetImage(parent, name);
            PlaceFromTopAt(paper.rectTransform, x, top, width, height);
            paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);   // PlaceFromTopAt 이 바로 세우므로 그 뒤에 기울인다
            paper.sprite = sprite;
            paper.type = Image.Type.Simple;
            paper.color = Color.white;
            paper.raycastTarget = false;
            return paper;
        }

        // 작은 수배지 안 : WANTED · 몽타주 · 이름 · 현상금 · 핀. 자리는 수배지 위쪽 가운데 기준
        //   구경용 수배지(첫 화면)와 고르는 수배지(Toggle)가 같이 쓴다
        //   (난이도 줄은 씬에서 지웠다. 수배지에 "난이도" 는 게임 말이라 어울리지 않는다)
        //   index : 0 쉬움, 1 보통, 2 어려움 → PosterPin 의 칸
        private static void FillPoster(RectTransform poster, TMP_FontAsset font, Sprite nail, Sprite portrait, EnemyDef enemy, int index)
        {
            TextMeshProUGUI wanted = GetText(poster, "Wanted");
            PlaceFromTop(wanted.rectTransform, 24, 170, 36);
            SetText(wanted, font, "WANTED", 34, InkRed, FontStyles.Bold);
            wanted.characterSpacing = 4;
            wanted.enableAutoSizing = true;
            wanted.fontSizeMin = 20;
            wanted.fontSizeMax = 34;

            Image montage = GetImage(poster, "Montage");
            PlaceFromTop(montage.rectTransform, 62, 130, 130);
            montage.sprite = portrait;
            montage.type = Image.Type.Simple;
            montage.color = Color.white;
            montage.raycastTarget = false;

            TextMeshProUGUI outlawName = GetText(poster, "Name");
            PlaceFromTop(outlawName.rectTransform, 196, 170, 30);
            SetText(outlawName, font, enemy.Name, 24, InkRed, FontStyles.Bold);
            outlawName.enableAutoSizing = true;   // 이름이 길면 줄인다
            outlawName.fontSizeMin = 14;
            outlawName.fontSizeMax = 24;

            TextMeshProUGUI reward = GetText(poster, "Reward");
            PlaceFromTop(reward.rectTransform, 226, 170, 24);
            SetText(reward, font, $"$ {enemy.BaseScore:N0}", 22, InkBrown, FontStyles.Bold);

            // 수배지를 게시판에 박은 못 (맨 나중 = 맨 위에 그린다). 자리는 씬에서 손으로 박은 곳
            PlaceDecoration(poster, "Pin", nail, PosterPin[index].x, PosterPin[index].y, 24, 48);
        }

        // 구경용 수배지 (첫 화면). index : 0 쉬움, 1 보통, 2 어려움 → PosterX · PosterTop · PosterAngle · PosterPin 의 칸
        private static void PlacePosterImage(RectTransform parent, TMP_FontAsset font, string name, Sprite paperSprite, Sprite nail, Sprite portrait, EnemyDef enemy, int index)
        {
            Image poster = PlacePaper(parent, name, paperSprite, PosterX[index], PosterTop[index], PosterWidth, PosterHeight, PosterAngle[index]);
            FillPoster(poster.rectTransform, font, nail, portrait, enemy, index);
        }

        // 고르는 수배지 (게임 시작 화면의 난이도 Toggle). index : 0 쉬움, 1 보통, 2 어려움 → PosterX · PosterTop · PosterAngle · PosterPin 의 칸
        //   켜지면 몽타주에 빨간 동그라미(Toggle 의 Graphic)가 보인다
        private static Toggle PlacePosterToggle(RectTransform parent, ToggleGroup group, TMP_FontAsset font, string name, Sprite paperSprite, Sprite nail, Sprite circle, Sprite portrait, EnemyDef enemy, int index)
        {
            PlaceShadow(parent, name + "Shadow", paperSprite, PosterX[index], PosterTop[index], PosterWidth, PosterHeight, PosterAngle[index]);

            Toggle toggle = GetToggle(parent, name);
            RectTransform rect = (RectTransform)toggle.transform;
            PlaceFromTopAt(rect, PosterX[index], PosterTop[index], PosterWidth, PosterHeight);
            rect.localRotation = Quaternion.Euler(0f, 0f, PosterAngle[index]);

            // ① 바탕 = 수배지 종이. 레이가 올라가면 밝아진다
            Image background = (Image)toggle.targetGraphic;
            background.sprite = paperSprite;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.raycastTarget = true;     // 레이에 맞아야 눌린다
            SetPaperColors(toggle);

            // ② 수배지 내용
            FillPoster(rect, font, nail, portrait, enemy, index);

            // ③ 켜짐 표시 = 몽타주를 감싼 빨간 동그라미. 내용보다 나중에 만들어야 위에 그려진다
            Image check = GetImage(rect, "Checked");
            PlaceFromTop(check.rectTransform, 46, 162, 162);   // 몽타주(62 ~ 192) 가운데에 맞췄다. 아래 이름은 덮지 않게
            check.sprite = circle;
            check.type = Image.Type.Simple;
            check.color = CircleRed;
            check.raycastTarget = false;         // 레이는 종이가 받는다
            toggle.graphic = check;
            toggle.toggleTransition = Toggle.ToggleTransition.None;   // 서서히 말고 바로 바뀌게

            // ④ 같은 묶음에 넣는다 → 셋 중 하나만 켜진다
            toggle.group = group;
            return toggle;
        }

        // 누르는 쪽지 (결투 신청 · 결투 시작 · 뒤로 ...) : 종이 + 글자 + 맨 위 핀
        //   pinX · pinTop : 쪽지 위쪽 가운데 기준 못 자리. 기본은 종이 위로 반쯤 튀어나온 자리 (메인화면은 씬에서 손으로 박은 자리를 넘긴다)
        private static Button PlaceNoteButton(RectTransform parent, TMP_FontAsset font, string name, Sprite paperSprite, Sprite nail, string content, Color textColor, float x, float top, float width, float height, float angle, float pinX = 0f, float pinTop = -16f)
        {
            PlaceShadow(parent, name + "Shadow", paperSprite, x, top, width, height, angle);

            Button button = GetButton(parent, name);
            RectTransform rect = (RectTransform)button.transform;
            PlaceFromTopAt(rect, x, top, width, height);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            Image image = button.GetComponent<Image>();
            image.sprite = paperSprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = true;          // 버튼은 레이에 맞아야 눌린다
            SetPaperColors(button);

            TextMeshProUGUI label = GetText(rect, "Label");
            Stretch(label.rectTransform);
            SetText(label, font, content, 38, textColor, FontStyles.Bold);
            label.characterSpacing = 2;

            PlaceDecoration(rect, "Pin", nail, pinX, pinTop, 24, 48);
            return button;
        }

        // 게시판 아래쪽 공고 한 장 (글자 몇 줄). 양쪽에 핀 둘
        //   pinLeft · pinRight : 공고 위쪽 가운데 기준 (x, 위에서 아래로). 씬에서 손으로 박은 자리를 화면마다 넘긴다
        private static void PlaceNotice(RectTransform parent, TMP_FontAsset font, string name, Sprite paperSprite, Sprite nail, string content, Vector2 pinLeft, Vector2 pinRight)
        {
            Image notice = PlacePaper(parent, name, paperSprite, NoticeX, NoticeTop, NoticeWidth, NoticeHeight, -1f);

            TextMeshProUGUI text = GetText(notice.rectTransform, "Text");
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(28f, 20f);    // 종이 테두리 안쪽으로 (왼쪽, 아래)
            text.rectTransform.offsetMax = new Vector2(-28f, -20f);  // (오른쪽, 위)
            SetText(text, font, content, 26, InkBrown, FontStyles.Normal);
            text.lineSpacing = 4;
            text.enableAutoSizing = true;        // 줄이 길면 칸에 맞게 줄인다
            text.fontSizeMin = 16;
            text.fontSizeMax = 26;

            PlaceDecoration(notice.rectTransform, "PinLeft", nail, pinLeft.x, pinLeft.y, 24, 48);
            PlaceDecoration(notice.rectTransform, "PinRight", nail, pinRight.x, pinRight.y, 24, 48);
        }

        // 내용이 많은 화면(홀스터 조정 · 빠른 조정 · 음량 조절)을 담는 큰 종이 한 장. 기울이지 않는다 (위에 버튼이 줄지어 있어서)
        //   돌려주는 RectTransform 에 자식을 놓으면 자리가 종이 위쪽 가운데 기준이 된다
        //   pinLeft · pinRight : 종이 위쪽 가운데 기준 (x, 위에서 아래로). 씬에서 손으로 박은 자리를 화면마다 넘긴다
        private static RectTransform PlaceSheet(RectTransform parent, string name, Sprite paperSprite, Sprite nail, Vector2 pinLeft, Vector2 pinRight)
        {
            return PlaceSheet(parent, name, paperSprite, nail, pinLeft, pinRight, MainSheet);
        }

        // box : 종이 자리 · 크기. 못 자리(pinLeft · pinRight)는 메인화면 종이(폭 700) 기준이라 폭에 맞춰 가로로 늘린다
        private static RectTransform PlaceSheet(RectTransform parent, string name, Sprite paperSprite, Sprite nail, Vector2 pinLeft, Vector2 pinRight, SheetBox box)
        {
            float stretch = box.Width / SheetWidth;
            Image sheet = PlacePaper(parent, name, paperSprite, box.X, box.Top, box.Width, box.Height, 0f);
            PlaceDecoration(sheet.rectTransform, "PinLeft", nail, pinLeft.x * stretch, pinLeft.y, 24, 48);
            PlaceDecoration(sheet.rectTransform, "PinRight", nail, pinRight.x * stretch, pinRight.y, 24, 48);
            return sheet.rectTransform;
        }

        // 누르는 종이 색 (평소 · 가리킴 · 누름). 쪽지 버튼과 수배지 Toggle 이 같이 쓴다
        private static void SetPaperColors(Selectable selectable)
        {
            selectable.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = selectable.colors;
            colors.normalColor = PaperNormal;
            colors.highlightedColor = Color.white;
            colors.pressedColor = PaperPressed;
            colors.selectedColor = PaperNormal;   // 누른 뒤 "선택됨" 으로 밝은 채 남지 않게
            selectable.colors = colors;
        }

        // 큰 종이 안의 줄 이름표 (홀스터 : 높이 / 앞뒤 / 좌우, 음량 : 전체 / 배경음악 ...). 긴 이름은 칸에 맞게 줄인다
        private static void PlaceRowLabel(RectTransform parent, TMP_FontAsset font, string name, string content, float x, float top, float width, float height)
        {
            TextMeshProUGUI label = GetText(parent, name);
            PlaceFromTopAt(label.rectTransform, x, top, width, height);
            SetText(label, font, content, TextButton, InkBrown, FontStyles.Bold);
            label.enableAutoSizing = true;
            label.fontSizeMin = 24;
            label.fontSizeMax = TextButton;
        }

        // 큰 종이 안의 작은 나무 간판 버튼 (홀스터 조정)
        private static Button PlaceRowButton(RectTransform parent, TMP_FontAsset font, string name, string content, float x, float top, float width)
        {
            Button button = GetButton(parent, name);
            PlaceFromTopAt((RectTransform)button.transform, x, top, width, RowHeight);
            ArrangeButton(button, font, content, TextButton);
            return button;
        }

        // 큰 종이 맨 위 제목 · 맨 아래 안내 (홀스터 조정 · 음량 조절이 같은 자리 · 같은 크기)
        private static void PlaceSheetHeading(RectTransform sheet, TMP_FontAsset font, string name, string content, SheetBox box)
        {
            TextMeshProUGUI heading = GetText(sheet, name);
            PlaceFromTop(heading.rectTransform, 30, box.Width - 120, 54);
            SetText(heading, font, content, TextHeading, InkRed, FontStyles.Bold);
        }

        private static void PlaceSheetHint(RectTransform sheet, TMP_FontAsset font, string name, string content, SheetBox box)
        {
            TextMeshProUGUI hint = GetText(sheet, name);
            PlaceFromTop(hint.rectTransform, box.Height - 52, box.Width - 100, 34);
            SetText(hint, font, content, TextHint, InkBrown, FontStyles.Normal);
            hint.enableAutoSizing = true;   // 종이가 좁으면 칸에 맞게 줄인다
            hint.fontSizeMin = 18;
            hint.fontSizeMax = TextHint;
        }

        // ---------------------------------------------------------------- 홀스터 조정 · 음량 조절 종이 (메인화면 판 · 일시 정지 판이 같이 쓴다)

        // 홀스터 조정 종이에서 메뉴 스크립트에 꽂을 것들
        private struct HolsterSheet
        {
            public TextMeshProUGUI Text;
            public Button Up, Down, Forward, Backward, Left, Right, Reset;
            public Button Quick;   // withQuick 이 false 면 null
        }

        // [홀스터 조정 화면] 게시판에 붙인 큰 종이 한 장(Sheet) 위에 조정 버튼들. 뒤로 쪽지는 부르는 쪽이 따로 단다
        //   방향 버튼 이름은 '~쪽' 으로 맞췄다. '뒤로' 는 화면을 돌아가는 버튼이라 헷갈리지 않게
        //   좌우는 '왼쪽 · 오른쪽' 이다. 왼손잡이는 홀스터가 왼쪽 허리라서 '안쪽 · 바깥쪽' 이 손마다 반대가 된다
        //   withQuick : '빠른 조정' 버튼을 둔다 (메인화면만. 일시 정지 판은 '처음 자리' 하나를 가운데에)
        //   Sheet 안의 자리는 Sheet 위쪽 가운데 기준이다
        //   배치 : 이름표 | 버튼 | 버튼 세 칸을 한 덩어리로 묶어 종이 가운데에 둔다. 버튼 폭은 종이 폭에 맞춰 정한다 (최대 220)
        //          줄 사이 · 처음 자리 · 안내는 종이 높이에 맞춰 내려간다
        private static HolsterSheet PlaceHolsterSheet(RectTransform page, TMP_FontAsset font, Sprite sheetPaper, Sprite nail, bool withQuick, SheetBox box)
        {
            RectTransform sheet = PlaceSheet(page, "Sheet", sheetPaper, nail, new Vector2(-298.7f, 38.1f), new Vector2(287f, 40f), box);
            HolsterSheet result = new HolsterSheet();

            PlaceSheetHeading(sheet, font, "HolsterLabel", "홀스터 위치", box);

            //    글자는 편집할 때 보이는 예시다. Play 하면 메뉴 스크립트가 저장된 자리로 바꾼다
            result.Text = GetText(sheet, "HolsterText");
            PlaceFromTop(result.Text.rectTransform, 86, box.Width - 100, 40);
            SetText(result.Text, font, "머리에서 아래 60cm · 오른쪽 20cm · 앞 6cm", TextBody, InkBrown, FontStyles.Normal);
            result.Text.enableAutoSizing = true;   // 숫자가 길어지면 칸에 맞게 줄인다 (SetText 가 끄므로 그 뒤에 켠다)
            result.Text.fontSizeMin = 22;
            result.Text.fontSizeMax = TextBody;

            //    세 칸 : 이름표 160 | 틈 20 | 버튼 | 틈 20 | 버튼
            const float labelWidth = 160f;
            const float gap = 20f;
            float buttonWidth = Mathf.Min(220f, (box.Width - 80f - labelWidth - gap * 2f) * 0.5f);
            float groupLeft = -(labelWidth + gap * 2f + buttonWidth * 2f) * 0.5f;
            float labelX = groupLeft + labelWidth * 0.5f;
            float leftX = groupLeft + labelWidth + gap + buttonWidth * 0.5f;
            float rightX = leftX + buttonWidth + gap;

            //    줄 : 홀스터 자리 글자 아래부터, 안내 위까지 셋 + 처음 자리 한 줄을 고르게
            float rowTop = 136f;
            float rowGap = Mathf.Min(80f, (box.Height - 52f - 12f - rowTop) / 4f);

            PlaceRowLabel(sheet, font, "HeightLabel", "높이", labelX, rowTop, labelWidth, RowHeight);
            result.Up = PlaceRowButton(sheet, font, "UpButton", "위쪽", leftX, rowTop, buttonWidth);
            result.Down = PlaceRowButton(sheet, font, "DownButton", "아래쪽", rightX, rowTop, buttonWidth);

            PlaceRowLabel(sheet, font, "DepthLabel", "앞뒤", labelX, rowTop + rowGap, labelWidth, RowHeight);
            result.Forward = PlaceRowButton(sheet, font, "ForwardButton", "앞쪽", leftX, rowTop + rowGap, buttonWidth);
            result.Backward = PlaceRowButton(sheet, font, "BackwardButton", "뒤쪽", rightX, rowTop + rowGap, buttonWidth);

            PlaceRowLabel(sheet, font, "SideLabel", "좌우", labelX, rowTop + rowGap * 2f, labelWidth, RowHeight);
            result.Left = PlaceRowButton(sheet, font, "LeftButton", "왼쪽", leftX, rowTop + rowGap * 2f, buttonWidth);
            result.Right = PlaceRowButton(sheet, font, "RightButton", "오른쪽", rightX, rowTop + rowGap * 2f, buttonWidth);

            //    처음 자리 (· 빠른 조정) : 버튼 두 칸 자리 아래에. 빠른 조정이 없으면 두 칸을 다 쓴다
            float bottomTop = rowTop + rowGap * 3f;
            result.Reset = GetButton(sheet, "ResetButton");
            if (withQuick)
            {
                PlaceFromTopAt((RectTransform)result.Reset.transform, leftX, bottomTop, buttonWidth, RowHeight);
                result.Quick = GetButton(sheet, "QuickButton");
                PlaceFromTopAt((RectTransform)result.Quick.transform, rightX, bottomTop, buttonWidth, RowHeight);
                ArrangeButton(result.Quick, font, "빠른 조정", TextButton);
            }
            else
            {
                PlaceFromTopAt((RectTransform)result.Reset.transform, (leftX + rightX) * 0.5f, bottomTop, buttonWidth * 2f + gap, RowHeight);
            }
            ArrangeButton(result.Reset, font, "처음 자리", TextButton);

            PlaceSheetHint(sheet, font, "HolsterHint", "버튼을 누르면 총이 홀스터로 돌아옵니다. 뽑아 보며 맞추세요", box);
            return result;
        }

        // 빠른 조정 화면 두 장에서 메뉴 스크립트에 꽂을 것들 (뒤로 쪽지는 부르는 쪽이 따로 단다)
        private struct QuickPages
        {
            public RectTransform HandPage, FollowPage;
            public Button LeftHand, RightHand;
            public TextMeshProUGUI FollowText;
        }

        // [주 손 고르기 화면] 빠른 조정 1단계 · [손 따라가기 화면] 빠른 조정 2단계 (메인화면 판 · 일시 정지 판이 같이 쓴다)
        //   손 따라가기에는 버튼 없이 안내 글자만 둔다
        //   여기에 버튼(취소 등)을 두면, 주 손으로 그 버튼을 누르려는 트리거가 확정이 되어 버린다
        private static QuickPages PlaceQuickPages(RectTransform panel, TMP_FontAsset font, Sprite sheetPaper, Sprite nail, SheetBox box)
        {
            QuickPages result = new QuickPages();
            Vector2 pinLeft = new Vector2(-297.3f, 36.2f);    // 씬에서 손으로 박은 자리 (빠른 조정 1 · 2단계 같은 종이)
            Vector2 pinRight = new Vector2(293.1f, 40.8f);

            // ① 주 손 고르기
            result.HandPage = CreatePage(panel, "HandPage");
            RectTransform handSheet = PlaceSheet(result.HandPage, "Sheet", sheetPaper, nail, pinLeft, pinRight, box);
            PlaceSheetHeading(handSheet, font, "HandLabel", "빠른 조정", box);

            TextMeshProUGUI handText = GetText(handSheet, "HandText");
            PlaceFromTop(handText.rectTransform, 100, box.Width - 100, 90);
            SetText(handText, font, "주로 쓰는 손을 고르세요\n홀스터가 그 손을 따라갑니다", TextBody, InkBrown, FontStyles.Normal);
            handText.lineSpacing = 10;

            //    큰 나무 간판 둘 : 종이 높이 가운데쯤
            float handTop = Mathf.Round((box.Height - 110f) * 0.5f + 50f);
            result.LeftHand = GetButton(handSheet, "LeftHandButton");
            PlaceFromTopAt((RectTransform)result.LeftHand.transform, -150, handTop, 260, 110);
            ArrangeButton(result.LeftHand, font, "왼손잡이", TextHeading);

            result.RightHand = GetButton(handSheet, "RightHandButton");
            PlaceFromTopAt((RectTransform)result.RightHand.transform, 150, handTop, 260, 110);
            ArrangeButton(result.RightHand, font, "오른손잡이", TextHeading);

            // ② 손 따라가기
            result.FollowPage = CreatePage(panel, "FollowPage");
            RectTransform followSheet = PlaceSheet(result.FollowPage, "Sheet", sheetPaper, nail, pinLeft, pinRight, box);
            PlaceSheetHeading(followSheet, font, "FollowLabel", "빠른 조정", box);

            //    글자는 편집할 때 보이는 예시다. Play 하면 메뉴 스크립트가 고른 손에 맞게 바꾼다
            result.FollowText = GetText(followSheet, "FollowText");
            PlaceFromTop(result.FollowText.rectTransform, 110, box.Width - 100, 160);
            SetText(result.FollowText, font, "오른손을 홀스터를 둘 자리에 대고\n오른손 트리거를 누르세요", TextHeading, InkRed, FontStyles.Bold);
            result.FollowText.lineSpacing = 20;
            result.FollowText.enableAutoSizing = true;   // 한 줄이 칸보다 길면 줄인다 (SetText 가 끄므로 그 뒤에 켠다)
            result.FollowText.fontSizeMin = 28;
            result.FollowText.fontSizeMax = TextHeading;

            TextMeshProUGUI followHint = GetText(followSheet, "FollowHint");
            PlaceFromTop(followHint.rectTransform, box.Height - 140f, box.Width - 100, 80);
            SetText(followHint, font, "홀스터가 손을 따라옵니다\n확정한 뒤 버튼으로 더 다듬을 수 있습니다", TextBody, InkBrown, FontStyles.Normal);
            followHint.lineSpacing = 10;

            result.HandPage.gameObject.SetActive(false);
            result.FollowPage.gameObject.SetActive(false);
            return result;
        }

        // 홀스터 조정 종이의 버튼 · 글자를 메뉴 스크립트 칸에 꽂는다 (MainMenu · PauseMenu 의 칸 이름이 같다)
        private static void SetHolsterFields(SerializedObject data, HolsterSheet holster)
        {
            SetField(data, "holsterText", holster.Text);
            SetField(data, "upButton", holster.Up);
            SetField(data, "downButton", holster.Down);
            SetField(data, "forwardButton", holster.Forward);
            SetField(data, "backwardButton", holster.Backward);
            SetField(data, "leftButton", holster.Left);
            SetField(data, "rightButton", holster.Right);
            SetField(data, "resetButton", holster.Reset);
        }

        // [음량 조절 화면] 홀스터 조정처럼 큰 종이 한 장 위에 줄 넷 (이름표 | 탄띠 슬라이더 | %). 뒤로 쪽지는 부르는 쪽이 따로 단다
        //   슬라이더는 VolumeSlider 가 스스로 SoundManager 와 이어서, 메뉴 스크립트에 꽂을 것이 없다
        //   배치 : 이름표 | 탄띠 | % 세 칸을 한 덩어리로 묶어 종이 가운데에 둔다
        //          탄띠 폭은 종이 폭에 맞춰 정하고(최대 VolumeBeltMax), 쉘 · 고리 · 탄띠 높이는 탄띠 폭에 비례해 커진다
        //          네 줄은 제목 아래 ~ 안내 위 사이에 고르게
        private static void PlaceVolumeSheet(RectTransform page, TMP_FontAsset font, Sprite sheetPaper, Sprite nail, Sprite belt, Sprite shell, Sprite strap, SheetBox box)
        {
            RectTransform sheet = PlaceSheet(page, "Sheet", sheetPaper, nail, new Vector2(-298.7f, 38.1f), new Vector2(287f, 40f), box);   // 홀스터 조정 종이와 같은 자리

            PlaceSheetHeading(sheet, font, "VolumeLabel", "음량", box);

            //    세 칸 : 이름표 150 | 틈 20 | 탄띠 | 틈 20 | % 110
            const float labelWidth = 150f;
            const float valueWidth = 110f;
            const float gap = 20f;
            float beltWidth = Mathf.Min(VolumeBeltMax, box.Width - 80f - labelWidth - valueWidth - gap * 2f);
            float groupLeft = -(labelWidth + gap * 2f + beltWidth + valueWidth) * 0.5f;
            VolumeRowLayout layout = new VolumeRowLayout
            {
                LabelX = groupLeft + labelWidth * 0.5f,
                LabelWidth = labelWidth,
                BeltX = groupLeft + labelWidth + gap + beltWidth * 0.5f,
                BeltWidth = beltWidth,
                ValueX = groupLeft + labelWidth + gap * 2f + beltWidth + valueWidth * 0.5f,
                ValueWidth = valueWidth,
            };

            //    줄 높이 = 탄띠 줄(쉘이 위아래로 삐져나온 높이). 네 줄을 제목(84) 아래 ~ 안내 위에 고르게
            float rowHeight = layout.SliderHeight;
            float firstTop = 96f;
            float rowGap = (box.Height - 52f - 16f - firstTop - rowHeight) / 3f;

            PlaceVolumeRow(sheet, font, belt, shell, strap, layout, "Master", "전체", SoundChannel.Master, firstTop);
            PlaceVolumeRow(sheet, font, belt, shell, strap, layout, "Bgm", "배경음악", SoundChannel.Bgm, firstTop + rowGap);
            PlaceVolumeRow(sheet, font, belt, shell, strap, layout, "Sfx", "효과음", SoundChannel.Sfx, firstTop + rowGap * 2f);
            PlaceVolumeRow(sheet, font, belt, shell, strap, layout, "Ambience", "환경음", SoundChannel.Ambience, firstTop + rowGap * 3f);

            PlaceSheetHint(sheet, font, "VolumeHint", "트리거를 누른 채 탄띠를 따라 끌어 맞추세요 · 효과음을 끄면 정오의 종도 꺼짐", box);
        }

        // 음량 줄의 가로 자리 (종이 위쪽 가운데 기준 x) · 폭. 탄띠 폭이 정해지면 쉘 · 고리 크기도 따라 정해진다
        //   크기의 기준 : 탄띠 300 칸일 때 쉘 20 × 46, 탄띠 높이 40, 줄(슬라이더) 높이 48 → 탄띠 폭에 비례해 키운다
        private struct VolumeRowLayout
        {
            public float LabelX, LabelWidth, BeltX, BeltWidth, ValueX, ValueWidth;
            public float Scale => BeltWidth / BeltWidthBase;
            public float SliderHeight => 48f * Scale;
        }

        // 음량 조절 줄 하나 : 이름표 | 탄띠 슬라이더 | 퍼센트. 슬라이더에 VolumeSlider 를 붙여 갈래를 정한다
        //   Sheet 위쪽 가운데 기준. 이름표는 홀스터 조정 줄과 같은 자리
        //   탄띠 : 가죽 띠 위 고리 10개에 샷건 쉘이 음량만큼 꽂힌다. 쉘을 켜고 끄는 건 VolumeSlider 가 한다
        //   그리는 순서 (Hierarchy 위 → 아래 = 뒤 → 앞) : 탄띠 → 쉘 10발 → 가죽 고리 10개
        //     고리는 쉘과 따로라서 쉘이 빠진 칸에도 남는다 (탄띠의 일부). 쉘이 꽂히면 고리 아래로 지나가 꽂혀 보인다
        //   손잡이는 없다. 탄띠 어디를 가리키든 그 칸까지 쉘이 채워진다 (끌면 따라 늘고 준다)
        private static void PlaceVolumeRow(RectTransform sheet, TMP_FontAsset font, Sprite belt, Sprite shell, Sprite strap, VolumeRowLayout layout, string key, string content, SoundChannel channel, float top)
        {
            float scale = layout.Scale;
            float rowHeight = layout.SliderHeight;
            float beltWidth = layout.BeltWidth;

            // ① 이름표 (배경음악처럼 긴 이름은 칸에 맞게 줄인다). 탄띠 줄과 같은 높이로 세로 가운데 맞춤
            PlaceRowLabel(sheet, font, key + "Label", content, layout.LabelX, top, layout.LabelWidth, rowHeight);

            // ② 슬라이더 = 탄띠 줄
            GameObject sliderObject = GetOrCreateChild(sheet, key + "Slider");
            RectTransform sliderRect = (RectTransform)sliderObject.transform;
            PlaceFromTopAt(sliderRect, layout.BeltX, top, beltWidth, rowHeight);

            //    가죽 탄띠 : 줄 가운데에. 레이가 여기에 닿으면 그 칸까지 쉘이 찬다
            Image beltImage = GetImage(sliderRect, "Belt");
            PlaceCenter(beltImage.rectTransform, 0, 0, beltWidth, 40f * scale);
            beltImage.sprite = belt;
            beltImage.type = Image.Type.Simple;
            beltImage.color = Color.white;
            beltImage.raycastTarget = true;

            //    채움 칸 : 그림 없이 자리만 (Slider 가 "어디를 가리켰나 → 몇 %" 를 이 칸 폭으로 계산한다)
            RectTransform fillArea = (RectTransform)GetOrCreateChild(sliderRect, "Fill Area").transform;
            Stretch(fillArea);
            RectTransform fill = (RectTransform)GetOrCreateChild(fillArea, "Fill").transform;
            Stretch(fill);

            //    쉘 10발 : 고리 가운데마다 하나씩. 탄띠보다 조금 길어서 위아래로 살짝 삐져나온다
            RectTransform shellRow = (RectTransform)GetOrCreateChild(sliderRect, "Shells").transform;
            Stretch(shellRow);
            GameObject[] shells = new GameObject[WantedPosterArt.BeltLoops];
            float pitch = beltWidth / WantedPosterArt.BeltLoops;
            for (int i = 0; i < shells.Length; i++)
            {
                Image shellImage = GetImage(shellRow, $"Shell{i + 1}");
                PlaceCenter(shellImage.rectTransform, -beltWidth * 0.5f + pitch * (i + 0.5f), 0, 20f * scale, 46f * scale);
                shellImage.sprite = shell;
                shellImage.type = Image.Type.Simple;
                shellImage.color = Color.white;
                shellImage.raycastTarget = false;   // 레이는 탄띠가 받는다
                shells[i] = shellImage.gameObject;
            }

            //    가죽 고리 10개 : 쉘보다 나중에 만들어 쉘 위에 그린다. 늘 켜져 있다 (VolumeSlider 가 건드리지 않는다)
            //    그림 2점 = 1칸. 띠의 가운데 줄은 그림 가운데보다 StrapAnchorOffset 점 위에 있다
            //    → 그림을 그만큼 내려서 띠 가운데 줄이 쉘 가운데(줄 높이 0)에 오게 한다
            RectTransform strapRow = (RectTransform)GetOrCreateChild(sliderRect, "Straps").transform;
            Stretch(strapRow);
            for (int i = 0; i < WantedPosterArt.BeltLoops; i++)
            {
                Image strapImage = GetImage(strapRow, $"Strap{i + 1}");
                PlaceCenter(strapImage.rectTransform, -beltWidth * 0.5f + pitch * (i + 0.5f), -WantedPosterArt.StrapAnchorOffset * 0.5f * scale,
                    WantedPosterArt.StrapWidth * 0.5f * scale, WantedPosterArt.StrapHeight * 0.5f * scale);
                strapImage.sprite = strap;
                strapImage.type = Image.Type.Simple;
                strapImage.color = Color.white;
                strapImage.raycastTarget = false;   // 레이는 탄띠가 받는다
            }

            Slider slider = sliderObject.GetComponent<Slider>();
            if (slider == null)
                slider = Undo.AddComponent<Slider>(sliderObject);
            slider.fillRect = fill;
            slider.handleRect = null;                // 손잡이 없음 : 가리킨 칸까지 쉘이 찬다
            slider.targetGraphic = beltImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;                     // 편집할 때 보이는 값 (10발). Play 하면 VolumeSlider 가 저장된 음량으로 바꾼다
            SetPaperColors(slider);                // 가리키면 탄띠가 밝아진다
            Navigation navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;   // 컨트롤러 스틱 입력으로 슬라이더가 움직이지 않게
            slider.navigation = navigation;

            // ③ 퍼센트
            TextMeshProUGUI value = GetText(sheet, key + "Value");
            PlaceFromTopAt(value.rectTransform, layout.ValueX, top, layout.ValueWidth, rowHeight);
            SetText(value, font, "100%", TextButton, InkBrown, FontStyles.Bold);

            // ④ VolumeSlider : 갈래 · 퍼센트 글자 칸을 채운다
            VolumeSlider volumeSlider = sliderObject.GetComponent<VolumeSlider>();
            if (volumeSlider == null)
                volumeSlider = Undo.AddComponent<VolumeSlider>(sliderObject);
            SerializedObject sliderData = new SerializedObject(volumeSlider);
            SerializedProperty channelField = sliderData.FindProperty("channel");
            if (channelField == null)
                throw new Exception("[WantedPosterLayout] VolumeSlider 에 channel 칸이 없습니다. 필드 이름이 바뀌었으면 여기도 고쳐 주세요.");
            channelField.enumValueIndex = (int)channel;
            SetField(sliderData, "valueText", value);
            SetArrayField(sliderData, "shells", shells);
            sliderData.ApplyModifiedProperties();
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

        // 부모 가운데를 기준으로 놓는다 (x, y : 가운데에서 오른쪽 + , 위쪽 +). 몽타주 위 총알 구멍처럼 부모 안에 놓는 것
        private static void PlaceCenter(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = new Vector3(x, y, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        // 꾸밈 그림 (못 · 종이의 총알 구멍). 누를 것이 아니라 레이를 막지 않는다
        private static void PlaceDecoration(RectTransform parent, string name, Sprite sprite, float x, float top, float width, float height)
        {
            Image image = GetImage(parent, name);
            PlaceFromTopAt(image.rectTransform, x, top, width, height);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        // 버튼 그림. 정렬 메뉴가 맨 처음 ① 재료에서 그려 둔다 (안 그렸으면 순서가 잘못된 것)
        private static Sprite GetWoodSign()
        {
            if (woodSign == null)
                throw new Exception("[WantedPosterLayout] 나무 간판 그림이 없습니다. 정렬 함수의 ① 재료에서 WantedPosterArt.CreateWoodSign 을 먼저 불러 주세요.");
            return woodSign;
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
            image.sprite = GetWoodSign();  // 나뭇결 + 양쪽 못 그림
            image.type = Image.Type.Sliced;   // 가로로 늘려도 양쪽 끝(못)은 찌그러지지 않게
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
            background.sprite = GetWoodSign();
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            background.raycastTarget = true;    // 레이에 맞아야 눌린다
            SetWoodColors(toggle);

            // ② 켜짐 표시 = 바탕을 꽉 덮는 빨갛게 칠한 간판 (Toggle 의 Graphic 칸)
            //    Toggle 은 켜지면 Graphic 을 보이고, 꺼지면 숨긴다 → 고른 난이도만 빨갛게 보인다
            Image check = GetImage(toggleRect, "Checked");
            Stretch(check.rectTransform);
            check.sprite = background.sprite;   // 바탕과 같은 나무 간판 그림
            check.type = Image.Type.Sliced;
            check.color = WoodChecked;          // 나뭇결 × 붉은색 = 빨간 칠을 한 간판 (결은 그대로 보인다)
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
    }
}
