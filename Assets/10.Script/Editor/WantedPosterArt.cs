using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // [에디터 도구] 수배지에 쓰는 그림을 코드로 그려서 PNG 로 저장한다. WantedPosterLayout 이 정렬할 때마다 부른다.
    //   종이 · 나무 간판 · 못 · 총알 구멍 · 도장 테두리 · 몽타주(적 얼굴 판화) · 겁쟁이 실루엣
    //   그림 하나 = 점(픽셀)마다 색을 계산해서 Color[] 에 채운 것. 예전 종이 그림을 만들던 방식 그대로다
    //   그림을 바꾸고 싶으면 아래 숫자를 고치고 ⋮ 메뉴를 다시 누른다 (PNG 를 덮어쓴다)
    public static class WantedPosterArt
    {
        private const string Folder = "Assets/30.UI";

        // 몽타주
        public const int MontageSize = 360;                // 그림 크기 (점). 수배지에는 320칸으로 들어간다
        private const string SourceFolder = Folder + "/MontageSource";   // 초록 배경 얼굴 사진 (원본)
        private const float LinePeriod = 5f;               // 판화 가로줄 간격 (점)

        // 나무 간판 (버튼). 양쪽 끝 Border 칸은 늘어나지 않는다 → 못이 찌그러지지 않는다
        private const int WoodWidth = 288;
        private const int WoodHeight = 72;
        private const int WoodBorder = 32;

        // 잉크 · 종이 색
        private static readonly Color Ink = new Color32(48, 30, 18, 255);
        private static readonly Color PaperLight = new Color32(233, 212, 166, 255);   // 밝은 종이
        private static readonly Color PaperStain = new Color32(201, 164, 108, 255);   // 얼룩
        private static readonly Color PaperBurnt = new Color32(90, 54, 28, 255);      // 그을린 가장자리

        // 나무 · 쇠 색
        private static readonly Color WoodBase = new Color32(124, 80, 47, 255);
        private static readonly Color WoodDark = new Color32(70, 42, 24, 255);
        private static readonly Color WoodLight = new Color32(160, 110, 68, 255);
        private static readonly Color Iron = new Color32(52, 48, 45, 255);
        private static readonly Color IronLight = new Color32(170, 162, 150, 255);
        private static readonly Color Rust = new Color32(122, 62, 26, 255);

        // 총알 구멍 불씨
        private static readonly Color EmberOrange = new Color32(222, 98, 22, 255);
        private static readonly Color EmberYellow = new Color32(255, 186, 64, 255);

        // ================================================================ 종이

        // 종이 한 장. width × height 점 (캔버스 1칸에 점 하나)
        //   frameInset  : 가장자리에서 인쇄 테두리까지 몇 점 (0 이면 테두리 없음)
        //   foldsAcross : 가로로 접힌 자국 몇 줄 (수배지는 세 번 접어 품에 넣는다 → 2줄)
        public static Sprite CreatePaper(string fileName, int width, int height, int frameInset, int foldsAcross)
        {
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = GetPaperPixel(x, y, width, height, frameInset, foldsAcross);
                }
            }
            return SaveSprite(fileName, width, height, pixels, Vector4.zero);
        }

        // 종이 점 하나 (x, y) 의 색. 그림은 아래(y = 0)부터 위로 쌓인다
        //   Mathf.PerlinNoise(a, b) : 0 ~ 1 사이를 부드럽게 오르내리는 값
        //     곱하는 수가 작을수록 무늬가 크고(0.006 → 큰 얼룩), 클수록 잘다(0.45 → 종이 결)
        private static Color GetPaperPixel(int x, int y, int width, int height, int frameInset, int foldsAcross)
        {
            // ① 얼룩 : 큰 무늬와 잔 무늬를 섞고, 0.45 보다 진한 곳만 얼룩색으로 물들인다
            float big = Mathf.PerlinNoise(x * 0.006f + 13.1f, y * 0.006f + 7.3f);
            float small = Mathf.PerlinNoise(x * 0.05f + 41.7f, y * 0.05f + 3.9f);
            float stain = Mathf.Clamp01((big * 0.75f + small * 0.25f - 0.45f) * 2f);
            Color color = Color.Lerp(PaperLight, PaperStain, stain);

            // ② 종이 결 : 아주 잔 무늬와 가로로 긴 섬유 결로 밝기를 조금씩 흔든다 (매끈한 색판처럼 안 보이게)
            float grain = Mathf.PerlinNoise(x * 0.45f + 3.3f, y * 0.45f + 8.8f) - 0.5f;
            float fiber = Mathf.PerlinNoise(x * 0.015f + 21f, y * 0.6f + 2f) - 0.5f;
            color = Shade(color, 1f + grain * 0.10f + fiber * 0.07f);

            // ③ 물 자국 : 컵을 올려 두었던 둥근 고리. 완벽한 원이 아니게 반지름을 흔든다
            float ringRadius = Mathf.Min(width, height) * 0.09f;
            float ringX = x - width * 0.76f;
            float ringY = y - height * 0.28f;
            float ringDistance = Mathf.Sqrt(ringX * ringX + ringY * ringY) + Mathf.PerlinNoise(x * 0.08f + 5f, y * 0.08f + 9f) * 6f;
            float ringOffset = (ringDistance - ringRadius) / 2.5f;
            float ring = Mathf.Exp(-ringOffset * ringOffset) * 0.45f;
            if (ringDistance < ringRadius)
                ring += 0.10f;   // 고리 안쪽도 살짝 물들었다
            color = Color.Lerp(color, Shade(PaperStain, 0.85f), ring);

            // ④ 접힌 자국 : 세로 가운데 한 줄 + 가로 foldsAcross 줄
            float fold = FoldLine(x - width * 0.5f);
            for (int i = 1; i <= foldsAcross; i++)
                fold += FoldLine(y - height * i / (foldsAcross + 1f));
            color = Shade(color, 1f + fold);

            // ⑤ 인쇄 테두리 : 굵은 바깥 줄 + 가는 안쪽 줄 + 네 모서리 마름모. 잉크가 군데군데 닳았다
            if (frameInset > 0)
            {
                float ink = FrameLine(x, y, width, height, frameInset, 4f);
                ink = Mathf.Max(ink, FrameLine(x, y, width, height, frameInset + 10, 1.5f));
                ink = Mathf.Max(ink, CornerDiamond(x, y, width, height, frameInset + 10));
                float wear = Mathf.PerlinNoise(x * 0.09f + 70f, y * 0.09f + 30f);
                ink *= Mathf.Clamp01(1.4f - wear);
                color = Color.Lerp(color, Ink, ink * 0.85f);
            }

            // ⑥ 가장자리까지 거리 : 네 변 중 가장 가까운 변까지 몇 점
            float edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));

            // ⑦ 찢어진 가장자리 : 4 ~ 20점 사이로 들쭉날쭉한 선을 만들고, 그 바깥은 투명하게 뚫는다
            float tear = 4f + Mathf.PerlinNoise(x * 0.04f + 90.2f, y * 0.04f + 55.5f) * 16f;
            if (edge < tear)
                return Color.clear;

            // ⑧ 그을림 : 찢어진 선에 가까울수록 탄 색. 선에서 안쪽으로 burnReach 점 들어가면 원래 종이색
            //    작은 쪽지는 50점이면 거의 다 타 버려서, 짧은 변의 18% 를 넘지 않게 한다
            float burnReach = Mathf.Min(50f, Mathf.Min(width, height) * 0.18f);
            float burn = 1f - Mathf.Clamp01((edge - tear) / burnReach);
            return Color.Lerp(color, PaperBurnt, burn * burn);
        }

        // 접힌 선 하나. offset = 접힌 선까지 거리 (부호 있음)
        //   선 위는 어둡게, 한쪽 옆은 밝게(빛 받는 면), 다른 쪽 옆은 조금 어둡게(그늘) → 종이가 접혔다 펴진 것처럼 보인다
        private static float FoldLine(float offset)
        {
            if (offset > -1f && offset <= 1f) return -0.12f;
            if (offset > 1f && offset <= 4f) return 0.05f;
            if (offset > -6f && offset <= -1f) return -0.04f;
            return 0f;
        }

        // 사각 테두리 줄 하나 : 가장자리에서 inset 점 들어간 자리부터 안쪽으로 thickness 점
        private static float FrameLine(int x, int y, int width, int height, float inset, float thickness)
        {
            float left = x - inset;
            float right = width - 1 - inset - x;
            float bottom = y - inset;
            float top = height - 1 - inset - y;
            float inside = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));   // 줄 사각형 안쪽이면 + , 바깥이면 -
            if (inside >= 0f && inside < thickness)
                return 1f;
            return 0f;
        }

        // 안쪽 테두리 네 모서리의 작은 마름모 (|가로 거리| + |세로 거리| 가 작으면 마름모 안)
        private static float CornerDiamond(int x, int y, int width, int height, float inset)
        {
            float cornerX = Mathf.Min(Mathf.Abs(x - inset), Mathf.Abs(width - 1 - inset - x));
            float cornerY = Mathf.Min(Mathf.Abs(y - inset), Mathf.Abs(height - 1 - inset - y));
            if (cornerX + cornerY < 9f)
                return 1f;
            return 0f;
        }

        // ================================================================ 나무 간판 (버튼)

        public static Sprite CreateWoodSign(string fileName)
        {
            Color[] pixels = new Color[WoodWidth * WoodHeight];
            for (int y = 0; y < WoodHeight; y++)
            {
                for (int x = 0; x < WoodWidth; x++)
                {
                    pixels[y * WoodWidth + x] = GetWoodPixel(x, y);
                }
            }
            // Border (왼쪽, 아래, 오른쪽, 위) : 양쪽 끝 32점은 Sliced 로 늘릴 때 그대로 둔다
            return SaveSprite(fileName, WoodWidth, WoodHeight, pixels, new Vector4(WoodBorder, 0, WoodBorder, 0));
        }

        private static Color GetWoodPixel(int x, int y)
        {
            // ① 모서리를 둥글게 깎는다. 바깥은 투명
            float edge = RoundedEdge(x, y, WoodWidth, WoodHeight, 10f);
            if (edge < 0f)
                return Color.clear;

            // ② 나뭇결 : 가로로 길게 흐르는 줄무늬. Perlin 으로 무늬를 휘게 한다
            float warp = Mathf.PerlinNoise(x * 0.012f + 4f, y * 0.03f + 1f) * 18f;
            float rings = Mathf.Sin((y + warp) * 0.55f) * 0.5f + 0.5f;
            float streak = Mathf.PerlinNoise(x * 0.02f + 50f, y * 0.35f + 7f) - 0.5f;
            Color color = Color.Lerp(WoodBase, WoodDark, Mathf.Clamp01(rings * 0.35f + streak * 0.6f));

            // ③ 옹이 : 한 군데 가로로 긴 동그라미가 짙다
            float knotX = (x - 196f) / 11f;
            float knotY = (y - 50f) / 5f;
            float knot = 1f - Mathf.Clamp01(Mathf.Sqrt(knotX * knotX + knotY * knotY));
            color = Color.Lerp(color, WoodDark, knot * 0.8f);

            // ④ 판자 두 장 : 가운데 가로 틈
            if (Mathf.Abs(y - WoodHeight * 0.5f) < 1.2f)
                color = Color.Lerp(color, WoodDark, 0.8f);

            // ⑤ 테두리 : 맨 바깥 3점은 짙게, 그 안 3점은 위쪽만 밝고 아래쪽은 어둡게 (빛이 위에서)
            if (edge < 3f)
                color = Color.Lerp(color, WoodDark, 0.85f);
            else if (edge < 6f && y > WoodHeight * 0.5f)
                color = Color.Lerp(color, WoodLight, 0.5f);
            else if (edge < 6f)
                color = Color.Lerp(color, WoodDark, 0.35f);

            // ⑥ 양쪽 끝 못 (Border 칸 안이라 늘어나지 않는다)
            color = DrawNail(color, x, y, 16f, WoodHeight * 0.5f, 5f);
            color = DrawNail(color, x, y, WoodWidth - 17f, WoodHeight * 0.5f, 5f);
            return color;
        }

        // 둥근 모서리 사각형의 가장자리까지 거리. 바깥이면 - 를 준다
        private static float RoundedEdge(int x, int y, int width, int height, float radius)
        {
            // 모서리 둥근 부분의 중심 : 점을 "모서리에서 radius 만큼 들어간 사각형" 안으로 당긴 자리
            float centerX = Mathf.Clamp(x, radius, width - 1 - radius);
            float centerY = Mathf.Clamp(y, radius, height - 1 - radius);
            float cornerX = x - centerX;
            float cornerY = y - centerY;
            float cornerDistance = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
            if (cornerDistance > 0f)
                return radius - cornerDistance;   // 네 모서리 근처 : 둥근 선까지 거리

            return Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));   // 나머지 : 곧은 변까지 거리
        }

        // 못 머리 하나를 color 위에 그린다. 빛은 위 왼쪽에서, 그림자는 아래 오른쪽으로
        private static Color DrawNail(Color color, float x, float y, float centerX, float centerY, float radius)
        {
            float distance = Distance(x, y, centerX, centerY);
            if (distance > radius)
            {
                if (Distance(x, y, centerX + 1.5f, centerY - 1.5f) < radius + 0.5f)
                {
                    if (color.a == 0f)
                        return new Color(0f, 0f, 0f, 0.4f);   // 투명한 바탕 위면 반투명 검정 그림자
                    return Shade(color, 0.6f);
                }
                return color;
            }

            float light = Mathf.Clamp01(1f - Distance(x, y, centerX - radius * 0.35f, centerY + radius * 0.35f) / radius);
            Color nail = Color.Lerp(Iron, IronLight, light * light);
            if (distance > radius - 1f)
                nail = Shade(nail, 0.6f);   // 못 머리 테두리
            return nail;
        }

        // ================================================================ 못 (종이를 벽에 박은 것)

        // 40 × 80 점. 위쪽에 못 머리, 그 아래로 녹물이 흘러내린 자국
        public static Sprite CreateNail(string fileName)
        {
            const int width = 40;
            const int height = 80;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // ① 녹물 : 못 아래로 갈수록 가늘고 옅어진다. 좌우로 조금 구불거린다
                    float drip = (60f - y) / 55f;   // 못 자리(y = 60)에서 0, 아래 끝에서 1
                    Color color = Color.clear;
                    if (drip > 0f && drip < 1f)
                    {
                        float wiggle = Mathf.Sin(y * 0.35f) * 1.2f;
                        float halfWidth = 4.5f * (1f - drip);
                        if (Mathf.Abs(x - 20f - wiggle) < halfWidth)
                            color = new Color(Rust.r, Rust.g, Rust.b, 0.55f * (1f - drip));
                    }

                    // ② 못 머리
                    pixels[y * width + x] = DrawNail(color, x, y, 20f, 60f, 9f);
                }
            }
            return SaveSprite(fileName, width, height, pixels, Vector4.zero);
        }

        // ================================================================ 총알 구멍

        public static Sprite CreateBulletHole(string fileName)
        {
            const int size = 64;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = GetBulletHolePixel(x, y, size);
                }
            }
            return SaveSprite(fileName, size, size, pixels, Vector4.zero);
        }

        private static Color GetBulletHolePixel(int x, int y, int size)
        {
            float center = size * 0.5f;
            float offsetX = x - center;
            float offsetY = y - center;
            float distance = Mathf.Sqrt(offsetX * offsetX + offsetY * offsetY);
            float angle = Mathf.Atan2(offsetY, offsetX);   // 가운데에서 본 방향 (라디안, -π ~ π)

            // ① 뚫린 구멍 : 방향마다 반지름을 흔들어 들쭉날쭉하게
            float jag = Mathf.PerlinNoise(angle * 2.5f + 10f, 3.3f);
            float holeRadius = size * 0.12f + jag * 3f;
            if (distance < holeRadius)
                return new Color(0.07f, 0.04f, 0.02f, 1f);

            // ② 탄 자국 고리 : 구멍에서 멀어질수록 옅어진다
            float burnRadius = size * 0.30f + jag * 5f;
            float burn = 1f - Mathf.Clamp01((distance - holeRadius) / (burnRadius - holeRadius));

            // ③ 갈라진 금 6줄 : 그 방향 선까지 거리가 1점보다 가까우면 금
            float crack = 0f;
            float crackLength = size * 0.46f;
            for (int i = 0; i < 6; i++)
            {
                float crackAngle = i * 1.047f + 0.4f + (i % 2) * 0.3f;   // 1.047 = 60도
                float angleGap = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, crackAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                if (Mathf.Abs(angleGap) < 1.5f && distance * Mathf.Abs(Mathf.Sin(angleGap)) < 0.9f && distance < crackLength)
                    crack = Mathf.Max(crack, 1f - distance / crackLength);
            }

            Color color = Color.Lerp(PaperBurnt, new Color(0.1f, 0.06f, 0.03f), burn);
            float alpha = Mathf.Clamp01(burn * burn * 0.9f + crack * 0.8f);

            // ④ 불씨 : 구멍 바로 바깥 띠가 주황색으로 달아올랐다. 구멍에 붙은 쪽이 가장 밝고, 바깥으로 갈수록 탄 색으로 식는다
            //    띠 두께를 방향마다 흔들어서(ember) 불씨가 군데군데 더 남은 것처럼 보이게 한다
            float emberWidth = 3f + Mathf.PerlinNoise(angle * 4f + 30f, 7.7f) * 4f;
            float ember = 1f - Mathf.Clamp01((distance - holeRadius) / emberWidth);
            if (ember > 0f)
            {
                Color glow = Color.Lerp(EmberOrange, EmberYellow, ember * ember);   // 가장 안쪽은 노랗게 빛난다
                color = Color.Lerp(color, glow, ember);
                alpha = Mathf.Max(alpha, ember);
            }

            color.a = alpha;
            return color;
        }

        // ================================================================ 도장 테두리

        // 흰색으로 그려 두고, 색은 Image.color 로 칠한다 (빨간 도장)
        public static Sprite CreateStampFrame(string fileName)
        {
            const int width = 340;
            const int height = 120;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float edge = RoundedEdge(x, y, width, height, 16f);
                    float ink = 0f;
                    if (edge >= 0f && edge < 7f) ink = 1f;           // 굵은 바깥 줄
                    if (edge >= 11f && edge < 13.5f) ink = 1f;      // 가는 안쪽 줄

                    // 도장 잉크는 고르게 안 묻는다 : 무늬가 높은 곳은 거의 비운다
                    float blot = Mathf.PerlinNoise(x * 0.15f + 11f, y * 0.15f + 22f) * 0.7f
                               + Mathf.PerlinNoise(x * 0.5f + 3f, y * 0.5f + 9f) * 0.3f;
                    if (blot > 0.66f)
                        ink *= 0.15f;

                    pixels[y * width + x] = new Color(1f, 1f, 1f, ink);
                }
            }
            return SaveSprite(fileName, width, height, pixels, Vector4.zero);
        }

        // ================================================================ 몽타주

        // 초록 배경에서 찍은 얼굴 사진(MontageSource 폴더)을 읽어, 동판화처럼 가로줄로 다시 그린다
        //   sourceFileName : Assets/30.UI/MontageSource/ 안의 파일 이름. 얼굴 크기 · 눈 높이를 셋이 같게 잘라 둔 것
        public static Sprite BakeMontage(string sourceFileName, string fileName)
        {
            int size = MontageSize;
            float[] tone = new float[size * size];   // 0 = 밝음 ~ 1 = 어두움
            float[] mask = new float[size * size];   // 1 = 사람, 0 = 배경
            ReadGreenScreen(sourceFileName, tone, mask);
            return SaveEngraving(fileName, tone, mask);
        }

        // 초록 배경 사진에서 사람만 오려 내고(mask), 사람 부분의 진하기(tone)를 꺼낸다
        private static void ReadGreenScreen(string sourceFileName, float[] tone, float[] mask)
        {
            int size = MontageSize;
            string path = SourceFolder + "/" + sourceFileName;
            if (File.Exists(path) == false)
                throw new Exception($"[WantedPosterArt] 몽타주 원본이 없습니다 → {path}. 초록 배경에서 찍은 얼굴 사진을 이 이름으로 넣어 주세요.");

            // ① PNG 파일을 바로 읽는다 (유니티가 가져온 텍스처는 Read/Write 를 켜야 점을 읽을 수 있어서, 파일을 직접 읽는다)
            Texture2D photo = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            photo.LoadImage(File.ReadAllBytes(path));   // 크기는 파일에 맞게 바뀐다

            float darkest = 0f;
            float lightest = 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // GetPixelBilinear(0~1, 0~1) : 사진 크기와 상관없이 그 비율 자리의 색
                    Color color = photo.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);

                    // ② 초록이 빨강 · 파랑보다 얼마나 센가 → 셀수록 배경. 경계는 반쯤 섞인다
                    float greenness = color.g - Mathf.Max(color.r, color.b);
                    float person = 1f - Mathf.Clamp01((greenness - 0.15f) / 0.25f);

                    // ③ 밝기 → 진하기 (어두울수록 1)
                    float brightness = color.r * 0.30f + color.g * 0.59f + color.b * 0.11f;
                    float darkness = 1f - brightness;

                    int index = y * size + x;
                    mask[index] = person;
                    tone[index] = darkness;

                    // 사람 부분에서 가장 밝은 곳 · 가장 어두운 곳을 기억한다 (④ 에서 대비를 넓힌다)
                    if (person > 0.9f)
                    {
                        darkest = Mathf.Max(darkest, darkness);
                        lightest = Mathf.Min(lightest, darkness);
                    }
                }
            }
            Object.DestroyImmediate(photo);

            if (darkest <= lightest)
                throw new Exception($"[WantedPosterArt] {path} 에서 사람을 찾지 못했습니다. 배경이 초록색인지 확인해 주세요.");

            // ④ 대비를 넓힌다 : 사람 부분의 가장 밝은 곳이 0, 가장 어두운 곳이 1 이 되게 늘린다
            float range = Mathf.Max(darkest - lightest, 0.2f);
            for (int i = 0; i < tone.Length; i++)
                tone[i] = Mathf.Clamp01((tone[i] - lightest) / range);
        }

        // 반칙(COWARD) 일 때 쓰는 그림 : 얼굴을 모르는 사람의 검은 실루엣 (= 플레이어 자신)
        public static Sprite CreateCowardSilhouette(string fileName)
        {
            int size = MontageSize;
            float[] tone = new float[size * size];
            float[] mask = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 가장자리가 계단처럼 깨지지 않게 한 점을 2 × 2 로 쪼개 보고 평균을 낸다
                    float inside = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = (x + 0.25f + sx * 0.5f) / size;
                            float v = (y + 0.25f + sy * 0.5f) / size;
                            if (IsInsideSilhouette(u, v))
                                inside += 0.25f;
                        }
                    }
                    int index = y * size + x;
                    mask[index] = inside;
                    tone[index] = 0.78f + (1f - (float)y / size) * 0.15f;   // 아래로 갈수록 조금 더 어둡게
                }
            }
            return SaveEngraving(fileName, tone, mask);
        }

        // 카우보이 실루엣 (u, v = 그림 안의 자리 0 ~ 1. v 는 아래가 0)
        //   타원 안 : ((u - 가운데u) / 가로반지름)² + ((v - 가운데v) / 세로반지름)² < 1
        private static bool IsInsideSilhouette(float u, float v)
        {
            if (Ellipse(u, v, 0.5f, 0.02f, 0.40f, 0.26f)) return true;                          // 어깨
            if (Mathf.Abs(u - 0.5f) < 0.065f && v > 0.15f && v < 0.42f) return true;          // 목
            if (Ellipse(u, v, 0.5f, 0.47f, 0.125f, 0.15f)) return true;                         // 머리
            if (Ellipse(u, v, 0.5f, 0.60f, 0.27f, 0.04f)) return true;                          // 모자 챙
            if (v > 0.60f && v < 0.80f && Mathf.Abs(u - 0.5f) < 0.13f - (v - 0.6f) * 0.15f) return true;   // 모자 몸통 (위로 갈수록 좁게)
            return false;
        }

        private static bool Ellipse(float u, float v, float centerU, float centerV, float radiusU, float radiusV)
        {
            float a = (u - centerU) / radiusU;
            float b = (v - centerV) / radiusV;
            return a * a + b * b < 1f;
        }

        // tone · mask 를 판화 그림으로 바꿔 저장한다 (몽타주와 실루엣이 같이 쓴다)
        private static Sprite SaveEngraving(string fileName, float[] tone, float[] mask)
        {
            int size = MontageSize;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = GetEngravedPixel(x, y, tone, mask);
                }
            }
            return SaveSprite(fileName, size, size, pixels, Vector4.zero);
        }

        // 판화 점 하나. 잉크색에 진하기(alpha)만 바꾼다 → 투명한 곳으로 아래 종이가 비친다 (종이에 찍힌 것처럼)
        private static Color GetEngravedPixel(int x, int y, float[] tone, float[] mask)
        {
            int size = MontageSize;
            int index = y * size + x;
            float person = mask[index];
            float dark = tone[index] * person;

            // ① 가로줄 : 어두운 곳일수록 줄이 굵다. 줄을 살짝 물결치게 해서 손으로 판 느낌을 낸다
            //    d = 줄 한가운데에서 얼마나 떨어졌나 (0 = 한가운데, 1 = 줄 사이 한가운데)
            float phase = (y + Mathf.Sin(x * 0.045f) * 1.6f) / LinePeriod;
            float d = Mathf.Abs(phase - Mathf.Floor(phase) - 0.5f) * 2f;
            float line = Mathf.Clamp01((dark - d - 0.1f) * LinePeriod + 0.5f);

            // ② 아주 어두운 곳은 사선을 한 번 더 긋는다 (크로스 해칭)
            float hatchTone = Mathf.Clamp01((dark - 0.62f) / 0.38f);
            float phase2 = (x + y) / (LinePeriod * 1.2f);
            float d2 = Mathf.Abs(phase2 - Mathf.Floor(phase2) - 0.5f) * 2f;
            float hatch = Mathf.Clamp01((hatchTone - d2 - 0.1f) * LinePeriod + 0.5f);

            float ink = person * (dark * 0.22f + Mathf.Max(line, hatch) * 0.78f);

            // ③ 윤곽선 : 사람 경계(mask 가 바뀌는 곳)는 진하게, 얼굴 안의 명암 경계는 조금
            float edge = Mathf.Clamp01(Gradient(mask, x, y) * 1.2f)
                       + Mathf.Clamp01((Gradient(tone, x, y) * person - 0.12f) * 2.5f) * 0.7f;
            ink = Mathf.Max(ink, Mathf.Clamp01(edge));

            // ④ 사진 칸 : 바탕을 옅게 칠하고 가장자리로 갈수록 조금 더 어둡게 (오래된 사진 느낌)
            float centerX = (x - size * 0.5f) / (size * 0.5f);
            float centerY = (y - size * 0.5f) / (size * 0.5f);
            float wash = 0.06f + (centerX * centerX + centerY * centerY) * 0.08f;
            ink = Mathf.Max(ink, wash);

            // ⑤ 사진 테두리 : 굵은 바깥 줄 + 가는 안쪽 줄
            float border = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
            if (border < 6f || (border >= 10f && border < 12f))
                ink = 1f;

            return new Color(Ink.r, Ink.g, Ink.b, Mathf.Clamp01(ink));
        }

        // (x, y) 자리에서 값이 얼마나 가파르게 바뀌나 (Sobel). 경계일수록 크다
        private static float Gradient(float[] values, int x, int y)
        {
            int size = MontageSize;
            if (x < 1 || y < 1 || x > size - 2 || y > size - 2)
                return 0f;

            float topLeft = values[(y + 1) * size + x - 1];
            float top = values[(y + 1) * size + x];
            float topRight = values[(y + 1) * size + x + 1];
            float left = values[y * size + x - 1];
            float right = values[y * size + x + 1];
            float bottomLeft = values[(y - 1) * size + x - 1];
            float bottom = values[(y - 1) * size + x];
            float bottomRight = values[(y - 1) * size + x + 1];

            float gx = (topRight + 2f * right + bottomRight) - (topLeft + 2f * left + bottomLeft);
            float gy = (topLeft + 2f * top + topRight) - (bottomLeft + 2f * bottom + bottomRight);
            return Mathf.Sqrt(gx * gx + gy * gy) * 0.25f;
        }

        // ================================================================ 현상금 게시판 (메인화면)

        // 비바람에 낡은 나무 게시판의 앞면 : 가로 판자 여러 장 + 판자 끝마다 못
        //   둘레 틀은 그림이 아니라 3D 나무 막대(큐브)로 감싼다 (WantedPosterLayout.PlaceBoardModel)
        public static Sprite CreateBoard(string fileName, int width, int height)
        {
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = GetBoardPixel(x, y, width, height);
                }
            }
            return SaveSprite(fileName, width, height, pixels, Vector4.zero);
        }

        private static Color GetBoardPixel(int x, int y, int width, int height)
        {
            const int plankCount = 7;       // 판자 수

            // ② 판자 : 몇 번째 판자인가, 판자 안에서 어디인가
            float plankHeight = (float)height / plankCount;
            int plank = Mathf.Min((int)(y / plankHeight), plankCount - 1);
            float plankY = y - plank * plankHeight;     // 판자 아래 변에서 몇 점 위

            // ③ 판자마다 색을 조금씩 다르게 (햇빛에 바랜 정도가 다르다)
            float plankTint = 0.82f + Mathf.PerlinNoise(plank * 3.7f + 1.3f, 0.5f) * 0.3f;

            // ④ 나뭇결 : 가로로 흐르고 Perlin 으로 휜다. 판자마다 무늬 자리를 옮긴다
            float warp2 = Mathf.PerlinNoise(x * 0.006f + plank * 9f, plankY * 0.04f) * 22f;
            float rings2 = Mathf.Sin((plankY + warp2) * 0.42f) * 0.5f + 0.5f;
            float streak = Mathf.PerlinNoise(x * 0.015f + plank * 5f, plankY * 0.3f) - 0.5f;
            Color color = Color.Lerp(WoodBase, WoodDark, Mathf.Clamp01(rings2 * 0.35f + streak * 0.7f));
            color = Shade(color, plankTint);

            // ⑤ 비바람 자국 : 큰 무늬로 군데군데 회색빛으로 바랬다
            float weather = Mathf.Clamp01((Mathf.PerlinNoise(x * 0.004f + 30f, y * 0.004f + 12f) - 0.45f) * 2.5f);
            color = Color.Lerp(color, new Color(0.45f, 0.40f, 0.35f), weather * 0.35f);

            // ⑥ 판자 사이 틈 : 아래 · 위 3점은 어둡게, 위쪽 가장자리 바로 아래는 살짝 밝게
            if (plankY < 2.5f || plankY > plankHeight - 1.5f)
                color = Color.Lerp(color, new Color(0.08f, 0.05f, 0.03f), 0.85f);
            else if (plankY > plankHeight - 5f)
                color = Shade(color, 1.12f);
            else if (plankY < 6f)
                color = Shade(color, 0.85f);

            // ⑦ 판자 양 끝 못 (틀에 박혀 있다)
            float nailY = plank * plankHeight + plankHeight * 0.5f;
            color = DrawNail(color, x, y, 18f, nailY, 4f);
            color = DrawNail(color, x, y, width - 1 - 18f, nailY, 4f);
            return color;
        }

        // ================================================================ 리볼버 실린더 (라운드 쪽지)

        // 리볼버 실린더를 뒤에서 본 모습 : 쇠 원판 + 약실 구멍 6개 + 가운데 축 + 테두리 홈 6개
        //   약실 구멍은 비어 있는(쏜) 모습이다. 남은 탄은 그 위에 탄 그림(CreateCartridge)을 겹쳐서 보여 준다
        public const int CylinderSize = 256;
        public const float ChamberRingRadius = 72f;   // 가운데에서 약실 가운데까지 (점)
        public const float ChamberRadius = 28f;       // 약실 구멍 반지름 (점)
        private static readonly Color Steel = new Color32(78, 80, 84, 255);
        private static readonly Color SteelDark = new Color32(34, 35, 38, 255);
        private static readonly Color SteelLight = new Color32(150, 154, 160, 255);
        private static readonly Color CartridgeBrass = new Color32(196, 150, 64, 255);
        private static readonly Color CartridgeBrassLight = new Color32(240, 206, 120, 255);
        private static readonly Color Primer = new Color32(176, 172, 164, 255);

        public static Sprite CreateCylinder(string fileName)
        {
            int size = CylinderSize;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = GetCylinderPixel(x + 0.5f, y + 0.5f);
                }
            }
            return SaveSprite(fileName, size, size, pixels, Vector4.zero);
        }

        private static Color GetCylinderPixel(float x, float y)
        {
            float center = CylinderSize * 0.5f;
            float dx = x - center;
            float dy = y - center;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;   // 오른쪽 0도, 시계 반대 방향 +

            // ① 바깥 원 + 테두리 홈 : 약실(90도, 30도, -30도 ...) 사이(0도, 60도, 120도 ...)마다 가장자리를 판다
            float outerRadius = 120f;
            float flute = Mathf.Abs(Mathf.Repeat(angle + 30f, 60f) - 30f);   // 홈 가운데에서 몇 도. Repeat = 60 으로 나눈 나머지
            float fluteDepth = Mathf.Clamp01(1f - flute / 12f) * 14f;   // 홈 가운데일수록 깊게
            float edge = outerRadius - fluteDepth - distance;
            float coverage = Mathf.Clamp01(edge + 0.5f);
            if (coverage <= 0f)
                return Color.clear;

            // ② 쇠 원판 : 위 왼쪽에서 빛을 받는다. 가장자리 둥근 면은 어둡게, 동심원 깎은 자국
            float facing = (-dx * 0.7071f + dy * 0.7071f) / Mathf.Max(distance, 1f);
            Color color = Color.Lerp(Steel, SteelLight, Mathf.Clamp01(facing * 0.35f + 0.2f) * (distance / outerRadius));
            color = Shade(color, 1f + Mathf.Sin(distance * 0.9f) * 0.03f);
            if (edge < 5f) color = Shade(color, 0.7f);

            // ③ 약실 구멍 6개 : 깊은 구멍이라 거의 검다. 구멍 가장자리는 깎아서 밝게
            for (int i = 0; i < 6; i++)
            {
                float chamberAngle = (90f - i * 60f) * Mathf.Deg2Rad;   // 맨 위부터 시계 방향
                float chamberX = center + Mathf.Cos(chamberAngle) * ChamberRingRadius;
                float chamberY = center + Mathf.Sin(chamberAngle) * ChamberRingRadius;
                float chamberDistance = Distance(x, y, chamberX, chamberY);
                if (chamberDistance < ChamberRadius + 3f)
                    color = Color.Lerp(color, SteelLight, Mathf.Clamp01(ChamberRadius + 3f - chamberDistance));   // 깎은 테
                if (chamberDistance < ChamberRadius)
                {
                    float depth = chamberDistance / ChamberRadius;   // 가장자리일수록 구멍 벽이 보인다
                    color = Color.Lerp(new Color(0.03f, 0.03f, 0.03f), SteelDark, depth * depth);
                }
            }

            // ④ 가운데 축 구멍
            if (distance < 16f) color = Color.Lerp(SteelLight, SteelDark, Mathf.Clamp01((16f - distance) / 4f));
            if (distance < 9f) color = new Color(0.04f, 0.04f, 0.04f);

            color.a = coverage;
            return color;
        }

        // 약실에 든 탄 (뒤에서 본 탄피 바닥) : 놋쇠 테 + 가운데 뇌관. 약실 구멍과 같은 크기로 겹친다
        public static Sprite CreateCartridge(string fileName)
        {
            const int size = 64;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float coverage = Mathf.Clamp01(31f - distance + 0.5f);
                    if (coverage <= 0f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float facing = (-dx * 0.7071f + dy * 0.7071f) / Mathf.Max(distance, 1f);
                    Color color = Color.Lerp(CartridgeBrass, CartridgeBrassLight, Mathf.Clamp01(facing * 0.5f + 0.4f));
                    if (distance > 27f) color = Shade(color, 0.75f);                 // 탄피 테
                    if (distance < 11f) color = Color.Lerp(Primer, Shade(Primer, 0.7f), distance / 11f);   // 뇌관
                    if (distance > 10f && distance < 12f) color = Shade(CartridgeBrass, 0.55f);           // 뇌관 둘레 홈
                    color.a = coverage;
                    pixels[y * size + x] = color;
                }
            }
            return SaveSprite(fileName, size, size, pixels, Vector4.zero);
        }

        // ================================================================ 음량 탄띠 (메인화면 음량 조절)

        private static readonly Color HullRed = new Color32(168, 36, 28, 255);      // 샷건 쉘 몸통 (붉은 종이 · 플라스틱)
        private static readonly Color HullDark = new Color32(92, 18, 14, 255);
        private static readonly Color HullLight = new Color32(222, 92, 70, 255);
        private static readonly Color Leather = new Color32(112, 68, 38, 255);      // 탄띠 가죽
        private static readonly Color LeatherDark = new Color32(58, 33, 18, 255);
        private static readonly Color LeatherLight = new Color32(150, 98, 58, 255);
        private static readonly Color Stitch = new Color32(214, 186, 132, 255);     // 바느질 실

        public const int BeltLoops = 10;   // 탄띠 고리 수 = 음량 10칸 (10% 씩)

        // 쉘 그림의 높이 자리 (점, 그림 맨 위에서 아래로). 위에서 살짝 내려다본 원통이라 가로 경계는 모두 가운데가 아래로 휜다
        private const float ShellCapCenter = 6f;     // 윗면 타원 가운데
        private const float ShellCapHalfHeight = 5f; // 윗면 타원 세로 반지름 (가로 반지름은 테 폭의 절반) → 납작할수록 옆에서 본 느낌
        private const float ShellBend = 5f;          // 가로 경계가 가운데에서 아래로 휘는 양 (= 윗면 타원 세로 반지름과 같게 → 같은 원근)
        private const float ShellRimEnd = 12f;       // 황동 테 (윗면 바로 아래 두꺼운 띠) 끝
        private const float ShellBrassEnd = 32f;     // 황동 밑동 끝
        private const float ShellHullEnd = 80f;      // 붉은 몸통 끝 → 그 아래는 접힌 끝

        // 샷건 쉘 한 발 (위에서 살짝 내려다본 원통) : 위 = 황동 바닥(타원 윗면 + 가운데 신관), 아래 = 붉은 몸통 (끝은 옆면만)
        //   탄띠에 거꾸로(황동이 위로) 꽂은 모양. 쉘을 쥐는 가죽 고리는 따로 그린다 (CreateBeltStrap)
        //   40 × 96 점 → 탄띠에는 20 × 48 칸으로 꽂는다. 빛은 왼쪽 위에서 (게시판의 다른 그림과 같게)
        //
        //   입체를 만드는 방법 : 그림의 높이(sy)에서 "가운데가 아래로 휜 양(bend)" 을 뺀 높이(s)로 칸을 나눈다
        //     → 테 · 밑동 · 몸통 · 고리 띠의 경계가 모두 아래로 볼록한 호가 되어, 원통을 위에서 내려다본 것처럼 보인다
        public static Sprite CreateShotgunShell(string fileName)
        {
            const int width = 40;
            const int height = 96;
            const float rimHalf = 19f;     // 테 · 윗면 가로 반지름
            const float brassHalf = 17f;   // 황동 밑동 가로 반지름
            const float hullHalf = 15f;    // 붉은 몸통 가로 반지름
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float sy = height - 1 - y + 0.5f;   // 그림 맨 위에서 아래로 (점 가운데)
                for (int x = 0; x < width; x++)
                {
                    float dx = x + 0.5f - width * 0.5f;                  // 가운데에서 가로로 (-20 ~ 20)
                    float u = (x + 0.5f) / width;                        // 0 ~ 1
                    float round = Mathf.Sin(u * Mathf.PI);               // 원기둥 : 가운데가 앞으로 나와 밝다
                    float light = Mathf.Clamp01(1f - Mathf.Abs(u - 0.36f) * 3.2f);   // 왼쪽에서 빛 → 세로 하이라이트
                    Color color = Color.clear;

                    // 이 세로줄에서 가로 경계가 아래로 휜 양 (가운데 최대, 양끝 0)
                    float bend = ShellBend * Mathf.Sqrt(Mathf.Max(0f, 1f - (dx / rimHalf) * (dx / rimHalf)));
                    float s = sy - bend;   // 휜 경계에 맞춘 높이

                    // ① 윗면 (황동 바닥) : 타원. 바깥 테두리 고리 + 가운데 신관
                    float capU = dx / rimHalf;
                    float capV = (sy - ShellCapCenter) / ShellCapHalfHeight;
                    float cap = capU * capU + capV * capV;               // 1 이하면 윗면 안
                    if (cap <= 1f)
                    {
                        // 바닥 면은 위를 본다 → 몸통보다 밝다. 왼쪽 위가 가장 밝게
                        color = Color.Lerp(CartridgeBrass, CartridgeBrassLight, Mathf.Clamp01(0.55f - capU * 0.35f - capV * 0.25f));
                        if (cap > 0.78f) color = Shade(color, 0.72f);    // 테 둘레 (바닥 가장자리 턱)
                        if (cap > 0.62f && cap <= 0.7f) color = Shade(color, 0.85f);   // 안쪽 홈 한 줄

                        // 신관 : 가운데 작은 은색 타원 + 어두운 테. 오른쪽 아래가 어두운 오목한 금속
                        float pu = dx / 6.5f;
                        float pv = (sy - ShellCapCenter) / 2.6f;
                        float primer = pu * pu + pv * pv;
                        if (primer <= 1f)
                            color = Color.Lerp(Primer, Shade(Primer, 0.65f), Mathf.Clamp01(0.4f + pu * 0.3f + pv * 0.3f));
                        if (primer > 1f && primer <= 1.45f)
                            color = Shade(CartridgeBrass, 0.5f);         // 신관 둘레 홈

                        pixels[y * width + x] = color;
                        continue;
                    }

                    // ② 황동 테 (윗면 아래 두꺼운 띠) : 윗면 타원 아래쪽부터 휜 경계 ShellRimEnd 까지
                    if (sy > ShellCapCenter && s < ShellRimEnd)
                    {
                        if (Mathf.Abs(dx) < rimHalf)
                            color = Shade(Color.Lerp(CartridgeBrass, CartridgeBrassLight, light), 0.5f + round * 0.45f);
                    }
                    // ③ 황동 밑동 : 테보다 조금 가늘다. 몸통 쪽 끝에 접힌 줄 하나
                    else if (s >= ShellRimEnd && s < ShellBrassEnd)
                    {
                        if (Mathf.Abs(dx) < brassHalf)
                        {
                            color = Shade(Color.Lerp(CartridgeBrass, CartridgeBrassLight, light), 0.6f + round * 0.45f);
                            if (s < ShellRimEnd + 1.5f) color = Shade(color, 0.6f);        // 테 아래 그늘
                            if (s > ShellBrassEnd - 3f) color = Shade(color, 0.72f);       // 몸통에 물린 접힌 줄
                        }
                    }
                    // ④ 붉은 몸통 : 가로 결이 희미하게 (결도 휘어서 원통으로 보인다)
                    else if (s >= ShellBrassEnd && s < ShellHullEnd)
                    {
                        if (Mathf.Abs(dx) < hullHalf)
                        {
                            color = Color.Lerp(HullDark, HullRed, 0.35f + round * 0.65f);
                            color = Color.Lerp(color, HullLight, light * 0.55f);
                            if (Mathf.Repeat(s - ShellBrassEnd, 7f) < 1f) color = Shade(color, 0.9f);
                        }
                    }
                    // ⑤ 아래쪽 끝 (접힌 끝의 옆면) : 위에서 내려다보니 끝의 면(별 모양 접힘)은 안 보이고 옆면만 보인다
                    //    끝으로 갈수록 조금 오므라들고 어두워진다. 아래 윤곽은 휜 경계라서 둥근 원통 바닥으로 보인다
                    else if (s >= ShellHullEnd)
                    {
                        float t = (s - ShellHullEnd) / 9f;                // 0 ~ 1 (몸통 쪽 → 끝)
                        float halfWidth = hullHalf * (1f - t * t * 0.18f);   // 끝이 살짝 오므라든다
                        if (t <= 1f && Mathf.Abs(dx) < halfWidth)
                        {
                            color = Color.Lerp(HullDark, HullRed, 0.35f + round * 0.65f);
                            color = Color.Lerp(color, HullLight, light * 0.55f);
                            color = Shade(color, 1f - t * 0.25f);       // 끝으로 갈수록 조금 어둡게
                        }
                    }

                    pixels[y * width + x] = color;
                }
            }
            return SaveSprite(fileName, width, height, pixels, Vector4.zero);
        }

        // 탄띠 고리 하나 (쉘을 감싸 쥐는 가죽 띠). 쉘과 따로 그려서 탄띠 위 · 쉘 위에 늘 깔아 둔다
        //   → 쉘이 빠진 칸에도 고리는 남는다. 쉘이 꽂히면 고리 아래로 쉘이 지나가 꽂혀 보인다
        //   48 × 40 점 → 탄띠에는 24 × 20 칸으로 깐다 (쉘 20 칸보다 조금 넓어 쉘을 감싼다)
        //   쉘과 같은 원근 : 위아래 경계가 가운데에서 아래로 휜다. 띠 바로 아래에 반투명 그림자 (쉘이나 탄띠 위에 진다)
        //   띠의 휘지 않은 가운데 줄이 그림 위에서 StrapBandTop + StrapBandHeight / 2 점 → 그림 가운데보다 StrapAnchorOffset 점 위
        public const int StrapWidth = 48;
        public const int StrapHeight = 40;
        private const float StrapBandTop = 4f;      // 띠 위 경계 (그림 맨 위에서, 휘지 않은 양끝 기준)
        private const float StrapBandHeight = 24f;  // 띠 두께 (쉘 그림의 고리 띠와 같은 비율)
        private const float StrapBend = 5f;         // 가운데가 아래로 휘는 양 (쉘의 원근과 같게)
        public const float StrapAnchorOffset = StrapHeight * 0.5f - (StrapBandTop + StrapBandHeight * 0.5f);   // = 4 점

        public static Sprite CreateBeltStrap(string fileName)
        {
            const float half = StrapWidth * 0.5f;
            Color[] pixels = new Color[StrapWidth * StrapHeight];
            for (int y = 0; y < StrapHeight; y++)
            {
                float sy = StrapHeight - 1 - y + 0.5f;   // 그림 맨 위에서 아래로
                for (int x = 0; x < StrapWidth; x++)
                {
                    float dx = x + 0.5f - half;
                    float u = (x + 0.5f) / StrapWidth;
                    float round = Mathf.Sin(u * Mathf.PI);   // 둥글게 감겨 가운데가 앞으로 나와 밝다
                    float bend = StrapBend * Mathf.Sqrt(Mathf.Max(0f, 1f - (dx / half) * (dx / half)));
                    float strap = sy - bend - StrapBandTop;   // 0 = 띠 위 경계, 아래로 +
                    Color color = Color.clear;

                    if (strap >= 0f && strap < StrapBandHeight)
                    {
                        float v = strap / StrapBandHeight;    // 0 (위) ~ 1 (아래)
                        color = Color.Lerp(Leather, LeatherLight, Mathf.PerlinNoise(x * 0.3f + 5f, y * 0.3f + 2f) * 0.5f);
                        color = Shade(color, 0.7f + round * 0.45f);
                        if (v < 0.12f) color = Color.Lerp(color, LeatherLight, 0.6f);   // 위 가장자리 빛
                        if (v > 0.86f) color = Color.Lerp(color, LeatherDark, 0.7f);    // 아래 가장자리 그늘
                        if (Mathf.Abs(dx) > half - 3f) color = Color.Lerp(color, LeatherDark, 0.6f);   // 양옆 끝 : 탄띠로 꺾여 들어가는 그늘
                    }
                    else if (strap >= StrapBandHeight && strap < StrapBandHeight + 3f)
                    {
                        float shadow = 1f - (strap - StrapBandHeight) / 3f;   // 띠에 붙은 줄이 가장 진하다
                        color = new Color(0f, 0f, 0f, 0.45f * shadow);
                    }

                    pixels[y * StrapWidth + x] = color;
                }
            }
            return SaveSprite(fileName, StrapWidth, StrapHeight, pixels, Vector4.zero);
        }

        // 가죽 탄띠 (바탕 띠만) : 위아래 바느질. 쉘을 쥐는 고리는 따로 그린 고리 오브젝트(CreateBeltStrap)를 위에 깐다
        //   600 × 80 점 → 슬라이더에는 300 × 40 칸으로 깐다. 고리 가운데 = 가로 (i + 0.5) / BeltLoops
        public static Sprite CreateAmmoBelt(string fileName)
        {
            const int width = 600;
            const int height = 80;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // ① 가죽 바탕 : 얼룩 + 잔 주름
                    float grain = Mathf.PerlinNoise(x * 0.05f + 7f, y * 0.08f + 3f) * 0.6f
                                + Mathf.PerlinNoise(x * 0.3f + 1f, y * 0.3f + 9f) * 0.4f;
                    Color color = Color.Lerp(Leather, LeatherLight, grain * 0.5f);

                    // ② 위아래 가장자리 : 닳아서 어둡다 + 바느질 점선
                    float edge = Mathf.Min(y, height - 1 - y);
                    if (edge < 4f) color = Color.Lerp(LeatherDark, color, edge / 4f);
                    bool stitchRow = Mathf.Abs(y - 9) < 1.5f || Mathf.Abs(y - (height - 10)) < 1.5f;
                    if (stitchRow && x % 12 < 6) color = Stitch;

                    pixels[y * width + x] = color;
                }
            }
            return SaveSprite(fileName, width, height, pixels, Vector4.zero);
        }

        // 3D 나무 막대(게시판 틀 · 뒤판)에 입히는 나뭇결. 가로로 길게 흐른다 → 막대를 길이 방향으로 눕혀서 입힌다
        //   UI 가 아니라 재질(Material)에 쓰는 그림이라 Sprite 대신 Texture2D 를 돌려준다
        public static Texture2D CreateWoodGrain(string fileName)
        {
            const int width = 512;
            const int height = 128;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float warp = Mathf.PerlinNoise(x * 0.008f + 3f, y * 0.03f + 6f) * 26f;
                    float rings = Mathf.Sin((y + warp) * 0.35f) * 0.5f + 0.5f;
                    float streak = Mathf.PerlinNoise(x * 0.02f + 40f, y * 0.4f + 2f) - 0.5f;
                    Color color = Color.Lerp(WoodBase, WoodDark, Mathf.Clamp01(rings * 0.4f + streak * 0.7f));
                    pixels[y * width + x] = color;
                }
            }

            Sprite sprite = SaveSprite(fileName, width, height, pixels, Vector4.zero);
            return sprite.texture;   // Sprite 로 가져온 그림도 안쪽은 Texture2D 다
        }

        // 빨간 펜(물감)으로 대충 두 바퀴 그린 동그라미 : "이 놈을 잡는다" 는 표시
        //   흰색으로 그려 두고, 색은 Image.color 로 칠한다
        public static Sprite CreateCircleMark(string fileName)
        {
            const int size = 256;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x - size * 0.5f) / (size * 0.5f);   // -1 ~ 1
                    float v = (y - size * 0.5f) / (size * 0.5f);
                    float angle = Mathf.Atan2(v, u);

                    // 고리 두 개 : 반지름 · 가운데가 조금씩 어긋난 타원 (손으로 두 바퀴 돌린 것처럼)
                    float ink = 0f;
                    ink = Mathf.Max(ink, ScribbleRing(u, v, angle, 0.86f, 0.80f, 0.00f, 0.00f, 0.030f, 0f));
                    ink = Mathf.Max(ink, ScribbleRing(u, v, angle, 0.82f, 0.86f, 0.03f, -0.02f, 0.022f, 2.1f));

                    pixels[y * size + x] = new Color(1f, 1f, 1f, ink);
                }
            }
            return SaveSprite(fileName, size, size, pixels, Vector4.zero);
        }

        // 타원 고리 하나. 굵기를 방향마다 흔들어 붓으로 그은 것처럼 한다 (1 = 잉크)
        private static float ScribbleRing(float u, float v, float angle, float radiusU, float radiusV, float shiftU, float shiftV, float thickness, float seed)
        {
            float a = (u - shiftU) / radiusU;
            float b = (v - shiftV) / radiusV;
            float distance = Mathf.Abs(Mathf.Sqrt(a * a + b * b) - 1f) * Mathf.Min(radiusU, radiusV);   // 고리 선에서 얼마나 (u 단위)
            float width = thickness * (0.6f + Mathf.PerlinNoise(angle * 1.5f + seed + 5f, seed) * 0.8f);
            return Mathf.Clamp01((width - distance) * 128f + 0.5f);   // 128 = 그림 반 폭 (점) → 가장자리 1점만 흐리게
        }

        // ================================================================ 공통

        // 점 목록을 PNG 로 저장하고, UI 에 쓸 수 있게 Sprite 로 가져온다
        //   border : Sliced 로 늘릴 때 그대로 둘 가장자리 (왼쪽, 아래, 오른쪽, 위). 필요 없으면 Vector4.zero
        private static Sprite SaveSprite(string fileName, int width, int height, Color[] pixels, Vector4 border)
        {
            // ① 점을 그림에 채운다
            Texture2D picture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            picture.SetPixels(pixels);
            picture.Apply();

            // ② PNG 파일로 저장한다 (있으면 덮어쓴다. .meta 는 그대로라 씬의 연결이 끊기지 않는다)
            if (AssetDatabase.IsValidFolder(Folder) == false)
                AssetDatabase.CreateFolder("Assets", "30.UI");
            string path = Folder + "/" + fileName;
            File.WriteAllBytes(path, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);   // 계산용 그림은 다 썼으니 지운다 (파일은 남는다)

            // ③ 유니티에게 새 파일을 알리고, Sprite 로 가져오게 한다
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;   // 투명한 곳 테두리가 하얗게 번지지 않게
            importer.mipmapEnabled = true;         // 멀리서 볼 때 지글거리지 않게
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteBorder = border;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new Exception($"[WantedPosterArt] {path} 를 Sprite 로 불러오지 못했습니다.");
            return sprite;
        }

        // 색의 밝기만 바꾼다 (투명도는 그대로). Color * 숫자 는 투명도까지 곱해져서 따로 만들었다
        private static Color Shade(Color color, float amount)
        {
            return new Color(color.r * amount, color.g * amount, color.b * amount, color.a);
        }

        private static float Distance(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
