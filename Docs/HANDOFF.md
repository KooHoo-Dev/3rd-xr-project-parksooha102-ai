# 정오의 결투 — 작업 맥락 요약

다른 PC · 다른 Claude 세션에서 이어서 작업하기 위한 요약. (2026-11-20 기준, 브랜치 `feature/enemy-duel`, 마지막 커밋 `550c941`)

---

## 1. 프로젝트

- **정오의 결투** : Meta Quest 3 용 1:1 VR 서부 결투 게임. 정오 신호에 허리 홀스터에서 리볼버를 뽑아 적보다 먼저 맞힌다.
- 무승부면 양쪽이 0.5m 씩 다가서고(거리 1m 감소) 다시 대치. 최대 6라운드 = 리볼버 6발 (마지막 6라운드는 적 명중률 100% 라 반드시 끝난다).
- 1인 개발, 7일 일정, 학교 과제 (XRI 상호작용 2종 이상, 시작 → 플레이 → 끝, 점수 · 시간 · 승패 표시).
- Unity 6 (6000.3), XR Interaction Toolkit 3.5.1, OpenXR, Android(IL2CPP) 빌드. 에셋은 Synty POLYGON 계열.
- **기획서는 저장소 밖에 있다** : `C:\Users\VR9th\Downloads\박수하 — VR 컨셉 기획서.md` (2026-11-15). 게임 규칙 · 숫자는 기획서가 기준이다. 다른 PC 로 옮길 때 같이 챙길 것.
- 교안(강의 자료) 저장소 : `C:\Users\VR9th\Documents\GitHub\3rd-1000-Unity-Basic-2D` (`Assets\10. Scripts\`). 데이터 파이프라인(DTO → Def → Table → DataManager) 은 이 교안 방식을 따른다.

## 2. 코드 작성 규칙 (사용자 선호 — 반드시 지킬 것)

1. **교안 스타일** : 한국어 주석, ①②③ 단계 주석, 예시 행을 주석으로. 사용자가 읽고 이해할 수 있는 수준만 쓴다.
   - 제네릭 추상화 · LINQ 금지. 표마다 구체 클래스, 손으로 한 칸씩 매핑.
   - 새 API 는 아는 것에 빗대어 설명 (JArray ≈ List, JObject ≈ Dictionary).
   - 스크립트보다 Hierarchy · Inspector 설정이 되면 그쪽을 먼저. 회전 수학 대신 "부모 피벗 + Inspector 오프셋" 패턴.
2. **`!` 연산자 금지** : `!x` 대신 `x == false` (람다 안에서도). `!=` 는 괜찮다.
3. **빠른 실패(fail-fast)** : 방어적 null 체크 · "경고만 찍고 계속" 금지. 잘못된 데이터는 바로 `throw` (무엇을 고치면 되는지 메시지에 적는다).
   - null 체크는 진짜 로직일 때만 (찾기-없으면-만들기, `Resources.Load` 결과 확인 등).
   - Inspector 필드를 지우거나 대체하기 전에 씬 YAML 에서 실제로 연결돼 있는지 확인한다.
4. 설명 · 대화는 한국어.

## 3. 구조 한눈에

```
Assets/
  01.Scene/      Title.unity (메인화면, 빌드 0번) · Town.unity (결투, 빌드 1번)
  10.Script/
    GameManager/ DataManager (표 창구, BeforeSceneLoad 에 로드) · StageManager (라운드 흐름 · 방송)
                 GameSettings (Title → Town 값 전달, static + PlayerPrefs)
    Combat/      CombatManager (플레이어 총 · 홀스터 이벤트 중계) · EnemyDuelist · HitZone
                 RoundResult · ShotInfo · ScoreCalculator (점수 봉투)
    Player/      PlayerPlacer (머리 추적 대기 · 위치 초기화 · 텔레포트로 세우기)
    Props/       Gun · HolsterSlot (소켓) · HolsterAnchor (허리춤 피벗) · AdvanceMarker (전진 표식)
                 NoonSun (정오의 해) · EnemyGunHolder (적 총 손 ↔ 홀스터)
    UI/          MainMenu (Title 판) · ResultBoard (수배지 결과판) · RoundBoard (라운드 쪽지)
    Table/       EnemyDef · EnemyTable (enemies.json 수동 파싱)
    Editor/      WantedPosterLayout (판 정렬 도구) · DuelAnimationBaker · XRSimulatorSceneSetup
    Tester/      ShotTester (개발용)
  Resources/     Table/enemies.json (3 난이도 × 6 라운드) · Enemies/ · Props/
```

### 흐름
- **Title** : `MainMenu` 오브젝트 자리 = 플레이어가 설 자리. `PlayerPlacer.PlaceFirst` 로 세운다.
  - 첫 화면 [게임 시작] [홀스터 조정]
  - 게임 시작 → 난이도 Toggle 3개(Toggle Group) + [뒤로] [결투 시작] → `GameSettings.Difficulty` 넣고 Town 로드
  - 홀스터 조정 → 위쪽/아래쪽 · 앞쪽/뒤쪽 · 왼쪽/오른쪽 (2cm), [처음 자리], [빠른 조정], [뒤로]
  - 빠른 조정 → [왼손잡이]/[오른손잡이] → 홀스터가 그 컨트롤러를 따라감 → 그 손 트리거(`XRI * Interaction/Activate`, `WasPressedThisFrame`)로 확정
  - 손을 고르는 순간 `HolsterAnchor.ResetPosition()` 으로 허리춤 기준을 다시 잡는다 (판을 보고 있어 머리 방향 = 몸 정면)
- **Town** : `StageManager` 가 `GameSettings.Difficulty` 의 적을 만들고 라운드를 돈다 (Ready → Standoff → Draw → Judged).
  - 신호 전 뽑기 = 반칙(COWARD), 3초 안 쏘면 무승부, 무승부 뒤 5초 안에 안 꽂으면 자동 꽂기, 전진 표식 텔레포트로 다음 라운드
  - 결과판 [메인으로] (`StageManager.ReturnToMainMenu` → Title) · [다시 결투] (Town 다시 로드, 설정 유지)
- **홀스터 자리** : `HolsterAnchor`(머리 자리 · 수평 방향을 따르는 피벗)의 자식 `Holster` 의 `localPosition`.
  - 값은 `GameSettings.HolsterOffset` (PlayerPrefs `HolsterX/Y/Z`), 기본값 `DefaultHolsterOffset = (0.2, -0.6, 0.06)`.
  - `HolsterAnchor.Awake` 가 이 값으로 덮어쓴다 → 씬의 Holster Position 은 편집용일 뿐.
  - Town 을 바로 Play 하면 난이도는 보통.

### 판(UI) 만드는 법
- 판은 손으로 만들지 않고 `WantedPosterLayout` 에디터 도구로 만든다. 숫자를 고치고 ⋮ 메뉴를 다시 누르는 방식.
  - Town `ResultBoard` ⋮ → **수배지 모양으로 정렬** / `RoundBoard` ⋮ → **쪽지 모양으로 정렬**
    - 쪽지 정렬은 셋을 만든다 : 무승부 탄피 카드(나무 판 680 × 380 + 3D 틀 위에 핀으로 박은 쪽지, 실린더 6칸, 쏜 칸은 빔) · 홀스터 칸 아래 `HolsterGlow`(Unlit 덧칠 구, 총을 꽂을 때까지 깜빡) · AdvanceMarker 아래 `Sign`(나무 말뚝 + "한 걸음 앞으로" 3D 글자)
    - RoundBoard 는 무승부만 띄운다 (승패 · 반칙 라운드는 쪽지 없이 결과판으로)
  - Title `MainMenu` ⋮ → **메인화면 판 정렬** : Panel 아래를 비우고 다섯 화면을 처음부터 다시 만든 뒤 칸 21개 + 컨트롤러 · 트리거를 자동 연결
- Panel 은 `우클릭 > XR > UI Canvas` 로 직접 만들어 이름을 `Panel` 로 (레이로 누르는 Raycaster 때문).
- 그림은 `WantedPosterArt` 가 정렬할 때마다 코드로 그려 `Assets/30.UI` 에 PNG 로 덮어쓴다 : 종이(Poster/Note/MenuPaper) · WoodSign(버튼) · Nail · BulletHole · StampFrame · `Montage_Easy/Normal/Hard/Coward`.
  - 몽타주 원본은 `Assets/30.UI/MontageSource/MontageSource_{Easy,Normal,Hard}.png` (초록 배경 얼굴 스샷, 360×360, 얼굴 폭 40% · 눈 높이 위에서 45% 로 맞춰 자름). 정렬 때 초록을 빼고 판화풍으로 다시 그려 ResultBoard 의 Portrait 칸에 넣는다.
  - 메인화면 = 나무 현상금 게시판 (판자 그림 1000 × 760, BountyBoard.png). 몸통은 3D : Panel 자식 큐브 BoardBack(뒤판) + FrameTop/Bottom/Left/Right(앞으로 2.8cm 튀어나온 틀, 폭 5cm). 재질 BoardFrame.mat · BoardBack.mat (URP Lit + WoodGrain.png), 충돌체 없음(레이 방해 방지). 맨 위 나무 간판 "정오의 결투".
    - 첫 화면 : 작은 수배지 셋(쉬움 · 보통 · 어려움, 구경용) · 오른쪽 쪽지 버튼(결투 신청 / 홀스터 조정) · 아래 공고(결투 규칙)
    - 게임 시작 화면 : 같은 수배지 셋이 곧 난이도 Toggle. 고르면 몽타주에 빨간 동그라미(CircleMark.png). 오른쪽 쪽지 결투 시작 / 뒤로
    - 홀스터 · 빠른 조정 화면 : 게시판에 붙인 큰 종이(Sheet) 위에 기존 버튼들, 뒤로는 오른쪽 쪽지
    - 종이마다 그림자(같은 그림 반투명 검정) + 핀(Nail.png 축소). 종이는 위쪽 가운데(핀 자리) 기준으로 조금씩 기울였다
    - 지금 매칭 : Easy = 카우보이 모자 · 조끼 · 빨간 스카프 / Normal = 깃털 모자 · 판초 / Hard = 검은 모자 · 검은 수염.
  - 결과판 = 나무 판(720 × 1180, ResultBoardPlanks.png + 3D 뒤판 · 틀, PlaceBoardModel 공용)에 수배지(Poster, 600 × 980)를 못으로 박은 모양. 수배지 내용은 모두 Poster 의 자식, 버튼 둘은 판 아래쪽(ButtonTop)에.
  - 수배지 내용 : WANTED → DEAD OR ALIVE → 몽타주 → 이름(enemies.json `Name`) → 내역 → REWARD. 결과는 몽타주 위 빨간 도장(DEAD / AT LARGE / COWARD), 이기면 맞힌 부위에 총알 구멍. 반칙이면 실루엣 + "바로 당신".

## 4. 지금 상태 · 남은 일

**확인 안 된 것 (가장 먼저 할 일)**
- [ ] Title 씬에서 MainMenu ⋮ **메인화면 판 정렬** 다시 누르고 저장 (빠른 조정 추가 후 아직 안 눌렀을 수 있음 — 칸 이름이 바뀌어 안 누르면 시작 시 에러)
- [ ] Town 씬에서 ResultBoard ⋮ **수배지 모양으로 정렬** 다시 누르고 저장 (`mainMenuButton` 칸)
- [ ] Build Profiles 에 Title 이 0번인지 확인
- [ ] Play 테스트 : Title → 난이도 → 결투 → 메인으로 / 다시 결투, 홀스터 버튼 · 빠른 조정 (시뮬레이터 : `]` 오른손 선택, `T` 트리거)

**알려진 한계**
- 빠른 조정은 홀스터 "중심"을 컨트롤러로 옮긴다. 총이 꽂히는 GunSlot 은 몇 cm 떨어져 있어 버튼으로 다듬어야 할 수 있다.
- 손 따라가기 화면엔 취소 버튼이 없다 (주 손 트리거가 버튼 누르기 대신 확정이 되므로 일부러 뺐다).

**기획서 일정상 남은 것** : 7일차 플레이테스트 · 밸런스(enemies.json 의 Easy/Hard 값은 임시값) · 빌드 · 시연 영상. 기획서 8장의 상태별 인터랙터 켜고 끄기는 아직 전부 구현되진 않음.

**저장소 메모**
- `Assets/Synty/PolygonGeneric/` 아래 미추적 파일 약 800개는 이번 작업과 무관해서 커밋하지 않았다. 다른 PC 에서는 Synty 패키지를 다시 가져와야 할 수 있다.
- 아직 push 안 함.

## 5. 유니티 없이 컴파일 확인하는 법

Unity 가 생성한 `Assembly-CSharp.csproj` 의 `<HintPath>` · `<Compile Include>` · `<DefineConstants>` 를 모아 응답 파일(.rsp)을 만들고 Roslyn 으로 직접 컴파일했다.

```
dotnet "C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore\csc.dll" @runtime.rsp
```
- 옵션 : `-target:library -nostdlib+ -langversion:9.0 -out:<임시폴더>`
- csproj 에 아직 없는 새 .cs 파일은 직접 목록에 더한다. 에디터 어셈블리는 방금 만든 Assembly-CSharp.dll 을 참조해서 따로 컴파일.
- 프로젝트 C# 버전은 9.0 이다.
