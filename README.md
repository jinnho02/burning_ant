# 빚더미 미소녀가 떡상할 리 없잖아, 무리무리! (※무리가 아니었다?!)  — 불타는 개미 (Burning Ant)

대출받은 돈으로 주식을 해서 빚을 가장 빨리 갚는 주식 투자 로그라이크 게임.

- **기획서 (기준 문서)**: [FX_master 기획서 (GDD)](https://claude.ai/artifact/CaZEkQ359TnmqCoWacE831)
  - `Docs/GDD.md`는 참고용 사본입니다. 기획이 바뀌면 기획서를 먼저 고쳐 주세요.
- **엔진**: Unity **6000.6.4f1** (팀 전원이 같은 버전을 써야 합니다)

## 처음 세팅

1. Unity Hub에서 **6000.6.4f1** 설치
2. Git LFS 설치 후 한 번만 실행
   ```
   git lfs install
   ```
3. 저장소 받기
   ```
   git clone https://github.com/jinnho02/burning_ant.git
   ```
4. Unity Hub → Add → 받은 폴더 선택 → 열기 (처음 열 때 Library 생성에 몇 분 걸림)
5. (선택) 씬·프리팹 충돌 병합 도구 등록. 경로는 각자 설치 위치에 맞게
   ```
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
   ```

## 빌드 (Unity를 열지 않고 실행 파일 만들기)

1. 이 프로젝트를 연 Unity 에디터가 있으면 닫습니다 (열려 있으면 빌드가 실패해요).
2. 프로젝트 폴더의 **`build.bat`** 을 더블클릭합니다. 처음엔 몇 분, 그다음부턴 1~2분 걸려요.
   - 빌드 전에 지금 브랜치를 **GitHub 최신 버전으로 자동 업데이트**합니다 (`git pull` + 캐릭터 그림 `git lfs pull`).
   - 내가 고친 파일이 있으면 덮어쓰지 않도록 업데이트를 건너뛰고 지금 파일로 빌드해요. 내 브랜치에만 있는 커밋이 있거나 인터넷이 안 될 때도 건너뛰어요.
   - 업데이트 없이 빌드하려면: `powershell -ExecutionPolicy Bypass -File build.ps1 -NoUpdate`
3. **`Builds\Windows\FX_master.exe`** 를 실행합니다. 친구에게 줄 때는 `Builds\Windows` 폴더를 통째로 압축해서 보내세요 (exe만 보내면 실행되지 않아요).

- Unity 6000.6.4f1이 기본 위치(`C:\Program Files\Unity\Hub\Editor\`)에 있어야 합니다. 다른 곳에 있으면 환경 변수 `UNITY_PATH`에 Unity.exe 경로를 넣으세요.
- 빌드 설정(Build Profiles)에 켜진 씬이 순서대로 들어가고, 첫 번째 씬(현재 DayLoop)으로 시작합니다.
- 실패하면 `Logs\build.log`를 보세요. 에디터 메뉴 **BurningAnt > Build > Windows**로도 같은 빌드를 할 수 있어요.
- GitHub에서 zip으로 받으면 캐릭터 그림(Git LFS)이 빠집니다. 꼭 `git clone`으로 받아서 빌드하세요.

## 폴더 구조

우리 팀 파일은 전부 `Assets/_Project/` 아래에 둡니다. 에셋 스토어나 외부 패키지와 섞이지 않게 하려는 것입니다.

```
Assets/_Project/
├── Scenes/          씬
├── Scripts/
│   ├── Core/        하루 루프, 게임 상태, 저장
│   ├── Market/      주가 생성, 차트, 매매
│   ├── News/        뉴스 생성, 연쇄 뉴스
│   ├── Items/       아이템, 상점
│   ├── Loan/        대출, 이자, 상환
│   ├── Character/   주인공 반신 캠 (옷·표정)
│   ├── UI/          화면 컨트롤러 (C#)
│   └── Editor/      에디터 전용: 셋업 메뉴, 자체 점검
├── UI/              UI Toolkit 레이아웃(.uxml)·스타일(.uss), PanelSettings
├── Data/            ScriptableObject 데이터
│   ├── MarketSettings.asset  장 시간·수수료·등락 한도 등 밸런스
│   ├── Stocks/      종목 (이름, 성향, 시작가)
│   ├── Items/       아이템 (가격, 효과, 정확도)
│   └── News/        뉴스 문구 템플릿
├── Prefabs/
├── Art/
│   ├── Character/   주인공 (옷 5단계, 표정 5종)
│   └── UI/
├── Audio/
└── Fonts/
Docs/                기획 사본 등 문서
```

## 주식 창 (Stock Window)

씬: `Assets/_Project/Scenes/StockWindow.unity` → 열고 Play.

| 바꾸고 싶은 것 | 고칠 곳 |
|---|---|
| 배치·요소 추가 | `UI/StockWindow/StockWindow.uxml` (UI Builder로 열어도 됨) |
| 색·크기·여백 (상승·하락 색 포함) | `UI/StockWindow/StockWindow.uss` 맨 위 변수 |
| 장 시간, 수수료, 등락 한도, 가격 움직임 | `Data/MarketSettings.asset` (인스펙터) |
| 종목 이름·성향·시작가, 종목 추가 | `Data/Stocks/` 에셋, 씬의 `StockWindow` 오브젝트 → MarketSession → Stocks 목록 |
| 매매·예약·장 진행 규칙 | `Scripts/Market/` (`Portfolio`, `MarketSession`) |
| 화면 갱신·버튼 동작 | `Scripts/UI/StockWindowController.cs` |

구조 (UI는 규칙을 모르고, 규칙은 UI를 모름):

```
MarketSession (장 진행: 예약 → 장 시작 시 시가 체결 → 틱 → 마감 → 다음 날)
 ├─ StockState × N  (전일 종가, 오늘 가격 경로)  ← PricePathGenerator
 └─ Portfolio       (현금, 보유, 매수·매도·수수료·최대 4종목)
        ▲ 이벤트(PhaseChanged, Ticked …)          │ Reserve / Trade
        │                                          ▼
StockWindowController (UXML 요소 찾기, 화면 갱신, 버튼 → 주문)
 ├─ StockListItem      왼쪽 종목 한 줄
 └─ PriceChartElement  라인 차트 (USS --chart-* 색)
```

- 에디터 메뉴 **BurningAnt > Setup > Stock Window Scene**: 씬과 기본 에셋을 다시 만듭니다 (이미 있는 에셋은 그대로 둠).
- 에디터 메뉴 **BurningAnt > Self Check > Market**: 매매·가격 로직 점검. 로직을 고친 뒤 실행해 주세요.
- 가격은 아직 뉴스와 무관한 무작위입니다 (`PricePathGenerator`의 `shortcut:` 주석 참고).
- 한글 폰트는 지금 OS 폰트(맑은 고딕)를 빌려 씁니다. 출시 전에 배포 가능한 폰트로 바꿔야 합니다 (`KoreanFont.cs`).

## 하루 루프 데모 (Day Loop)

씬: `Assets/_Project/Scenes/DayLoop.unity` → 열고 Play. 뉴스 → 아이템 사용·장 전 예약 → 60초 장 → 정산(이자·상환·추가 대출) → 상점 → 다음 날이 끝까지 돕니다. 빚을 다 갚으면 승리, 총자산 $100 미만 + 추가 대출 소진이면 파산.

- 가운데 주식 창은 위 Stock Window(`StockWindow.uxml`, `StockWindowController`)를 그대로 끼워 씁니다.
- **기획서와 다른 점 (팀 합의 변경)**: 상점은 매일 랜덤 4개가 아니라 아이템 9종을 전부 진열하고, 플레이어가 골라 사서 씁니다.

| 바꾸고 싶은 것 | 고칠 곳 |
|---|---|
| 화면 배치·색 (위 막대, 뉴스·힌트, 반신 캠, 아이템 막대, 정산·상점 창) | `UI/GameScreen/GameScreen.uxml`, `GameScreen.uss` |
| 빚·이자·추가 대출·파산 기준 | `Data/LoanSettings.asset` |
| 뉴스 개수·폭·연쇄 확률, 뉴스 문구 | `Data/NewsSettings.asset` |
| 아이템 이름·가격·정확도·지속 시간, 상점 진열 | `Data/Items/` 에셋, 씬의 `Game` 오브젝트 → GameLoop → Shop Items |
| 옷 단계·표정 경계값 | `Data/CharacterSettings.asset` |
| 단계 진행·아이템 효과·승패 규칙 | `Scripts/Core/GameLoop.cs` |
| 화면 갱신·버튼 | `Scripts/UI/GameScreenController.cs` |

```
GameLoop (뉴스 → 아이템 → 장 → 정산 → 상점, 대출·아이템·승패)
 ├─ NewsGenerator  뉴스·연쇄 뉴스 생성, 뉴스를 반영해 오늘 가격 경로 확정
 ├─ LoanAccount    빚·이자·추가 대출·상환
 ├─ Inventory      산 아이템
 └─ MarketSession  (기존) 장 진행·매매
GameScreenController (위 막대, 뉴스·힌트, 반신 캠, 아이템 막대, 단계별 창)
StockWindowController (기존) 가운데 주식 창
```

- 에디터 메뉴 **BurningAnt > Setup > Day Loop Scene**: 씬과 데이터 에셋을 다시 만듭니다 (이미 있는 에셋은 그대로 둠).
- 에디터 메뉴 **BurningAnt > Self Check > Day Loop**: 대출·이자·뉴스 로직 점검.
- 반신 캠은 그림이 나오기 전까지 글자 얼굴로 대신합니다 (`CharacterSettings.MoodFace`의 `shortcut:` 주석).

## 협업 규칙

- **브랜치**: `main`에는 직접 올리지 않고 작업 브랜치 → Pull Request로 합칩니다.
  - 이름: `feature/기능`, `fix/버그`, `art/에셋` (예: `feature/loan-system`)
- **씬**: 같은 씬을 두 사람이 동시에 고치면 충돌이 잘 납니다. 씬 담당을 정하고, 나머지는 프리팹으로 작업합니다.
- **.meta 파일**: 에셋과 `.meta`는 항상 같이 커밋합니다. `.meta`가 빠지면 다른 사람 프로젝트에서 연결이 깨집니다.
- **큰 파일**: 이미지·사운드·폰트는 Git LFS로 자동 처리됩니다 (`.gitattributes`). LFS 설치를 꼭 해 주세요.
- **코드 스타일**: `.editorconfig` 기준 (UTF-8, 스페이스 4칸).
- **숫자 밸런스**: 코드에 직접 쓰지 말고 `Data/`의 ScriptableObject에 둡니다. 기획자가 코드 없이 조정할 수 있게 하려는 것입니다.
