# 빚더미 미소녀가 떡상할 리 없잖아, 무리무리! (※무리가 아니었다?!)  — 불타는 개미 (Burning Ant)

대출받은 돈으로 주식을 해서 빚을 가장 빨리 갚는 주식 투자 로그라이크 게임.

- **기획서 (기준 문서)**: [FX_master 기획서 (GDD)](https://claude.ai/artifact/CaZEkQ359TnmqCoWacE831)
  - `Docs/GDD.md`는 참고용 사본입니다. 기획이 바뀌면 기획서를 먼저 고쳐 주세요.
- **엔진**: Unity **6000.6.4f1** (팀 전원이 같은 버전을 써야 합니다)

## 게임 실행하기

실행 파일(빌드)은 저장소 코드가 아니라 **GitHub Release**에 올라가 있습니다 (zip이 143MB라 일반 파일로는 못 올림). 둘 중 하나로 실행하세요.

**1. 빌드 zip으로 (Windows, 가장 쉬움)**
1. [Releases](https://github.com/jinnho02/burning_ant/releases/latest) 페이지에서 `BurningAnt_Windows.zip`을 내려받아 압축을 풉니다.
   (바로 받기: https://github.com/jinnho02/burning_ant/releases/latest/download/BurningAnt_Windows.zip)
2. `BurningAnt/BurningAnt.exe`를 실행합니다. 같은 폴더의 `BurningAnt_Data` 등 다른 파일과 함께 있어야 합니다.
3. "Windows의 PC 보호" 창이 뜨면 **추가 정보 → 실행**을 누릅니다 (서명 없는 게임잼 빌드라 뜹니다).
4. 전체 화면으로 시작합니다. Alt+Enter로 창 모드, Alt+F4로 종료. 권장 1920×1080 이상, Windows 10/11 64비트.

**2. 저장소에서 직접 (Unity 에디터)**
1. 아래 "처음 세팅"대로 Unity 6000.6.4f1과 Git LFS를 설치하고 저장소를 받습니다.
2. Unity Hub에서 폴더를 열고 `Assets/_Project/Scenes/DayLoop.unity`를 열어 Play.
3. 빌드하려면 File → Build Profiles → Windows → Build.
4. 에디터 Game 창에서 글씨가 계단처럼 보이면: 화면비 드롭다운의 **Low Resolution Aspect Ratios** 체크 해제, **Scale 1x**, 해상도 1920×1080.

## 게임 방법 (요약)

빚 1억을 지고 현금 1억으로 시작해, 주식으로 돈을 불려 빚을 갚는 게임입니다.

- **목표**: 빚 0원 + 자산 10억 이상이면 성공 엔딩.
- **파산**: 장 마감 정산 때 순자산(현금 + 주식 − 빚)이 −5억 밑이거나, 현금으로 1주도 못 사고 팔 주식·남은 대출이 모두 없으면 배드 엔딩.
- **하루 5단계**
  1. **아침 뉴스**: 4종목에 기사가 뜹니다. 그중 일부만 장 중에 후속 특보가 나옵니다.
  2. **아이템 · 예약**: 산 아이템을 쓰고, 장 시작 때 시가로 체결될 매수·매도를 예약합니다. 시가가 전날 종가보다 크게 불리하게 열리면 예약은 취소됩니다.
  3. **60초 장**: 실시간 매매 (Q 매수, W 매도, 1~9 아이템). 손익은 넣은 돈의 3배로 움직입니다.
     - **특보**: 아침 뉴스의 후속 기사나, 뉴스에 없던 종목의 돌발 기사가 노란 줄로 뜨고 그 순간부터 주가가 크게 움직입니다. 오르내림을 거쳐 마감 전 결과 값에 닿고, 가끔 기사와 반대로 가는 반전 결과도 있습니다 (근거는 결과창에).
  4. **결과**: 하루 매매 결과, 뉴스 결과, 이자 갚기·빚 갚기. 이자는 3일차부터 매일 붙습니다.
  5. **상점**: 매일 무작위 8개 아이템. 하루에 종류당 1개, 다시 살수록 가격이 처음의 50%씩 오릅니다.
- **대출**: 기본 대출 1억(하루 5%), 추가 대출 2억×2번(하루 30%, 다다음 날부터), 현금이 마이너스면 햇살론 최대 2억×2번(하루 30%).
- **아이템 20종**: 정보(엠바고, 애널리스트 리포트, 수상한 전화 등), 매매 보조(손절·익절 예약권 등), 대출, 도박(복권, 악마의 계약서), 시간(모래시계, 에너지 드링크).

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
- 뉴스가 걸린 종목은 `NewsGenerator`가 정한 결과대로 `PricePathGenerator`가 굴곡 있는 경로를 만듭니다.
- 폰트: 본문 Pretendard, 큰 제목 Gmarket Sans Bold (`Resources/Fonts`, SIL OFL, `KoreanFont.cs`).

## 하루 루프 데모 (Day Loop)

씬: `Assets/_Project/Scenes/DayLoop.unity` → 열고 Play. 뉴스 → 아이템 사용·장 전 예약 → 60초 장 → 정산(이자·상환·추가 대출) → 상점 → 다음 날이 끝까지 돕니다. 빚 0원 + 자산 10억이면 성공, 순자산 −5억 밑이거나 더 할 수 있는 게 없으면 파산.

- 가운데 주식 창은 위 Stock Window(`StockWindow.uxml`, `StockWindowController`)를 그대로 끼워 씁니다.
- 상점은 매일 아이템 20종 중 8개를 무작위로 진열합니다 (하루에 종류당 1개, 재구매 시 가격 인상).

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
