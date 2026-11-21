using System.Linq;
using BurningAnt.Character;
using BurningAnt.Core;
using BurningAnt.Items;
using BurningAnt.Market;
using BurningAnt.News;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// GameScreen.uxml과 GameLoop를 연결한다. 가운데 주식 창은 StockWindowController가 그대로 맡고,
    /// 여기서는 위 막대(단계·빚 상환) · 뉴스 띠 · 상태창 · 주인공 · 아이템 칸 · 단계별 창(정산, 상점, 결과)만 다룬다.
    /// 디자인 기준: Docs/ui-mockup.png
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameScreenController : MonoBehaviour
    {
        [SerializeField] GameLoop game;

        [Header("주인공 그림")]
        [SerializeField, Range(0.5f, 4f), Tooltip("주인공 그림 크기 배율 (1 = 오른쪽 아래 영역에 꽉 맞춤). 1보다 크면 영역 밖으로 위·왼쪽으로 커진다")]
        float characterScale = 1f;

        [Header("경고창 · 프롤로그")]
        [SerializeField, Tooltip("켜면 이미 본 적이 있어도 타이틀 뒤에 프롤로그를 다시 보여준다 (테스트용)")] bool alwaysShowPrologue;
        [SerializeField, Tooltip("프롤로그 컷이 저절로 넘어가는 시간(초). 0이면 클릭할 때만")] float prologueCutSeconds = 2.5f;

        [Header("튜토리얼")]
        [SerializeField, Range(0.5f, 3f), Tooltip("튜토리얼 설명 창과 글자 크기 배율")] float tutorialScale = 2f;
        [SerializeField, Tooltip("비우면 임시 문구(TutorialScript.CreateDefault)를 쓴다")] TutorialScript tutorialScript;
        [SerializeField, Tooltip("켜면 이미 본 적이 있어도 1일차에 튜토리얼을 다시 띄운다 (테스트용)")] bool alwaysShowTutorial;

        [Header("타이틀 '클릭하여 시작하기' 위치 (Play 중에 고쳐도 바로 반영)")]
        [SerializeField, Range(0f, 1f), Tooltip("글자 윗변 높이. 타이틀 그림 높이 대비 비율 (0 = 그림 맨 위, 1 = 맨 아래)")]
        float titleTextTop = 0.77f;
        [SerializeField, Range(0f, 1f), Tooltip("글자 가로 가운데. 타이틀 그림 폭 대비 비율 (0 = 왼쪽 끝, 1 = 오른쪽 끝)")]
        float titleTextCenterX = 0.765f;
        [SerializeField, Range(0.01f, 0.1f), Tooltip("글자 크기. 타이틀 그림 높이 대비 비율")]
        float titleTextSize = 0.026f;

        MarketSession Session => game.Session;
        StockWindowController stockWindow;

        Label dayLabel, repayLabel, cashLabel, stockValueLabel, interestLabel, loanCountLabel, netWorthLabel;
        readonly Label[] steps = new Label[5];
        VisualElement repayFill, itemBarList;
        ScrollView hintList;
        VisualElement hintCard;
        Label faceLabel, speechLabel, moodLabel, itemBarHint;
        VisualElement newsBand, newsBandList;
        Label newsBandTitle;
        Button newsConfirmButton, buyButton, sellButton, startButton;
        VisualElement pickerOverlay, settlementOverlay, shopOverlay, endOverlay, emergencyOverlay;
        Label emergencyBody;
        Button loanButton;
        Label pickerTitle, settlementTitle, settlementSummary, settlementMessage, shopCash, shopMessage;
        VisualElement loanOverlay, interestBox, endingStage, endingEnd, endingFinal;
        FloatField loanField;
        Label loanBody, interestDueLabel, endingAmount, endingHint, endingSummary;
        Button interestPayButton;
        int endingIndex;
        float autoTimer;
        GamePhase autoPhase = GamePhase.GameOver;
        VisualElement telescopePanel, alarmOverlay;
        Label telescopeTitle;
        PriceChartElement telescopeChart;
        float alarmTimer;
        VisualElement pickerList;
        FloatField repayField;
        PriceChartElement chart;
        VisualElement portrait, characterArea;
        float appliedCharacterScale = -1f;
        string lastMessage = "";
        float hintTimer;
        VisualElement titleOverlay;
        VisualElement warningOverlay, prologueOverlay, prologueStage;
        readonly VisualElement[] prologueCovers = new VisualElement[3];
        int prologuePage, prologueCut;
        float prologueTimer, warningShownAt;
        static bool warningShownThisRun; // 경고창은 실행할 때 한 번만 (다시 하기로 씬을 다시 열면 생략)
        static bool prologueShownThisRun;
        VisualElement shopStage, shopGrid, shopTooltip;
        Label shopTipName, shopTipTag, shopTipDesc, shopTipPrice, shopTipOwned;
        ItemDefinition hoveredShopItem;
        VisualElement newsOverlay;
        ScrollView newsPaperList;
        Label newsPaperDate;
        Label titleStart;
        float titleBlinkTimer;
        VisualElement flowCard, flowList, sideNewsList;
        ScrollView settlementReport;
        TutorialRunner tutorial;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            KoreanFont.Apply(root);
            // 5영역 배치: 종목 목록을 주식 창에서 꺼내 왼쪽 전체 높이 영역으로 옮긴다 (StockWindow.uxml은 그대로).
            // 이름으로 찾기 때문에 StockWindowController는 옮긴 뒤에도 그대로 동작한다.
            var stockListPanel = root.Q("stock-list-panel");
            var leftColumn = root.Q("left-column");
            if (stockListPanel != null && leftColumn != null) leftColumn.Add(stockListPanel);
            stockWindow = GetComponent<StockWindowController>();
            Query(root);
            Bind(root);

            // 뉴스 띠는 차트 바로 위(주식 창 오른쪽 카드 맨 위)에 둔다
            root.Q("detail-panel")?.Insert(0, newsBand);
            BindTitle(root);
            BindIntro(root);
            tutorial = new TutorialRunner(root, game, tutorialScript != null ? tutorialScript : TutorialScript.CreateDefault(), alwaysShowTutorial, tutorialScale);
            Session.ReservationsChanged += RefreshFlow;

            game.Changed += RefreshAll;
            game.PhaseChanged += OnPhaseChanged;
            game.Message += OnMessage;
            gameObject.AddComponent<GameAudio>().Init(game, root);
            telescopePanel = root.Q("telescope-panel");
            telescopeTitle = root.Q<Label>("telescope-title");
            telescopeChart = root.Q<PriceChartElement>("telescope-chart");
            alarmOverlay = root.Q("alarm-overlay");
            game.AlarmFired += () => alarmTimer = 3f;
            // 시연용 자동 진행 중에는 창이 뒤에 있어도 계속 돌게 (평소 실행은 그대로: 창을 내리면 멈춤)
            if (Prologue.PrologueController.AutoplayArg) Application.runInBackground = true;
            game.Inventory.Changed += RefreshItemBar;
            Session.Portfolio.Changed += RefreshAll;
            if (stockWindow != null) stockWindow.SelectionChanged += RefreshNews;

            OnPhaseChanged();
            // 종목 리스트는 StockWindowController.Start가 만든다. 실행 순서가 정해져 있지 않아 한 프레임 뒤 다시 표시한다.
            root.schedule.Execute(RefreshNews);
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.Changed -= RefreshAll;
            game.PhaseChanged -= OnPhaseChanged;
            game.Message -= OnMessage;
            if (game.Inventory != null) game.Inventory.Changed -= RefreshItemBar;
            if (Session != null && Session.Portfolio != null) Session.Portfolio.Changed -= RefreshAll;
            if (stockWindow != null) stockWindow.SelectionChanged -= RefreshNews;
            if (Session != null) Session.ReservationsChanged -= RefreshFlow;
        }

        void Update()
        {
            if (game.Inventory == null) return;
            BlinkTitle();
            LayoutCharacter(false);
            UpdateIntro();
            tutorial?.Update(IntroShowing());
            AutoPlay();
            UpdateAlarm();
            RefreshStatus();
            HandleItemKeys();
            if (game.Phase == GamePhase.Market)
            {
                UpdateTelescope();
                hintTimer -= Time.unscaledDeltaTime;
                if (hintTimer <= 0f)
                {
                    hintTimer = 0.2f; // 효과 남은 시간 표시
                    RefreshHints();
                }
            }
        }

        void Query(VisualElement root)
        {
            dayLabel = root.Q<Label>("day-label");
            for (int i = 0; i < steps.Length; i++) steps[i] = root.Q<Label>($"step-{i + 1}");
            repayLabel = root.Q<Label>("repay-label");
            repayFill = root.Q("repay-fill");

            cashLabel = root.Q<Label>("cash-label");
            stockValueLabel = root.Q<Label>("stock-value-label");
            interestLabel = root.Q<Label>("interest-label");
            loanCountLabel = root.Q<Label>("loan-count-label");
            netWorthLabel = root.Q<Label>("networth-label");
            hintList = root.Q<ScrollView>("hint-list");
            hintCard = root.Q("hint-card");
            sideNewsList = root.Q("side-news-list");
            flowCard = root.Q("flow-card");
            flowList = root.Q("flow-list");
            settlementReport = root.Q<ScrollView>("settlement-report");

            portrait = root.Q("portrait");
            characterArea = root.Q("character-area");
            characterArea?.RegisterCallback<GeometryChangedEvent>(_ => LayoutCharacter(true));
            faceLabel = root.Q<Label>("face-label");
            speechLabel = root.Q<Label>("speech-label");
            moodLabel = root.Q<Label>("mood-label");

            itemBarList = root.Q("item-bar-list");
            itemBarHint = root.Q<Label>("item-bar-hint");
            chart = root.Q<PriceChartElement>("chart");
            buyButton = root.Q<Button>("buy-button");
            sellButton = root.Q<Button>("sell-button");
            startButton = root.Q<Button>("start-button");

            newsBand = root.Q("news-band");
            newsBandTitle = root.Q<Label>("news-band-title");
            newsBandList = root.Q("news-band-list");
            newsConfirmButton = root.Q<Button>("news-confirm-button");
            newsOverlay = root.Q("news-overlay");
            newsPaperList = root.Q<ScrollView>("news-paper-list");
            newsPaperDate = root.Q<Label>("news-paper-date");

            pickerOverlay = root.Q("picker-overlay");
            pickerTitle = root.Q<Label>("picker-title");
            pickerList = root.Q("picker-list");

            emergencyOverlay = root.Q("emergency-overlay");
            emergencyBody = root.Q<Label>("emergency-body");
            loanButton = root.Q<Button>("loan-button");
            settlementOverlay = root.Q("settlement-overlay");
            settlementTitle = root.Q<Label>("settlement-title");
            settlementSummary = root.Q<Label>("settlement-summary");
            settlementMessage = root.Q<Label>("settlement-message");
            repayField = root.Q<FloatField>("repay-field");

            shopOverlay = root.Q("shop-overlay");
            shopStage = root.Q("shop-stage");
            shopGrid = root.Q("shop-grid");
            shopTooltip = root.Q("shop-tooltip");
            shopTipName = root.Q<Label>("shop-tip-name");
            shopTipTag = root.Q<Label>("shop-tip-tag");
            shopTipDesc = root.Q<Label>("shop-tip-desc");
            shopTipPrice = root.Q<Label>("shop-tip-price");
            shopTipOwned = root.Q<Label>("shop-tip-owned");
            shopOverlay.RegisterCallback<GeometryChangedEvent>(_ => FitToImage(shopStage, shopOverlay, StoreAspect));
            shopCash = root.Q<Label>("shop-cash");
            shopMessage = root.Q<Label>("shop-message");

            endOverlay = root.Q("end-overlay");
            endingStage = root.Q("ending-stage");
            endingAmount = root.Q<Label>("ending-amount");
            endingHint = root.Q<Label>("ending-hint");
            endingEnd = root.Q("ending-end");
            endingSummary = root.Q<Label>("ending-summary");
            endingFinal = root.Q("ending-final");
            endOverlay.RegisterCallback<GeometryChangedEvent>(_ => LayoutEnding());
            endOverlay.RegisterCallback<ClickEvent>(_ => AdvanceEnding());

            loanOverlay = root.Q("loan-overlay");
            loanField = root.Q<FloatField>("loan-field");
            loanBody = root.Q<Label>("loan-body");
            interestBox = root.Q("interest-box");
            interestDueLabel = root.Q<Label>("interest-due-label");
            interestPayButton = root.Q<Button>("interest-pay-button");
        }

        void Bind(VisualElement root)
        {
            newsConfirmButton.clicked += game.ConfirmNews;
            root.Q<Button>("news-paper-button").clicked += game.ConfirmNews;
            root.Q<Button>("picker-cancel-button").clicked += () => Show(pickerOverlay, false);
            root.Q<Button>("repay-button").clicked += () => game.Repay(repayField.value * 10000f); // 입력은 만원 단위
            root.Q<Button>("repay-all-button").clicked += () => game.Repay(Session.Portfolio.Cash);
            loanButton.clicked += OpenLoan;
            root.Q<Button>("loan-cancel-button").clicked += CloseLoan;
            root.Q<Button>("loan-confirm-button").clicked += () =>
            {
                CloseLoan();
                game.TakeExtraLoan(loanField.value);
            };
            interestPayButton.clicked += game.PayInterest;
            root.Q<Button>("emergency-accept-button").clicked += game.AcceptEmergencyLoan;
            root.Q<Button>("emergency-decline-button").clicked += game.DeclineEmergencyLoan;
            root.Q<Button>("to-shop-button").clicked += game.GoToShop;
            root.Q<Button>("next-day-button").clicked += game.StartNextDay;
            root.Q<Button>("restart-button").clicked += () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(gameObject.scene.name); // 씬이 Build Profiles 목록에 있어야 한다 (셋업 메뉴가 추가)
            };
        }

        // ───────── 타이틀 ─────────

        /// <summary>
        /// 시작할 때 타이틀이 맨 위에 덮여 있고, 아무 곳이나 클릭하면 닫혀 1일차 뉴스가 보인다.
        /// 프롤로그를 아직 안 봤으면 클릭 시 프롤로그 씬으로 가고, 프롤로그가 끝나 돌아오면 타이틀을 건너뛴다.
        /// </summary>
        void BindTitle(VisualElement root)
        {
            titleOverlay = root.Q("title-overlay");
            titleStart = root.Q<Label>("title-start");
            if (titleOverlay == null) return;
            if (Prologue.PrologueController.ReturnedFromPrologue)
            {
                Prologue.PrologueController.ReturnedFromPrologue = false;
                Show(titleOverlay, false);
                return;
            }
            titleOverlay.BringToFront();
            titleOverlay.RegisterCallback<ClickEvent>(_ => OnTitleClicked());
            titleOverlay.RegisterCallback<GeometryChangedEvent>(_ => LayoutTitle());
            if (Prologue.PrologueController.AutoplayArg) titleOverlay.schedule.Execute(OnTitleClicked).ExecuteLater(2000); // 시연용
        }

        /// <summary>
        /// 타이틀 클릭 → (처음 한 번) 그림 프롤로그 → 1일차 뉴스 + 튜토리얼.
        /// 예전 글자 프롤로그 씬(Prologue.unity)은 그림 프롤로그로 바뀌어 여기서 부르지 않는다.
        /// </summary>
        void OnTitleClicked()
        {
            Show(titleOverlay, false);
            // 실행할 때마다 한 번 (다시 하기로 씬을 다시 열면 생략)
            if (alwaysShowPrologue || !prologueShownThisRun) StartPrologue();
        }

        // ───────── 경고창 · 프롤로그 ─────────

        // 프롤로그 그림(1672×941)에서 컷 3개 자리 (left, top, right, bottom 비율). 가림막이 이 자리를 덮었다가 하나씩 걷힌다
        const float PrologueAspect = 1672f / 941f;
        static readonly float[][,] PrologueCuts =
        {
            new float[,] { { 0f, 0f, 1f, 0.505f }, { 0f, 0.505f, 0.503f, 1f }, { 0.503f, 0.505f, 1f, 1f } },       // 1장: 위 1컷, 아래 2컷
            new float[,] { { 0f, 0f, 0.484f, 1f }, { 0.484f, 0f, 0.778f, 1f }, { 0.778f, 0f, 1f, 1f } },           // 2장: 세로 3컷
        };

        void BindIntro(VisualElement root)
        {
            warningOverlay = root.Q("warning-overlay");
            prologueOverlay = root.Q("prologue-overlay");
            prologueStage = root.Q("prologue-stage");
            for (int i = 0; i < 3; i++) prologueCovers[i] = root.Q($"prologue-cover-{i}");
            if (prologueOverlay != null)
            {
                prologueOverlay.RegisterCallback<ClickEvent>(_ => AdvancePrologue());
                prologueOverlay.RegisterCallback<GeometryChangedEvent>(_ => FitToImage(prologueStage, prologueOverlay, PrologueAspect));
                root.Q<Button>("prologue-skip").clicked += EndPrologue;
            }

            // 실행하면 경고창이 가장 먼저 (타이틀보다 위)
            if (warningOverlay != null && !warningShownThisRun && titleOverlay != null && titleOverlay.resolvedStyle.display != DisplayStyle.None)
            {
                warningShownThisRun = true;
                warningShownAt = Time.unscaledTime;
                Show(warningOverlay, true);
                warningOverlay.BringToFront();
                warningOverlay.RegisterCallback<ClickEvent>(_ =>
                {
                    if (Time.unscaledTime - warningShownAt < 0.8f) return; // 실수로 바로 넘기지 않게
                    Show(warningOverlay, false);
                });
            }
        }

        bool IntroShowing() =>
            new[] { titleOverlay, warningOverlay, prologueOverlay }.Any(o => o != null && o.resolvedStyle.display != DisplayStyle.None);

        void StartPrologue()
        {
            if (prologueOverlay == null) return;
            prologuePage = 0;
            Show(prologueOverlay, true);
            prologueOverlay.BringToFront();
            ShowProloguePage();
        }

        void ShowProloguePage()
        {
            var cuts = PrologueCuts[prologuePage];
            // 가림막은 걷힐 때만 서서히(0.45초). 새 장으로 바꿀 때 서서히 덮이면 그 사이 그림 전체가 잠깐 보이므로 즉시 덮는다
            for (int i = 0; i < 3; i++)
            {
                var c = prologueCovers[i];
                c.AddToClassList("prologue-cover--instant");
                c.schedule.Execute(() => c.RemoveFromClassList("prologue-cover--instant")).ExecuteLater(100);
            }
            prologueStage.EnableInClassList("prologue-page--1", prologuePage == 0);
            prologueStage.EnableInClassList("prologue-page--2", prologuePage == 1);
            for (int i = 0; i < 3; i++)
            {
                var c = prologueCovers[i];
                c.style.left = Length.Percent(cuts[i, 0] * 100f);
                c.style.top = Length.Percent(cuts[i, 1] * 100f);
                c.style.width = Length.Percent((cuts[i, 2] - cuts[i, 0]) * 100f);
                c.style.height = Length.Percent((cuts[i, 3] - cuts[i, 1]) * 100f);
                c.RemoveFromClassList("prologue-cover--open");
            }
            prologueCut = 0;
            prologueTimer = 0.4f; // 첫 컷은 바로
        }

        /// <summary>클릭(또는 시간): 다음 컷을 드러내고, 3컷을 다 보면 다음 장, 마지막 장이 끝나면 게임으로.</summary>
        void AdvancePrologue()
        {
            if (prologueCut < 3)
            {
                prologueCovers[prologueCut].AddToClassList("prologue-cover--open");
                prologueCut++;
                prologueTimer = prologueCutSeconds;
                return;
            }
            if (prologuePage + 1 < PrologueCuts.Length)
            {
                prologuePage++;
                ShowProloguePage();
                return;
            }
            EndPrologue();
        }

        void EndPrologue()
        {
            prologueShownThisRun = true;
            Show(prologueOverlay, false);
        }

        void UpdateIntro()
        {
            if (prologueOverlay == null || prologueOverlay.resolvedStyle.display == DisplayStyle.None) return;
            FitToImage(prologueStage, prologueOverlay, PrologueAspect);
            if (prologueCutSeconds <= 0f) return;
            prologueTimer -= Time.unscaledDeltaTime;
            if (prologueTimer <= 0f) AdvancePrologue();
        }

        // 타이틀 그림(1672×941) 안에서의 문구 자리: 제목 아랫변(약 70.7%)과 그래프 윗변(약 78%) 사이
        const float TitleAspect = 1672f / 941f;
        const float TitleTextWidth = 0.34f;

        /// <summary>scale-and-crop으로 그려진 그림의 실제 위치를 계산해 문구를 그림 기준 자리에 둔다.</summary>
        void LayoutTitle()
        {
            var rect = titleOverlay.contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            float drawW = rect.width, drawH = rect.width / TitleAspect;
            if (drawH < rect.height)
            {
                drawH = rect.height;
                drawW = rect.height * TitleAspect;
            }
            float offsetX = (rect.width - drawW) * 0.5f;
            float offsetY = (rect.height - drawH) * 0.5f;

            titleStart.style.width = drawW * TitleTextWidth;
            titleStart.style.left = offsetX + drawW * (titleTextCenterX - TitleTextWidth * 0.5f);
            titleStart.style.top = offsetY + drawH * titleTextTop;
            titleStart.style.fontSize = drawH * titleTextSize;
        }

        void BlinkTitle()
        {
            if (titleOverlay == null || titleOverlay.resolvedStyle.display == DisplayStyle.None) return;
            LayoutTitle(); // 인스펙터에서 값을 바꾸면 바로 보이게
            titleBlinkTimer -= Time.unscaledDeltaTime;
            if (titleBlinkTimer > 0f) return;
            titleBlinkTimer = 1.2f; // 깜빡임 주기
            titleStart.ToggleInClassList("title-start--dim");
        }

        // ───────── 이벤트 ─────────

        void OnPhaseChanged()
        {
            lastMessage = "";
            Show(pickerOverlay, false);
            Show(shopOverlay, game.Phase == GamePhase.Shop);
            Show(newsOverlay, game.Phase == GamePhase.News);
            if (game.Phase == GamePhase.News) BuildNewsPaper();
            if (loanOverlay.resolvedStyle.display == DisplayStyle.Flex) CloseLoan();
            if (game.Phase == GamePhase.GameOver) StartEnding();
            else Show(endOverlay, false);
            chart.SetPreview(0);
            Show(telescopePanel, false);

            if (game.Phase == GamePhase.Settlement)
            {
                repayField.value = 0f;
                settlementMessage.text = "";
            }
            // 상점 안내: 장 마감 속보 같은 지난 메시지가 남아 겹치지 않게 상점에 들어올 때 새로 쓴다
            if (game.Phase == GamePhase.Shop) shopMessage.text = "";
            RefreshNews();
            RefreshAll();
            tutorial?.OnPhaseChanged();
        }

        void OnMessage(string text)
        {
            // 장 중 속보·마감 기사는 뉴스 칸에 뜬다. 아이템 칸 오른쪽에는 띄우지 않는다
            if (text.StartsWith("[속보]") || text.StartsWith("[마감]")) return;
            lastMessage = text;
            if (game.Phase == GamePhase.Settlement) settlementMessage.text = text;
            if (game.Phase == GamePhase.Shop) shopMessage.text = text;
            RefreshItemBar();
        }

        static string TimingName(ItemTiming t) => t switch { ItemTiming.Prep => "장 전", ItemTiming.Market => "장 중", _ => "장 전·장 중" };

        void OpenLoan()
        {
            var s = game.LoanSettings;
            loanBody.text =
                $"한 판에 한 번, 최대 {Format.Money(s.extraLoanMax)}까지 빌릴 수 있어요.\n" +
                $"일반 대출과 따로 하루 이자 {s.extraLoanRate * 100f:0}%가 붙고, 이자는 {game.Day + s.extraLoanGraceDays}일차(다다음 날)부터예요.";
            loanField.value = s.extraLoanMax;
            Show(loanOverlay, true);
            loanOverlay.BringToFront();
            game.ExternalPause = true; // 장 중이면 고르는 동안 시간을 멈춘다
        }

        void CloseLoan()
        {
            Show(loanOverlay, false);
            game.ExternalPause = false;
        }

        // ───────── 갱신 ─────────

        void RefreshAll()
        {
            RefreshStatus();
            RefreshHints();
            RefreshItemBar();
            RefreshFlow();
            // 정산 후 잔고가 0 밑이면 정산 창 대신 긴급 대출 창 (고르기 전까지 4단계로 못 감)
            Show(emergencyOverlay, game.NeedsEmergency);
            Show(settlementOverlay, game.Phase == GamePhase.Settlement && !game.NeedsEmergency);
            if (game.NeedsEmergency) RefreshEmergency();
            if (game.Phase == GamePhase.Settlement) RefreshSettlement();
            if (game.Phase == GamePhase.Shop) RefreshShop();
        }

        void RefreshStatus()
        {
            var loan = game.Loan;
            dayLabel.text = $"DAY {game.Day}";

            // 단계 진행: 지난 단계는 흐리게, 지금 단계는 강조, 장 중에는 남은 시간
            int now = game.Phase switch
            {
                GamePhase.News => 0,
                GamePhase.Prep => 1,
                GamePhase.Market => 2,
                GamePhase.Settlement => 3,
                GamePhase.Shop => 4,
                _ => 5,
            };
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i].EnableInClassList("step--done", i < now);
                steps[i].EnableInClassList("step--now", i == now);
            }
            // 단계 이름에 번호는 없이, 장은 제한 시간을 함께 (장 중엔 남은 시간)
            float marketSeconds = now == 2 ? Session.RemainingSeconds : Session.Settings.ticksPerDay * Session.Settings.secondsPerTick;
            steps[2].text = $"장 · {Mathf.CeilToInt(Mathf.Max(0f, marketSeconds))}초";

            // 빚 상환 게이지 = 갚은 돈 / (갚은 돈 + 남은 빚)
            float repaid = loan.TotalRepaid;
            float total = repaid + loan.Debt;
            repayLabel.text = $"{Format.Money(repaid)} / {Format.Money(total)}";
            repayFill.style.width = Length.Percent(total > 0f ? 100f * repaid / total : 100f);

            cashLabel.text = Format.Money(Session.Portfolio.Cash);
            stockValueLabel.text = Format.Money(game.StockValue());
            int startDay = game.LoanSettings.interestStartDay;
            // 오늘 낼 이자율만 (조건은 3단계 튜토리얼에서 설명): 오늘 이자가 붙는 대출들의 이자율
            var rates = loan.RatesOn(game.Day).Select(r => $"{r * 100f:0.#}%").ToList();
            interestLabel.text = rates.Count == 0 ? "0%" : string.Join(" · ", rates);
            loanCountLabel.text = loan.CanTakeExtraLoan ? "1회 남음" : "사용함";
            loanButton.SetEnabled(game.CanTakeExtraLoanNow); // 추가 대출은 2·3단계에서, 한 판에 1번

            float netWorth = game.NetWorth();
            netWorthLabel.text = Format.SignedMoney(netWorth);
            Format.SetTrendClass(netWorthLabel, netWorth);

            // 주인공
            float today = game.TodayReturn();
            var mood = game.Character.GetMood(today);
            bool beforeMarket = game.Phase is GamePhase.News or GamePhase.Prep;
            var image = beforeMarket && game.Character.prepImage != null ? game.Character.prepImage : game.Character.MoodImage(mood);
            SetImage(portrait, image);
            faceLabel.text = CharacterSettings.MoodFace(mood);
            faceLabel.style.display = image == null ? DisplayStyle.Flex : DisplayStyle.None;
            speechLabel.text = beforeMarket ? game.Character.prepLine : game.Character.MoodLine(mood);
            moodLabel.text = $"{CharacterSettings.MoodName(mood)} · 오늘 {Format.SignedPercent(today)}";
            Format.SetTrendClass(moodLabel, today);

            // 뉴스를 확인하기 전(1단계)에는 예약·장 시작을 막는다 (예전엔 뉴스 팝업이 화면을 가렸다)
            bool newsPhase = game.Phase == GamePhase.News;
            buyButton?.SetEnabled(!newsPhase);
            sellButton?.SetEnabled(!newsPhase);
            startButton?.SetEnabled(!newsPhase);
        }

        /// <summary>뉴스 띠와 종목 리스트의 "뉴스" 표시. 1단계엔 오늘 뉴스 전체, 그 뒤엔 선택한 종목의 뉴스만.</summary>
        /// <summary>하루 시작 전 신문 화면에 오늘 뉴스를 적는다.</summary>
        void BuildNewsPaper()
        {
            newsPaperDate.text = $"{game.Day}일차 아침";
            newsPaperList.Clear();
            foreach (var n in game.TodayNews)
            {
                var line = new Label(n.Text);
                line.AddToClassList("news-paper-item");
                line.EnableInClassList("news-paper-item--chain", n.Foreseen);
                newsPaperList.Add(line);
            }
        }

        void RefreshNews()
        {
            if (stockWindow != null)
                foreach (var item in stockWindow.ListItems)
                {
                    var news = game.TodayNews.FirstOrDefault(n => n.Stock == item.Stock);
                    bool foreseen = (news != null && news.Foreseen) || game.Foresights.Any(n => n.Stock == item.Stock);
                    item.SetBadge(foreseen ? "예지" : news != null ? "뉴스" : null, foreseen ? "hint" : "news");
                }

            bool newsPhase = game.Phase == GamePhase.News;
            var selected = stockWindow != null ? stockWindow.Selected : null;
            var lines = newsPhase
                ? game.TodayNews.ToList()
                : game.TodayNews.Concat(game.Foresights).Where(n => n.Stock == selected).ToList();

            newsBandList.Clear();
            foreach (var n in lines)
            {
                // 결과가 뜨기 전엔 최초 기사만, 뜬 뒤엔 결과 기사와 변동률
                var line = new Label("· " + (n.Revealed ? $"{n.ResultText} ({Format.SignedPercent(n.Effect)})" : n.Text));
                line.AddToClassList("news-band-line");
                line.EnableInClassList("news-band-line--chain", n.Revealed);
                newsBandList.Add(line);
            }
            newsBandTitle.text = newsPhase ? $"{game.Day}일차 아침 뉴스" : "이 종목 오늘의 뉴스";

            // 오른쪽 위 영역: 오늘 뉴스 전체 (결과가 뜬 뉴스는 결과 기사와 변동률)
            if (sideNewsList != null)
            {
                sideNewsList.Clear();
                foreach (var n in game.TodayNews)
                {
                    var side = new Label("· " + (n.Revealed ? $"{n.ResultText} ({Format.SignedPercent(n.Effect)})" : n.Text));
                    side.AddToClassList("side-news-line");
                    side.EnableInClassList("side-news-line--revealed", n.Revealed);
                    sideNewsList.Add(side);
                }
                if (game.TodayNews.Count == 0) sideNewsList.Add(new Label("오늘은 뉴스가 없어요.") { name = "side-news-empty" });
            }
            Show(newsConfirmButton, newsPhase);
            Show(newsBand, newsPhase || lines.Count > 0);
            newsBand.EnableInClassList("news-band--alert", newsPhase);
        }

        /// <summary>아이템을 갖고 있거나 오늘 썼을 때만 아이템 관련 안내(아이템 칸 오른쪽 글자, 받은 힌트 카드)를 보인다.</summary>
        bool HasItems() => game.Inventory.Owned.Any() || (game.Today != null && game.Today.ItemUses.Count > 0);

        void RefreshHints()
        {
            Show(hintCard, HasItems());
            hintList.Clear();
            foreach (var h in game.Hints) AddHint(h);
            foreach (var a in game.ActiveAutoSells()) AddHint("[예약 중] " + a);
            if (game.StopRemaining > 0f) AddHint($"[시간 정지] {game.StopRemaining:0.0}초 남음");
            if (game.SlowRemaining > 0f) AddHint($"[슬로우 모션] {game.SlowRemaining:0.0}초 남음");
            if (game.PreviewTicks > 0 && game.TelescopeStock != null) AddHint($"[망원경] {game.TelescopeStock.Name} 미리보기 창");
            if (hintList.childCount == 0) AddHint("아직 없어요. 정보 아이템을 쓰면 여기에 떠요.");
        }

        void AddHint(string text)
        {
            var line = new Label(text);
            line.AddToClassList("hint-line");
            hintList.Add(line);
        }

        void RefreshItemBar()
        {
            itemBarList.Clear();
            var owned = game.Inventory.Owned.ToList();
            for (int i = 0; i < owned.Count; i++)
            {
                var (item, count) = owned[i];
                var slot = new Button(() => OnItemClicked(item));
                slot.AddToClassList("item-slot");
                slot.EnableInClassList("item-slot--off", !game.CanUseNow(item));
                bool usedToday = game.Today != null && game.Today.ItemUses.Any(u => u.Item == item);
                slot.EnableInClassList("item-slot--used", usedToday);
                slot.tooltip = item.description;
                string key = i < 9 ? $"[{i + 1}] " : "";
                var name = new Label($"{key}{item.displayName} x{count}");
                name.AddToClassList("item-slot__name");
                var meta = new Label(TimingName(item.Timing) + (usedToday ? " · 오늘 사용" : ""));
                meta.AddToClassList("item-slot__meta");
                if (item.icon != null)
                {
                    var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                    icon.AddToClassList("item-slot__icon");
                    icon.style.backgroundImage = new StyleBackground(item.icon);
                    slot.Add(icon);
                }
                var textCol = new VisualElement { pickingMode = PickingMode.Ignore };
                textCol.AddToClassList("item-slot__text");
                textCol.Add(name);
                textCol.Add(meta);
                slot.Add(textCol);
                itemBarList.Add(slot);
            }

            string guide = game.Phase switch
            {
                GamePhase.Market => "장 중 아이템 사용 가능 · 매수 Q · 매도 W",
                _ => "",
            };
            itemBarHint.text = string.IsNullOrEmpty(lastMessage) ? guide : lastMessage;
            Show(itemBarHint, HasItems());
        }

        /// <summary>숫자 1~9 = 아이템 칸 순서대로 사용 (입력 중이거나 창이 떠 있으면 무시).</summary>
        void HandleItemKeys()
        {
            if (!Input.anyKeyDown || AnyOverlayOpen() || IsTyping()) return;
            var owned = game.Inventory.Owned.ToList();
            for (int i = 0; i < owned.Count && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    OnItemClicked(owned[i].item);
                    return;
                }
        }

        bool AnyOverlayOpen() =>
            (tutorial != null && tutorial.IsShowing) || IntroShowing() ||
            new[] { pickerOverlay, settlementOverlay, shopOverlay, endOverlay, titleOverlay, newsOverlay, emergencyOverlay, loanOverlay }.Any(o => o != null && o.resolvedStyle.display == DisplayStyle.Flex);

        bool IsTyping() =>
            itemBarList.panel?.focusController?.focusedElement is VisualElement focused &&
            (focused.GetFirstOfType<IntegerField>() != null || focused.GetFirstOfType<FloatField>() != null || focused.GetFirstOfType<TextField>() != null);

        void OnItemClicked(ItemDefinition item)
        {
            if (!game.CanUseNow(item))
            {
                game.UseItem(item, null); // 쓸 수 없는 이유를 메시지로 보여준다
                return;
            }
            if (!item.NeedsTarget)
            {
                game.UseItem(item, null);
                return;
            }

            pickerTitle.text = $"{item.displayName}: 종목 선택";
            pickerList.Clear();
            foreach (var stock in Session.Stocks)
            {
                if (item.TargetsHeldOnly && Session.Portfolio.GetHolding(stock).Shares < 1) continue;
                var target = stock;
                var b = new Button(() =>
                {
                    Show(pickerOverlay, false);
                    game.UseItem(item, target);
                }) { text = stock.Name };
                b.AddToClassList("picker-button");
                pickerList.Add(b);
            }
            if (pickerList.childCount == 0)
                pickerList.Add(new Label("보유한 종목이 없어요") { name = "picker-empty" });
            Show(pickerOverlay, true);
        }

        void RefreshEmergency()
        {
            float shortfall = -Session.Portfolio.Cash;
            var loan = game.Loan;
            var s = game.LoanSettings;
            float amount = loan.EmergencyAmount(shortfall);
            float rate = s.emergencyRate * (loan.HalveNextRate ? 0.5f : 1f);
            float debtAfter = loan.Debt + amount;
            string closed = game.LastClosedStocks.Count > 0 ? $"마이너스 종목({string.Join(", ", game.LastClosedStocks.Select(x => x.Name))}) 정리로 " : "";
            emergencyBody.text =
                $"{closed}잔고에서 {Format.Money(game.LastSettlementCharge)}이 빠져 현금이 {Format.SignedMoney(-shortfall)}이에요.\n\n" +
                $"· 대출 금액: {Format.Money(amount)} (모자란 돈 + 여유 {Format.Money(s.emergencyLoanBuffer)}, 최대 {Format.Money(s.emergencyLoanMax)})\n" +
                $"· 대출 후 현금: {Format.Money(amount - shortfall)}\n" +
                $"· 햇살론 이자: 하루 {rate * 100f:0.#}% (기본 대출과 따로, 내일부터)" + (loan.HalveNextRate ? " · 신용등급 세탁기 적용" : "") + "\n" +
                $"· 남은 빚: {Format.Money(loan.Debt)} → {Format.Money(debtAfter)}\n" +
                $"· 햇살론 이자(하루): 약 {Format.Money(amount * rate)}\n\n" +
                "햇살론은 파산 직전에 한 번만 받을 수 있어요. 안 받으면 파산이에요.";
        }

        // ───────── 오늘의 흐름 (2단계) ─────────

        /// <summary>지금까지 한 일(뉴스 확인, 예약, 쓴 아이템)과 아직 안 쓴 아이템. 2단계에서 강조한다.</summary>
        void RefreshFlow()
        {
            if (flowList == null || game.Today == null) return;
            var today = game.Today;
            flowList.Clear();
            flowCard.EnableInClassList("flow-card--focus", game.Phase == GamePhase.Prep);

            AddFlow(today.NewsChecked ? "● 뉴스 확인함" : "○ 뉴스 확인 전", today.NewsChecked ? "done" : "todo");

            if (game.Phase is GamePhase.News or GamePhase.Prep)
            {
                var res = Session.Reservations;
                AddFlow(res.Count == 0
                    ? "○ 예약 주문 없음"
                    : "● 예약 " + res.Count + "건: " + string.Join(", ", res.Select(o =>
                        $"{o.Stock.Name} {(o.Side == OrderSide.Buy ? "매수" : "매도")} {(o.UseMax ? "최대" : o.Quantity + "주")}")),
                    res.Count == 0 ? "todo" : "done");
            }
            else
            {
                AddFlow(today.ReservationsPlaced == 0
                    ? "○ 예약 없이 시작"
                    : $"● 예약 체결 {today.ReservationsFilled}/{today.ReservationsPlaced}건", today.ReservationsPlaced == 0 ? "todo" : "done");
            }

            var used = today.ItemUses;
            AddFlow(used.Count == 0
                ? "○ 오늘 쓴 아이템 없음"
                : "● 쓴 아이템: " + string.Join(", ", used.Select(u => $"{u.Item.displayName}({(u.Phase == GamePhase.Prep ? "장 전" : "장 중")})")),
                used.Count == 0 ? "todo" : "done");

            var owned = game.Inventory.Owned.ToList();
            var prepLeft = owned.Where(o => o.item.Timing != ItemTiming.Market).Select(o => $"{o.item.displayName} x{o.count}").ToList();
            var marketLeft = owned.Where(o => o.item.Timing != ItemTiming.Prep).Select(o => $"{o.item.displayName} x{o.count}").ToList();
            if (prepLeft.Count > 0)
                AddFlow("안 쓴 장 전 아이템: " + string.Join(", ", prepLeft), game.Phase == GamePhase.Prep ? "warn" : "todo");
            if (marketLeft.Count > 0)
                AddFlow("장 중에 쓸 아이템: " + string.Join(", ", marketLeft), "todo");
            if (owned.Count == 0 && used.Count == 0)
                AddFlow("가진 아이템 없음 (상점은 5단계)", "todo");
        }

        void AddFlow(string text, string kind)
        {
            var line = new Label(text);
            line.AddToClassList("flow-line");
            line.AddToClassList("flow-line--" + kind);
            flowList.Add(line);
        }

        // ───────── 하루 결과창 (4단계) ─────────

        void RefreshSettlement()
        {
            var r = game.Today;
            settlementTitle.text = $"{game.Day}일차 장 결과";
            settlementSummary.text = $"보유 주식은 다음 날로 이어져요. 성공 = 빚 0원 + 자산 {Format.Money(game.LoanSettings.winAssets)} 이상";
            RefreshPayBoxes();
            settlementReport.Clear();
            if (r == null) return;

            // 1. 오늘 손익: 총자산 변화와 그 안에서 매매로 번 돈
            var head = new VisualElement();
            head.AddToClassList("report-headline");
            var big = new Label(Format.SignedMoney(r.TradingProfit));
            big.AddToClassList("report-big");
            Format.SetTrendClass(big, r.TradingProfit);
            var rate = new Label($"매매로 번 돈 · 총자산 {Format.Money(r.StartAssets)} → {Format.Money(r.EndAssets)} ({Format.SignedPercent(r.ReturnRate)})");
            rate.AddToClassList("report-note");
            head.Add(big);
            head.Add(rate);
            settlementReport.Add(head);

            // 1-2. 돈의 흐름: 어디서 벌고 어디로 나갔나
            var money = Section("오늘 돈의 흐름");
            Row(money, true, "항목", "금액");
            void MoneyRow(string name, float v)
            {
                if (Mathf.Abs(v) < 0.005f) return;
                var row = Row(money, false, name, Format.SignedMoney(v));
                Format.SetTrendClass(row.ElementAt(1), v);
            }
            MoneyRow("매매 손익 (3배)", r.TradingProfit);
            MoneyRow("아이템으로 들어온 돈", r.ItemCash - r.InsuranceRefund);
            MoneyRow("손실 보험 환급", r.InsuranceRefund);
            MoneyRow("추가 대출", r.ExtraLoan);
            MoneyRow("햇살론", r.EmergencyLoan);
            MoneyRow("빚 상환", -r.Repaid);
            MoneyRow("이자 낸 돈", -(r.InterestPaid - r.InterestUnpaid));
            MoneyRow("마이너스 정산 (잔고 차감)", -r.SettlementCharge);
            if (money.childCount <= 2) Note(money, "오늘은 돈이 움직이지 않았어요.");

            // 2. 내 종목: 주식 창처럼 매수·매도·평가·손익
            var stocks = Section("내 종목 (손익 = 주가 변동 × 3, 수수료 없음)");
            var lines = r.Stocks.Values.Where(l => l.Active).OrderByDescending(l => l.ProfitLoss).ToList();
            if (lines.Count == 0) Note(stocks, "오늘은 거래도 보유도 없었어요.");
            else
            {
                Row(stocks, true, "종목", "매수", "매도", "시작 평가금", "마감 평가금", "손익");
                foreach (var l in lines)
                {
                    var row = Row(stocks, false, l.Stock.Name,
                        l.Buys > 0 ? $"{l.BoughtShares}주 · {Format.Money(l.BuyAmount)}" : "-",
                        l.Sells > 0 ? $"{l.SoldShares}주 · {Format.Money(l.SellAmount)}" : "-",
                        Format.Money(l.StartValue),
                        Format.Money(l.EndValue),
                        Format.SignedMoney(l.ProfitLoss));
                    Format.SetTrendClass(row.ElementAt(row.childCount - 1), l.ProfitLoss);
                }
                var best = lines.First();
                var worst = lines.Last();
                if (best.ProfitLoss > 0f) Note(stocks, $"오늘의 효자: {best.Stock.Name} {Format.SignedMoney(best.ProfitLoss)}");
                if (worst.ProfitLoss < 0f) Note(stocks, $"오늘의 불효자: {worst.Stock.Name} {Format.SignedMoney(worst.ProfitLoss)}");
            }
            int trades = lines.Sum(l => l.Buys + l.Sells);
            Note(stocks, $"체결 {trades}건 · 예약 체결 {r.ReservationsFilled}/{r.ReservationsPlaced}건" +
                         (r.AutoSellsFired > 0 ? $" · 자동 매도 발동 {r.AutoSellsFired}회" : ""));

            // 2-2. 시장 한눈에: 오늘 전 종목의 시가 갭 · 고가 · 저가 · 종가 (전일 종가 대비)
            var market = Section("오늘 시장 (전일 종가 대비)");
            Row(market, true, "종목", "업종", "시가", "고가", "저가", "종가");
            foreach (var st in Session.Stocks.OrderByDescending(x => x.PriceAt(Session.Tick) / x.PrevClose))
            {
                int last = Mathf.Min(Session.Tick, st.Path.Length - 1);
                float hi = float.MinValue, lo = float.MaxValue;
                for (int i = 0; i <= last; i++)
                {
                    hi = Mathf.Max(hi, st.Path[i]);
                    lo = Mathf.Min(lo, st.Path[i]);
                }
                float close = st.PriceAt(Session.Tick) / st.PrevClose - 1f;
                bool held = r.Stocks.TryGetValue(st, out var mine) && mine.Active;
                bool hadNews = r.News.Any(n => n.Stock == st) || game.Foresights.Any(n => n.Stock == st);
                var row = Row(market, false,
                    (held ? "● " : "") + st.Name + (hadNews ? " [뉴스]" : ""),
                    st.Definition.sector.ToKorean(),
                    Format.SignedPercent(st.OpenPrice / st.PrevClose - 1f),
                    Format.SignedPercent(hi / st.PrevClose - 1f),
                    Format.SignedPercent(lo / st.PrevClose - 1f),
                    Format.SignedPercent(close));
                Format.SetTrendClass(row.ElementAt(5), close);
            }
            Note(market, "● = 오늘 거래했거나 들고 있던 종목");

            // 3. 아이템
            var items = Section("사용한 아이템");
            if (r.ItemUses.Count == 0) Note(items, "오늘 쓴 아이템이 없어요.");
            foreach (var u in r.ItemUses)
            {
                string verdict = u.HintCorrect == null ? ""
                    : u.HintRevealedTomorrow ? " · 결과는 내일"
                    : u.HintCorrect.Value ? " · 적중" : " · 빗나감";
                var row = Row(items, false, $"{u.Item.displayName} ({(u.Phase == GamePhase.Prep ? "장 전" : "장 중")})", u.Text + verdict);
                if (u.HintCorrect != null && !u.HintRevealedTomorrow)
                    row.ElementAt(1).AddToClassList(u.HintCorrect.Value ? "report-ok" : "report-bad");
            }

            // 4. 뉴스 결과: 뉴스가 말한 방향과 실제 하루 등락
            var news = Section("오늘 뉴스는 어떻게 됐나");
            if (r.News.Count == 0) Note(news, "뉴스가 없었어요.");
            foreach (var n in r.News)
            {
                if (n.Stock == null) continue;
                float change = n.Stock.PriceAt(Session.Tick) / n.Stock.PrevClose - 1f;
                bool hit = (n.Effect >= 0f) == (change >= 0f);
                var row = Row(news, false, n.Stock.Name, $"{n.Text} → {(n.Success ? "성공" : "실패")}", $"실제 {Format.SignedPercent(change)}");
                row.ElementAt(2).AddToClassList(hit ? "report-ok" : "report-bad");
            }

            // 5. 대출
            var loan = Section("빚 · 대출");
            if (r.SettlementCharge > 0f)
                Note(loan, $"마이너스 정산: {string.Join(", ", r.ClosedStocks.Select(s => s.Name))} 정리, 잔고에서 {Format.Money(r.SettlementCharge)} 빠짐");
            if (r.EmergencyLoan > 0f) Note(loan, $"햇살론 {Format.Money(r.EmergencyLoan)}");
            if (r.ExtraLoan > 0f) Note(loan, $"추가 대출 {Format.Money(r.ExtraLoan)}");
            if (r.InterestDeferred) Note(loan, "오늘은 이자일이지만 이자 유예 신청서로 면제됐어요");
            if (r.ItemDebt > 0f) Note(loan, $"대출 아이템으로 빚 +{Format.Money(r.ItemDebt)}");
            Note(loan, r.Interest > 0f
                ? $"오늘 이자 {Format.Money(r.Interest)} (왼쪽 '이자 갚기'로 내요. 안 내면 상점으로 갈 때 빚에 붙어요)"
                : game.Day < game.LoanSettings.interestStartDay ? $"이자는 {game.LoanSettings.interestStartDay}일차부터 매일 붙어요" : "오늘은 낼 이자가 없어요");
            foreach (var l in game.Loan.Loans.Where(l => l.Principal > 0.5f))
                Note(loan, $"{l.Name} {Format.Money(l.Principal)} · 하루 {l.Rate * 100f:0.#}% ({l.StartDay}일차부터)");
            if (r.Repaid > 0f) Note(loan, $"오늘 갚은 돈 {Format.Money(r.Repaid)}");
            Note(loan, $"현금 {Format.Money(Session.Portfolio.Cash)} · 남은 빚 {Format.Money(game.Loan.Debt)}");
        }

        /// <summary>왼쪽 이자 갚기 (3일차부터 보임) · 오른쪽 빚 갚기.</summary>
        void RefreshPayBoxes()
        {
            var r = game.Today;
            bool interestDays = game.Day >= game.LoanSettings.interestStartDay;
            Show(interestBox, interestDays);
            if (!interestDays || r == null) return;
            float left = game.InterestLeft;
            interestDueLabel.text = r.InterestDeferred ? "오늘 이자는 이자 유예 신청서로 면제"
                : r.Interest <= 0f ? "오늘은 낼 이자가 없어요"
                : left <= 0f ? $"오늘 이자 {Format.Money(r.Interest)} 다 냈어요"
                : $"오늘 이자 {Format.Money(left)} 남음 (안 내면 빚에 붙음)";
            interestPayButton.SetEnabled(left > 0f);
        }

        VisualElement Section(string title)
        {
            var box = new VisualElement();
            box.AddToClassList("report-section");
            var t = new Label(title);
            t.AddToClassList("report-section-title");
            box.Add(t);
            settlementReport.Add(box);
            return box;
        }

        static VisualElement Row(VisualElement parent, bool header, params string[] cells)
        {
            var row = new VisualElement();
            row.AddToClassList("report-row");
            if (header) row.AddToClassList("report-row--head");
            for (int i = 0; i < cells.Length; i++)
            {
                var c = new Label(cells[i]);
                c.AddToClassList("report-cell");
                if (i == 0) c.AddToClassList("report-cell--name");
                else if (cells.Length > 2) c.AddToClassList("report-cell--num");
                if (header) c.AddToClassList("report-cell--head");
                row.Add(c);
            }
            parent.Add(row);
            return row;
        }

        static void Note(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("report-note");
            parent.Add(l);
        }

        // 상점 그림(store.png, 1672×941)에서 가방 안감 바닥면: 위가 좁고 아래가 넓은 사다리꼴 (그림 대비 비율)
        const float StoreAspect = 1672f / 941f;
        const float FloorTopY = 0.319f, FloorTopLeft = 0.090f, FloorTopRight = 0.772f;
        const float FloorBottomY = 0.840f, FloorBottomLeft = 0.018f, FloorBottomRight = 0.819f;
        const float FloorDepthCurve = 1.2f; // 1보다 크면 안쪽(위) 줄이 납작해져 멀어 보인다

        // 바닥면 위 칸 자리 (바닥 기준 u: 왼→오, v: 안쪽→앞쪽, 0~1)
        static readonly float[] ShopColumnsU = { 0.03f, 0.275f, 0.52f, 0.765f };
        static readonly float[] ShopRowsV = { 0.06f, 0.52f };
        const float ShopSlotU = 0.2f, ShopSlotV = 0.3f, ShopNameGap = 0.006f, ShopNameHeight = 0.07f;

        static float FloorY(float v) => Mathf.Lerp(FloorTopY, FloorBottomY, Mathf.Pow(Mathf.Clamp01(v), FloorDepthCurve));

        /// <summary>바닥 좌표(u, v)를 그림 좌표(비율)로. 좌우 끝은 가방 옆선을 따라 기운다.</summary>
        static Vector2 FloorPoint(float u, float v)
        {
            float y = FloorY(v);
            float t = (y - FloorTopY) / (FloorBottomY - FloorTopY);
            float left = Mathf.Lerp(FloorTopLeft, FloorBottomLeft, t);
            float right = Mathf.Lerp(FloorTopRight, FloorBottomRight, t);
            return new Vector2(Mathf.Lerp(left, right, u), y);
        }

        /// <summary>stage를 container 안에서 scale-and-crop으로 그려진 그림 영역과 같은 위치·크기로 맞춘다.</summary>
        static void FitToImage(VisualElement stage, VisualElement container, float aspect)
        {
            var rect = container.contentRect;
            if (stage == null || rect.width <= 0f || rect.height <= 0f) return;
            float w = rect.width, h = rect.width / aspect;
            if (h < rect.height)
            {
                h = rect.height;
                w = rect.height * aspect;
            }
            stage.style.left = (rect.width - w) * 0.5f;
            stage.style.top = (rect.height - h) * 0.5f;
            stage.style.width = w;
            stage.style.height = h;
        }

        /// <summary>오늘 진열된 아이템을 가방 바닥에 눕힌 2×4 칸에 놓는다. 칸 아래에는 이름만, 설명은 마우스 포인터 옆 창에.</summary>
        void RefreshShop()
        {
            shopCash.text = "현금 " + Format.Money(Session.Portfolio.Cash);
            shopGrid.Clear();

            var items = game.TodayShop;
            int slots = ShopColumnsU.Length * ShopRowsV.Length;
            for (int i = 0; i < slots; i++)
            {
                float u = ShopColumnsU[i % ShopColumnsU.Length];
                float v = ShopRowsV[i / ShopColumnsU.Length];
                var item = i < items.Count ? items[i] : null;

                // 바닥에 눕힌 사다리꼴 칸: 네 꼭짓점을 그림 좌표로 구하고, 그 바깥 사각형에 요소를 놓는다
                Vector2 tl = FloorPoint(u, v), tr = FloorPoint(u + ShopSlotU, v);
                Vector2 br = FloorPoint(u + ShopSlotU, v + ShopSlotV), bl = FloorPoint(u, v + ShopSlotV);
                float minX = Mathf.Min(tl.x, bl.x), maxX = Mathf.Max(tr.x, br.x);
                float minY = tl.y, maxY = bl.y, w = maxX - minX, h = maxY - minY;
                Vector2 Rel(Vector2 pt) => new((pt.x - minX) / w, (pt.y - minY) / h);

                var slot = new ShopSlotElement();
                slot.AddToClassList("shop-slot");
                SetPercentRect(slot, minX, minY, w, h);
                slot.SetCorners(Rel(tl), Rel(tr), Rel(br), Rel(bl));
                shopGrid.Add(slot);

                // 아이템 그림: 사다리꼴 안에 들어가는 가운데 사각형 (읽기 쉽게 눕히지 않는다)
                float innerLeft = (Mathf.Max(tl.x, bl.x) - minX) / w, innerRight = (Mathf.Min(tr.x, br.x) - minX) / w;
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("shop-slot-icon");
                icon.style.left = Length.Percent((innerLeft + 0.04f) * 100f);
                icon.style.right = Length.Percent((1f - innerRight + 0.04f) * 100f);
                slot.Add(icon);

                // 이름: 칸 앞쪽(아래) 변 바로 밑, 그 변 폭만큼
                var name = new Label(item != null ? item.displayName : "");
                name.AddToClassList("shop-slot-name");
                name.pickingMode = PickingMode.Ignore;
                SetPercentRect(name, bl.x, bl.y + ShopNameGap, br.x - bl.x, ShopNameHeight);
                shopGrid.Add(name);

                if (item == null)
                {
                    // 오늘 진열할 아이템이 모자라면 빈 칸
                    slot.AddToClassList("shop-slot--empty");
                    slot.FillColor = new Color(0f, 0f, 0f, 0f);
                    slot.BorderColor = new Color(0.96f, 0.9f, 0.85f, 0.3f);
                    slot.pickingMode = PickingMode.Ignore;
                    continue;
                }

                if (item.icon != null) icon.style.backgroundImage = new StyleBackground(item.icon);
                bool poor = Session.Portfolio.Cash < item.price;
                slot.EnableInClassList("shop-slot--poor", poor);
                int owned = game.Inventory.Count(item);
                if (owned > 0)
                {
                    var count = new Label($"보유 {owned}") { pickingMode = PickingMode.Ignore };
                    count.AddToClassList("shop-slot-count");
                    slot.Add(count);
                }

                slot.RegisterCallback<PointerEnterEvent>(e => { ShowShopTooltip(item); MoveShopTooltip(e.position); });
                slot.RegisterCallback<PointerMoveEvent>(e => MoveShopTooltip(e.position));
                slot.RegisterCallback<PointerLeaveEvent>(_ => HideShopTooltip(item));
                slot.RegisterCallback<ClickEvent>(_ => game.Buy(item));
            }

            if (hoveredShopItem != null && items.Contains(hoveredShopItem)) ShowShopTooltip(hoveredShopItem);
            else HideShopTooltip(null);
        }

        static void SetPercentRect(VisualElement e, float left, float top, float width, float height)
        {
            e.style.left = Length.Percent(left * 100f);
            e.style.top = Length.Percent(top * 100f);
            e.style.width = Length.Percent(width * 100f);
            e.style.height = Length.Percent(height * 100f);
        }

        void ShowShopTooltip(ItemDefinition item)
        {
            hoveredShopItem = item;
            shopTipName.text = item.displayName;
            shopTipTag.text = item.Timing switch { ItemTiming.Prep => "아이템 시간용", ItemTiming.Market => "장 시간용", _ => "아이템·장 시간 둘 다" };
            shopTipDesc.text = string.IsNullOrEmpty(item.description) ? "(설명 없음)" : item.description;
            shopTipPrice.text = Format.Money(item.price) + (Session.Portfolio.Cash < item.price ? " · 현금 부족" : " · 클릭하면 구매");
            shopTipOwned.text = $"보유 {game.Inventory.Count(item)}개";
            shopTooltip.BringToFront();
            Show(shopTooltip, true);
        }

        const float TooltipOffset = 18f;

        /// <summary>설명창을 포인터 오른쪽 아래에 두고, 화면 밖으로 나가면 포인터 반대쪽으로 넘긴다.</summary>
        void MoveShopTooltip(Vector2 pointer)
        {
            var area = shopOverlay.worldBound;
            float w = shopTooltip.resolvedStyle.width, h = shopTooltip.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0f) w = 420f;
            if (float.IsNaN(h) || h <= 0f) h = 160f;

            float x = pointer.x - area.x + TooltipOffset;
            float y = pointer.y - area.y + TooltipOffset;
            if (x + w > area.width) x = pointer.x - area.x - TooltipOffset - w;
            if (y + h > area.height) y = pointer.y - area.y - TooltipOffset - h;
            shopTooltip.style.left = Mathf.Clamp(x, 0f, Mathf.Max(0f, area.width - w));
            shopTooltip.style.top = Mathf.Clamp(y, 0f, Mathf.Max(0f, area.height - h));
        }

        /// <summary>item이 지금 보이는 설명의 아이템일 때만 닫는다 (칸 사이를 옮겨 다닐 때 깜빡임 방지). null이면 무조건 닫는다.</summary>
        void HideShopTooltip(ItemDefinition item)
        {
            if (item != null && item != hoveredShopItem) return;
            hoveredShopItem = null;
            Show(shopTooltip, false);
        }

        // ───────── 망원경 창 · 파산 경보 ─────────

        /// <summary>망원경을 쓰면 그 종목의 [쓴 순간 ~ 몇 초 뒤] 가격을 따로 창에 그린다. 그 구간이 지나면 닫힌다.</summary>
        void UpdateTelescope()
        {
            var s = game.TelescopeStock;
            bool show = s != null && game.PreviewTicks > 0;
            Show(telescopePanel, show);
            if (!show) return;
            int from = game.TelescopeFromTick, until = Mathf.Min(game.TelescopeUntilTick, s.Path.Length - 1);
            var seg = new float[until - from + 1];
            System.Array.Copy(s.Path, from, seg, 0, seg.Length);
            telescopeChart.SetData(s.PrevClose, s.DailyLimit, seg, seg.Length, seg.Length - 1, null);
            telescopeTitle.text = $"망원경 · {s.Name} 앞으로 {Mathf.CeilToInt(game.PreviewTicks * Session.Settings.secondsPerTick)}초 · 끝 {Format.Money(seg[seg.Length - 1])}";
        }

        /// <summary>파산 경보기가 울리면 3초 동안 빨간 테두리가 깜빡인다 (소리는 GameAudio).</summary>
        void UpdateAlarm()
        {
            if (alarmOverlay == null) return;
            bool on = alarmTimer > 0f;
            Show(alarmOverlay, on);
            if (!on) return;
            alarmTimer -= Time.unscaledDeltaTime;
            alarmOverlay.EnableInClassList("alarm-overlay--dim", Mathf.FloorToInt(alarmTimer * 4f) % 2 == 0);
            alarmOverlay.BringToFront();
        }

        // ───────── 시연용 자동 진행 (실행 인자 -autoplay. -autoplay-win / -autoplay-bankrupt면 2일차 결과 뒤 엔딩) ─────────

        static bool HasArg(string arg) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), arg) >= 0;

        void AutoPlay()
        {
            if (!Prologue.PrologueController.AutoplayArg) return;
            if (warningOverlay != null && warningOverlay.resolvedStyle.display == DisplayStyle.Flex) Show(warningOverlay, false);
            if (game.Phase != autoPhase)
            {
                // 단계가 바뀌면 잠시 보여 준 뒤 진행 (결과창·상점은 더 오래, 스크린샷용)
                autoPhase = game.Phase;
                autoTimer = game.Phase is GamePhase.Settlement or GamePhase.Shop ? 6f : 3f;
            }
            autoTimer -= Time.unscaledDeltaTime;
            if (IntroShowing())
            {
                autoTimer = 3f; // 인트로가 끝난 뒤 첫 화면(튜토리얼 등)을 3초 보여 준다
                return;
            }
            if (autoTimer > 0f) return;
            autoTimer = 3f;
            if (tutorial != null && tutorial.IsShowing)
            {
                tutorial.Skip(); // 화면에 한 번 보인 뒤 건너뜀 (튜토리얼이 뜨는지 스크린샷 확인용)
                return;
            }
            switch (game.Phase)
            {
                case GamePhase.News:
                    game.ConfirmNews();
                    break;
                case GamePhase.Prep:
                    var pick = Session.Stocks[game.Day % Session.Stocks.Count];
                    Session.Reserve(new OrderRequest(pick, OrderSide.Buy, 0, true));
                    Session.StartMarket();
                    break;
                case GamePhase.Settlement:
                    if (game.NeedsEmergency) { game.AcceptEmergencyLoan(); break; }
                    if (game.Day >= 2 && (HasArg("-autoplay-win") || HasArg("-autoplay-bankrupt")))
                    {
                        game.EndForDemo(HasArg("-autoplay-win"));
                        break;
                    }
                    game.PayInterest();
                    game.GoToShop();
                    break;
                case GamePhase.Shop:
                    game.StartNextDay();
                    break;
                case GamePhase.GameOver:
                    AdvanceEnding();
                    break;
            }
        }

        // ───────── 엔딩 (파산 3장 · 성공 4장, 클릭하면 다음, 다 보면 다시 하기) ─────────

        const float EndingAspect = 1672f / 941f;
        const int BankruptImages = 4, SuccessImages = 4; // 파산 4번째는 그림이 아니라 어두운 화면 + 경고 문구 (ending-final)
        int EndingCount => game.Won ? SuccessImages : BankruptImages;

        void StartEnding()
        {
            endingIndex = 0;
            Show(pickerOverlay, false);
            Show(settlementOverlay, false);
            Show(emergencyOverlay, false);
            Show(endOverlay, true);
            endOverlay.BringToFront();
            // 파산2 말풍선 "드디어 n억 돌파!": n = 파산할 때의 빚 (억 단위, 버림, 최소 1)
            endingAmount.text = Mathf.Max(1, Mathf.FloorToInt(game.Loan.Debt / 100_000_000f)).ToString();
            endingSummary.text = game.Won
                ? $"{game.Day}일 만에 성공! 빚 0원 · 자산 {Format.Money(game.TotalAssets())}"
                : $"{game.Day}일차 파산 · 빚 {Format.Money(game.Loan.Debt)} · 잔고 {Format.SignedMoney(Session.Portfolio.Cash)}";
            ShowEndingImage();
        }

        void ShowEndingImage()
        {
            int last = EndingCount - 1;
            int shown = Mathf.Min(endingIndex, last);
            for (int i = 1; i <= SuccessImages; i++)
            {
                endingStage.EnableInClassList($"ending-success-{i}", game.Won && i == shown + 1);
                if (i <= BankruptImages) endingStage.EnableInClassList($"ending-bankrupt-{i}", !game.Won && i == shown + 1);
            }
            Show(endingAmount, !game.Won && shown == 1);
            bool final = !game.Won && shown == BankruptImages - 1;
            Show(endingFinal, final);
            Show(endingStage, !final);
            bool done = endingIndex >= last;
            Show(endingEnd, done);
            Show(endingHint, !done);
            LayoutEnding();
        }

        void AdvanceEnding()
        {
            if (game.Phase != GamePhase.GameOver || endingIndex >= EndingCount - 1) return;
            endingIndex++;
            ShowEndingImage();
        }

        void LayoutEnding()
        {
            FitToImage(endingStage, endOverlay, EndingAspect);
            float h = endingStage.style.height.value.value;
            if (!float.IsNaN(h) && h > 0f) endingAmount.style.fontSize = h * 0.1f; // 그림 속 "억" 글자 크기
        }

        /// <summary>
        /// 주인공 그림을 characterScale배로 키운다. 화면 오른쪽 아래 끝에 붙이고, 커진 만큼 위·왼쪽으로 뻗는다.
        /// 그림은 클릭을 받지 않아(picking Ignore) 뒤에 있는 버튼·카드는 그대로 눌린다.
        /// 기준(1배) 높이 = 오른쪽 열에서 카드들 아래 남은 높이.
        /// </summary>
        void LayoutCharacter(bool force)
        {
            if (characterArea == null || portrait == null) return;
            if (!force && Mathf.Approximately(appliedCharacterScale, characterScale)) return;

            // 1배: USS 배치 그대로 (말풍선 · 그림 · 표정 문구가 위아래로 나뉘어 겹치지 않음)
            if (characterScale <= 1.01f)
            {
                portrait.style.position = StyleKeyword.Null;
                portrait.style.top = portrait.style.left = portrait.style.bottom = portrait.style.right = StyleKeyword.Null;
                portrait.style.width = portrait.style.height = StyleKeyword.Null;
                characterArea.style.overflow = StyleKeyword.Null;
                appliedCharacterScale = characterScale;
                return;
            }
            float areaH = characterArea.layout.height, areaW = characterArea.layout.width;
            if (float.IsNaN(areaH) || areaH <= 0f) return;

            // 1배 = 오른쪽 아래 영역에 꽉 맞춤 (말풍선 자리만 위에 남김). 영역 밖으로 커지면 영역이 잘라내지 않게 overflow를 푼다
            const float baseTopGap = 40f;
            float baseH = Mathf.Max(80f, areaH - baseTopGap);
            float h = baseH * characterScale;
            var screen = characterArea.panel?.visualTree?.layout ?? new Rect(0, 0, 1920, 1080);
            float maxH = screen.height - 90f;
            if (h > maxH) h = maxH;
            float w = areaW * (h / baseH);
            characterArea.style.overflow = characterScale > 1.01f ? Overflow.Visible : Overflow.Hidden;

            portrait.style.position = Position.Absolute;
            portrait.style.top = StyleKeyword.Auto;
            portrait.style.left = StyleKeyword.Auto;
            portrait.style.bottom = 0f;
            portrait.style.right = 0f;
            portrait.style.height = h;
            portrait.style.width = w;
            portrait.SendToBack(); // 1배보다 크면 그림이 영역을 넘으므로 말풍선·표정 라벨이 그 위에 온다
            appliedCharacterScale = characterScale;
        }

        static void SetImage(VisualElement element, Texture2D image)
        {
            if (image == null)
            {
                element.style.display = DisplayStyle.None;
                return;
            }
            element.style.display = DisplayStyle.Flex;
            if (element.userData as Texture2D == image) return;
            element.userData = image;
            element.style.backgroundImage = new StyleBackground(image);
        }

        static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
