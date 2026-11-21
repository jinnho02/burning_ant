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
│   └── UI/          화면 UI
├── Data/            ScriptableObject 데이터
│   ├── Stocks/      종목 (이름, 성향, 등락 한도)
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

## 협업 규칙

- **브랜치**: `main`에는 직접 올리지 않고 작업 브랜치 → Pull Request로 합칩니다.
  - 이름: `feature/기능`, `fix/버그`, `art/에셋` (예: `feature/loan-system`)
- **씬**: 같은 씬을 두 사람이 동시에 고치면 충돌이 잘 납니다. 씬 담당을 정하고, 나머지는 프리팹으로 작업합니다.
- **.meta 파일**: 에셋과 `.meta`는 항상 같이 커밋합니다. `.meta`가 빠지면 다른 사람 프로젝트에서 연결이 깨집니다.
- **큰 파일**: 이미지·사운드·폰트는 Git LFS로 자동 처리됩니다 (`.gitattributes`). LFS 설치를 꼭 해 주세요.
- **코드 스타일**: `.editorconfig` 기준 (UTF-8, 스페이스 4칸).
- **숫자 밸런스**: 코드에 직접 쓰지 말고 `Data/`의 ScriptableObject에 둡니다. 기획자가 코드 없이 조정할 수 있게 하려는 것입니다.
