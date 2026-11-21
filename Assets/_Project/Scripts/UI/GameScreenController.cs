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

        [Header("타이틀 '클릭하여 시작하기' 위치 (Play 중에 고쳐도 바로 반영)")]
        [SerializeField, Range(0f, 1f), Tooltip("글자 윗변 높이. 타이틀 그림 높이 대비 비율 (0 = 그림 맨 위, 1 = 맨 아래)")]
        float titleTextTop = 0.77f;
        [SerializeField, Range(0f, 1f), Tooltip("글자 가로 가운데. 타이틀 그림 폭 대비 비율 (0 = 왼쪽 끝, 1 = 오른쪽 끝)")]
        float titleTextCenterX = 0.765f;
        [SerializeField, Range(0.01f, 0.1f), Tooltip("글자 크기. 타이틀 그림 높이 대비 비율")]
        float titleTextSize = 0.026f;

        MarketSession Session => game.Session;
        StockWindowController stockWindow;

        Label dayLabel, repayLabel, cashLabel, stockValueLabel, debtLabel, interestLabel, loanCountLabel, netWorthLabel;
        readonly Label[] steps = new Label[5];
        VisualElement repayFill, itemBarList;
        ScrollView hintList;
        Label faceLabel, speechLabel, moodLabel, outfitLabel, itemBarHint;
        VisualElement newsBand, newsBandList;
        Label newsBandTitle;
        Button newsConfirmButton, buyButton, sellButton, startButton;
        VisualElement pickerOverlay, settlementOverlay, shopOverlay, endOverlay, emergencyOverlay;
        Label emergencyBody;
        Button loanButton;
        Label pickerTitle, settlementTitle, settlementSummary, settlementMessage, shopCash, shopMessage, endTitle, endBody;
        VisualElement pickerList;
        FloatField repayField;
        PriceChartElement chart;
        VisualElement portrait, endPortrait;
        string lastMessage = "";
        float hintTimer;
        VisualElement titleOverlay;
        VisualElement shopStage, shopGrid, shopTooltip;
        Label shopTipName, shopTipTag, shopTipDesc, shopTipPrice, shopTipOwned;
        ItemDefinition hoveredShopItem;
        VisualElement newsOverlay;
        ScrollView newsPaperList;
        Label newsPaperDate;
        Label titleStart;
        float titleBlinkTimer;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            KoreanFont.Apply(root);
            stockWindow = GetComponent<StockWindowController>();
            Query(root);
            Bind(root);

            // 뉴스 띠는 차트 바로 위(주식 창 오른쪽 카드 맨 위)에 둔다
            root.Q("detail-panel")?.Insert(0, newsBand);
            BindTitle(root);

            game.Changed += RefreshAll;
            game.PhaseChanged += OnPhaseChanged;
            game.Message += OnMessage;
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
        }

        void Update()
        {
            if (game.Inventory == null) return;
            BlinkTitle();
            RefreshStatus();
            HandleItemKeys();
            if (game.Phase == GamePhase.Market)
            {
                chart.SetPreview(game.PreviewTicks);
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
            debtLabel = root.Q<Label>("debt-label");
            interestLabel = root.Q<Label>("interest-label");
            loanCountLabel = root.Q<Label>("loan-count-label");
            netWorthLabel = root.Q<Label>("networth-label");
            hintList = root.Q<ScrollView>("hint-list");

            portrait = root.Q("portrait");
            faceLabel = root.Q<Label>("face-label");
            speechLabel = root.Q<Label>("speech-label");
            moodLabel = root.Q<Label>("mood-label");
            outfitLabel = root.Q<Label>("outfit-label");
            endPortrait = root.Q("end-portrait");

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
            endTitle = root.Q<Label>("end-title");
            endBody = root.Q<Label>("end-body");
        }

        void Bind(VisualElement root)
        {
            newsConfirmButton.clicked += game.ConfirmNews;
            root.Q<Button>("news-paper-button").clicked += game.ConfirmNews;
            root.Q<Button>("picker-cancel-button").clicked += () => Show(pickerOverlay, false);
            root.Q<Button>("repay-button").clicked += () => game.Repay(repayField.value);
            root.Q<Button>("repay-all-button").clicked += () => game.Repay(Session.Portfolio.Cash);
            loanButton.clicked += game.TakeExtraLoan;
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

        void OnTitleClicked()
        {
            if (!Prologue.PrologueController.HasSeen && Application.CanStreamedLevelBeLoaded(Prologue.PrologueController.SceneName))
                SceneManager.LoadScene(Prologue.PrologueController.SceneName);
            else
                Show(titleOverlay, false);
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
            Show(endOverlay, game.Phase == GamePhase.GameOver);
            chart.SetPreview(0);

            if (game.Phase == GamePhase.Settlement) repayField.value = 0f;
            RefreshNews();
            RefreshAll();
        }

        void OnMessage(string text)
        {
            lastMessage = text;
            settlementMessage.text = text;
            shopMessage.text = text;
            RefreshItemBar();
        }

        // ───────── 갱신 ─────────

        void RefreshAll()
        {
            RefreshStatus();
            RefreshHints();
            RefreshItemBar();
            // 정산 후 잔고가 0 밑이면 정산 창 대신 긴급 대출 창 (고르기 전까지 4단계로 못 감)
            Show(emergencyOverlay, game.NeedsEmergency);
            Show(settlementOverlay, game.Phase == GamePhase.Settlement && !game.NeedsEmergency);
            if (game.NeedsEmergency) RefreshEmergency();
            if (game.Phase == GamePhase.Settlement) RefreshSettlement();
            if (game.Phase == GamePhase.Shop) RefreshShop();
            if (game.Phase == GamePhase.GameOver) RefreshEnd();
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
            steps[2].text = now == 2 ? $"3 장 · {Mathf.CeilToInt(Mathf.Max(0f, Session.RemainingSeconds))}초" : "3 장";

            // 빚 상환 게이지 = 갚은 돈 / (갚은 돈 + 남은 빚)
            float repaid = loan.TotalRepaid;
            float total = repaid + loan.Debt;
            repayLabel.text = $"{Format.Money(repaid)} / {Format.Money(total)}";
            repayFill.style.width = Length.Percent(total > 0f ? 100f * repaid / total : 100f);

            cashLabel.text = Format.Money(Session.Portfolio.Cash);
            stockValueLabel.text = Format.Money(game.StockValue());
            debtLabel.text = Format.Money(loan.Debt);
            int daysLeft = loan.NextInterestDay(game.Day) - game.Day;
            interestLabel.text = $"{(daysLeft == 0 ? "오늘" : $"D-{daysLeft}")} · {loan.InterestRate * 100f:0}%";
            loanCountLabel.text = $"{loan.ExtraLoansLeft}회 남음";
            loanButton.SetEnabled(game.Phase == GamePhase.Prep && loan.CanTakeExtraLoan); // 추가 대출은 2단계에서만

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
            outfitLabel.text = game.Character.OutfitName(netWorth);

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
                line.EnableInClassList("news-paper-item--chain", n.Kind != NewsKind.Normal);
                newsPaperList.Add(line);
            }
        }

        void RefreshNews()
        {
            if (stockWindow != null)
                foreach (var item in stockWindow.ListItems)
                    item.SetBadge(game.TodayNews.Any(n => n.Stock == item.Stock) ? "뉴스" : null);

            bool newsPhase = game.Phase == GamePhase.News;
            var selected = stockWindow != null ? stockWindow.Selected : null;
            var lines = newsPhase
                ? game.TodayNews.ToList()
                : game.TodayNews.Where(n => n.Stock == null || n.Stock == selected).ToList();

            newsBandList.Clear();
            foreach (var n in lines)
            {
                var line = new Label("· " + n.Text);
                line.AddToClassList("news-band-line");
                line.EnableInClassList("news-band-line--chain", n.Kind != NewsKind.Normal);
                newsBandList.Add(line);
            }
            newsBandTitle.text = newsPhase ? $"{game.Day}일차 아침 뉴스" : "이 종목 오늘의 뉴스";
            Show(newsConfirmButton, newsPhase);
            Show(newsBand, newsPhase || lines.Count > 0);
            newsBand.EnableInClassList("news-band--alert", newsPhase);
        }

        void RefreshHints()
        {
            hintList.Clear();
            foreach (var h in game.Hints) AddHint(h);
            foreach (var a in game.ActiveAutoSells()) AddHint("[예약 중] " + a);
            if (game.StopRemaining > 0f) AddHint($"[시간 정지] {game.StopRemaining:0.0}초 남음");
            if (game.SlowRemaining > 0f) AddHint($"[슬로우 모션] {game.SlowRemaining:0.0}초 남음");
            if (game.PreviewTicks > 0) AddHint("[망원경] 차트의 주황 선이 앞으로의 가격");
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
                slot.tooltip = item.description;
                string key = i < 9 ? $"[{i + 1}] " : "";
                var name = new Label($"{key}{item.displayName} x{count}");
                name.AddToClassList("item-slot__name");
                var meta = new Label(item.Timing == ItemTiming.Prep ? "장 전" : "장 중");
                meta.AddToClassList("item-slot__meta");
                slot.Add(name);
                slot.Add(meta);
                itemBarList.Add(slot);
            }

            string guide = game.Phase switch
            {
                GamePhase.News => "뉴스를 읽고 '확인'을 누르세요",
                GamePhase.Prep => "정보 아이템을 쓰고, 매수·매도 예약 후 '장 시작'",
                GamePhase.Market => "장 중 아이템 사용 가능 · 매수 Q · 매도 W",
                _ => owned.Count == 0 ? "아이템은 장이 끝난 뒤 상점에서 살 수 있어요" : "",
            };
            itemBarHint.text = string.IsNullOrEmpty(lastMessage) ? guide : lastMessage;
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
            new[] { pickerOverlay, settlementOverlay, shopOverlay, endOverlay, titleOverlay, newsOverlay, emergencyOverlay }.Any(o => o != null && o.resolvedStyle.display == DisplayStyle.Flex);

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
            emergencyBody.text =
                $"장 마감 정산에서 마이너스 종목({string.Join(", ", game.LastClosedStocks.Select(s => s.Name))}) 때문에 " +
                $"잔고에서 {Format.Money(game.LastSettlementCharge)}이 빠져, 현금이 {Format.SignedMoney(-shortfall)}이에요.\n\n" +
                $"햇살론을 받으면 {Format.Money(shortfall + game.LoanSettings.emergencyLoanBuffer)}을 빌려 살아남지만, " +
                $"이자율이 {loan.InterestRate * 100f:0}% → {(loan.InterestRate + game.LoanSettings.emergencyRateStep) * 100f:0}%로 올라요.\n" +
                "한 판에 한 번뿐이에요. 안 받으면 파산이에요.";
        }

        void RefreshSettlement()
        {
            var portfolio = Session.Portfolio;
            float total = game.TotalAssets();
            float todayProfit = total - game.DayStartAssets;

            settlementTitle.text = $"{game.Day}일차 장 결과";
            string closed = game.LastSettlementCharge > 0f
                ? $"마이너스 정산: {string.Join(", ", game.LastClosedStocks.Select(s => s.Name))} 정리, 잔고에서 {Format.Money(game.LastSettlementCharge)} 빠짐\n"
                : "";
            string interest = game.LastInterest > 0f
                ? $"\n오늘은 이자일: 빚에 {Format.Money(game.LastInterest)} 추가됨"
                : $"\n다음 이자일: {game.Loan.NextInterestDay(game.Day + 1)}일차";
            settlementSummary.text =
                closed +
                $"오늘 손익 {Format.SignedMoney(todayProfit)} ({Format.SignedPercent(game.TodayReturn())})\n" +
                $"현금 {Format.Money(portfolio.Cash)} · 주식 {Format.Money(game.StockValue())}\n" +
                $"남은 빚 {Format.Money(game.Loan.Debt)} (이자율 {game.Loan.InterestRate * 100f:0}%)" +
                interest +
                "\n\n보유 주식은 다음 날로 이어져요. 원하는 만큼 갚을 수 있어요.";
        }

        // 상점 그림(store.png, 1672×941)에서 가방 안감 바닥면: 위가 좁고 아래가 넓은 사다리꼴 (그림 대비 비율)
        const float StoreAspect = 1672f / 941f;
        const float FloorTopY = 0.319f, FloorTopLeft = 0.090f, FloorTopRight = 0.772f;
        const float FloorBottomY = 0.840f, FloorBottomLeft = 0.018f, FloorBottomRight = 0.819f;
        const float FloorDepthCurve = 1.2f; // 1보다 크면 안쪽(위) 줄이 납작해져 멀어 보인다

        // 바닥면 위 칸 자리 (바닥 기준 u: 왼→오, v: 안쪽→앞쪽, 0~1)
        static readonly float[] ShopColumnsU = { 0.07f, 0.305f, 0.54f, 0.775f };
        static readonly float[] ShopRowsV = { 0.12f, 0.55f };
        const float ShopSlotU = 0.185f, ShopSlotV = 0.27f, ShopNameGap = 0.006f, ShopNameHeight = 0.04f;

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
                slot.EnableInClassList("shop-slot--poor", Session.Portfolio.Cash < item.price);
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
            shopTipTag.text = item.Timing == ItemTiming.Prep ? "아이템 시간용" : "장 시간용";
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

        void RefreshEnd()
        {
            SetImage(endPortrait, game.Won
                ? game.Character.winImage
                : game.Character.MoodImage(Mood.Crash));
            if (game.Won)
            {
                endTitle.text = "빚 청산! 무리가 아니었다?!";
                endBody.text = $"{game.Day}일 만에 빚을 모두 갚았어요.\n점수: {game.Day}일 (적을수록 좋아요)\n남은 현금 {Format.Money(Session.Portfolio.Cash)}";
            }
            else
            {
                endTitle.text = "파산…";
                endBody.text = $"{game.Day}일차에 총자산이 {Format.Money(game.TotalAssets())}로 바닥나고 추가 대출도 다 썼어요.\n남은 빚 {Format.Money(game.Loan.Debt)}";
            }
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
