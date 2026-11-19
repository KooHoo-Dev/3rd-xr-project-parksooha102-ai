using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // [에디터 도구] 결과판(ResultBoard)과 라운드 쪽지(RoundBoard)를 수배지 모양으로 정렬한다.
    // 자리 · 크기 · 색을 바꾸고 싶으면 아래 숫자만 고치고 메뉴를 다시 누른다.
    public static class WantedPosterLayout
    {
        private const string UndoName = "수배지 모양으로 정렬";
        private const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";
        private const string PaperFolder = "Assets/30.UI";
        private const string PaperPath = PaperFolder + "/WantedPaper.png";

        // 크기 (캔버스 칸. 1칸 = 1mm)
        private const int BoardWidth = 600;
        private const int BoardHeight = 900;   // 종이 790 + 아래 버튼 자리
        private const int PaperHeight = 790;
        private const int LineWidth = 500;     // 가로줄 길이
        private const int NoteWidth = 600;     // 라운드 쪽지
        private const int NoteHeight = 300;

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
                "2라운드<pos=70%>-100",
                40, InkBrown, FontStyles.Normal);
            detail.lineSpacing = 30;
            detail.enableAutoSizing = true;      // 줄이 길거나 많으면(승리는 4줄) 칸에 맞게 줄인다
            detail.fontSizeMin = 28;
            detail.fontSizeMax = 40;

            Image ruleBottom = GetImage(panel, "RuleBottom");
            PlaceLine(ruleBottom, 620);

            TextMeshProUGUI reward = GetText(panel, "Reward");
            PlaceFromTop(reward.rectTransform, 632, 520, 100);
            SetText(reward, font, "REWARD $ 2,080", 64, InkRed, FontStyles.Bold);
            reward.characterSpacing = 4;

            Button retryButton = GetButton(panel, "RetryButton");
            PlaceFromTop((RectTransform)retryButton.transform, 810, 320, 80);
            ArrangeButton(retryButton, font);

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

            // ⑤ ResultBoard 의 칸에 연결한다 (Inspector 칸에 끌어다 넣는 일을 코드로 한다)
            //    칸이 private 이라 board.resultText = subtitle 로는 못 넣는다.
            //    SerializedObject 는 "Inspector 가 보는 그 칸들" 을 이름으로 꺼내 주는 창구다.
            SerializedObject boardData = new SerializedObject(board);
            SetField(boardData, "panel", panel.gameObject);
            SetField(boardData, "resultText", subtitle);
            SetField(boardData, "detailText", detail);
            SetField(boardData, "rewardText", reward);
            SetField(boardData, "retryButton", retryButton);
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

        // ---------------------------------------------------------------- 자리 · 모양 정하기

        // Panel 위쪽 가운데를 기준으로 놓는다
        //   top    : Panel 맨 위에서 몇 칸 아래에 내 윗변이 오나
        //   width  : 가로 크기,  height : 세로 크기
        private static void PlaceFromTop(RectTransform rect, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);   // 기준점 : 부모(Panel)의 위쪽 가운데
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);       // 내 위쪽 가운데를 기준점에 맞춘다
            rect.anchoredPosition3D = new Vector3(0f, -top, 0f);   // UI 는 위쪽이 + 라서, 아래로 내리려면 - 를 붙인다
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

        // 다시 결투 버튼 : 나무 간판 색 + 크림색 글자
        private static void ArrangeButton(Button button, TMP_FontAsset font)
        {
            // 바탕 색은 Button 의 Color Tint 가 정한다. Image 색은 흰색(그대로)으로 둔다
            //   화면에 보이는 색 = Image 색 × 상태 색 이라서,
            //   Image 를 나무색으로 두면 상태 색으로 더 밝게 만들 수 없다 (곱하면 어두워지기만 한다)
            Image image = button.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;    // 버튼은 레이에 맞아야 눌린다

            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = WoodNormal;
            colors.highlightedColor = WoodHighlighted;
            colors.pressedColor = WoodPressed;
            colors.selectedColor = WoodNormal;   // 누른 뒤 "선택됨" 으로 밝은 채 남지 않게
            button.colors = colors;

            // 버튼 글자. Button - TextMeshPro 로 만들었으면 자식 Text (TMP) 가 이미 있다
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                GameObject labelObject = GetOrCreateChild((RectTransform)button.transform, "Text (TMP)");
                label = Undo.AddComponent<TextMeshProUGUI>(labelObject);
            }

            //    글자 칸이 버튼을 꽉 채우게 (Anchor 를 stretch 로, 여백 0)
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.localScale = Vector3.one;

            SetText(label, font, "다시 결투", 40, ButtonTextColor, FontStyles.Bold);
        }

        // ResultBoard 의 칸 하나에 넣는다. 칸 이름(필드 이름)이 틀리면 바로 멈춘다
        private static void SetField(SerializedObject data, string fieldName, Object value)
        {
            SerializedProperty field = data.FindProperty(fieldName);
            if (field == null)
                throw new Exception($"[WantedPosterLayout] ResultBoard 에 {fieldName} 칸이 없습니다. ResultBoard 의 필드 이름이 바뀌었으면 여기도 고쳐 주세요.");

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
