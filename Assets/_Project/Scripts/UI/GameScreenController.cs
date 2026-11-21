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

        MarketSession Session => game.Session;
        StockWindowController stockWindow;

        Label dayLabel, repayLabel, cashLabel, stockValueLabel, debtLabel, interestLabel, loanCountLabel, netWorthLabel;
        readonly Label[] steps = new Label[5];
        VisualElement repayFill, itemBarList;
        ScrollView hintList, shopList;
        Label faceLabel, speechLabel, moodLabel, outfitLabel, itemBarHint;
        VisualElement newsBand, newsBandList;
        Label newsBandTitle;
        Button newsConfirmButton, buyButton, sellButton, startButton;
        VisualElement pickerOverlay, settlementOverlay, shopOverlay, endOverlay;
        Label pickerTitle, settlementTitle, settlementSummary, settlementMessage, shopCash, shopMessage, endTitle, endBody;
        VisualElement pickerList;
        FloatField repayField;
        PriceChartElement chart;
        VisualElement portrait, endPortrait;
        string lastMessage = "";
        float hintTimer;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            KoreanFont.Apply(root);
            stockWindow = GetComponent<StockWindowController>();
            Query(root);
            Bind(root);

            // 뉴스 띠는 차트 바로 위(주식 창 오른쪽 카드 맨 위)에 둔다
            root.Q("detail-panel")?.Insert(0, newsBand);

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

            pickerOverlay = root.Q("picker-overlay");
            pickerTitle = root.Q<Label>("picker-title");
            pickerList = root.Q("picker-list");

            settlementOverlay = root.Q("settlement-overlay");
            settlementTitle = root.Q<Label>("settlement-title");
            settlementSummary = root.Q<Label>("settlement-summary");
            settlementMessage = root.Q<Label>("settlement-message");
            repayField = root.Q<FloatField>("repay-field");

            shopOverlay = root.Q("shop-overlay");
            shopList = root.Q<ScrollView>("shop-list");
            shopCash = root.Q<Label>("shop-cash");
            shopMessage = root.Q<Label>("shop-message");

            endOverlay = root.Q("end-overlay");
            endTitle = root.Q<Label>("end-title");
            endBody = root.Q<Label>("end-body");
        }

        void Bind(VisualElement root)
        {
            newsConfirmButton.clicked += game.ConfirmNews;
            root.Q<Button>("picker-cancel-button").clicked += () => Show(pickerOverlay, false);
            root.Q<Button>("repay-button").clicked += () => game.Repay(repayField.value);
            root.Q<Button>("repay-all-button").clicked += () => game.Repay(Session.Portfolio.Cash);
            root.Q<Button>("loan-button").clicked += game.TakeExtraLoan;
            root.Q<Button>("to-shop-button").clicked += game.GoToShop;
            root.Q<Button>("next-day-button").clicked += game.StartNextDay;
            root.Q<Button>("restart-button").clicked += () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(gameObject.scene.name); // 씬이 Build Profiles 목록에 있어야 한다 (셋업 메뉴가 추가)
            };
        }

        // ───────── 이벤트 ─────────

        void OnPhaseChanged()
        {
            lastMessage = "";
            Show(pickerOverlay, false);
            Show(settlementOverlay, game.Phase == GamePhase.Settlement);
            Show(shopOverlay, game.Phase == GamePhase.Shop);
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
            new[] { pickerOverlay, settlementOverlay, shopOverlay, endOverlay }.Any(o => o.resolvedStyle.display == DisplayStyle.Flex);

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

        void RefreshSettlement()
        {
            var portfolio = Session.Portfolio;
            float total = game.TotalAssets();
            float todayProfit = total - game.DayStartAssets;

            settlementTitle.text = $"{game.Day}일차 장 결과";
            string interest = game.LastInterest > 0f
                ? $"\n오늘은 이자일: 빚에 {Format.Money(game.LastInterest)} 추가됨"
                : $"\n다음 이자일: {game.Loan.NextInterestDay(game.Day + 1)}일차";
            settlementSummary.text =
                $"오늘 손익 {Format.SignedMoney(todayProfit)} ({Format.SignedPercent(game.TodayReturn())})\n" +
                $"현금 {Format.Money(portfolio.Cash)} · 주식 {Format.Money(game.StockValue())}\n" +
                $"남은 빚 {Format.Money(game.Loan.Debt)} (이자율 {game.Loan.InterestRate * 100f:0}%)" +
                interest +
                "\n\n보유 주식은 다음 날로 이어져요. 원하는 만큼 갚을 수 있어요.";
        }

        void RefreshShop()
        {
            shopCash.text = "현금 " + Format.Money(Session.Portfolio.Cash);
            shopList.Clear();
            foreach (var item in game.ShopItems)
            {
                var row = new VisualElement();
                row.AddToClassList("shop-row");

                var info = new VisualElement();
                info.AddToClassList("shop-info");
                var title = new VisualElement();
                title.AddToClassList("row");
                var name = new Label(item.displayName);
                name.AddToClassList("shop-name");
                var tag = new Label(item.Timing == ItemTiming.Prep ? "아이템 시간용" : "장 시간용");
                tag.AddToClassList("shop-tag");
                title.Add(name);
                title.Add(tag);
                var desc = new Label(item.description);
                desc.AddToClassList("shop-desc");
                info.Add(title);
                info.Add(desc);

                var price = new Label(Format.Money(item.price));
                price.AddToClassList("shop-price");
                var owned = new Label($"보유 {game.Inventory.Count(item)}");
                owned.AddToClassList("shop-owned");
                var buy = new Button(() => game.Buy(item)) { text = "구매" };
                buy.AddToClassList("shop-buy");
                buy.SetEnabled(Session.Portfolio.Cash >= item.price);

                row.Add(info);
                row.Add(price);
                row.Add(owned);
                row.Add(buy);
                shopList.Add(row);
            }
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
