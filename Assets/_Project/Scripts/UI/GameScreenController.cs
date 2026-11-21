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
    /// 여기서는 위 막대 · 뉴스/힌트 · 반신 캠 · 아이템 막대 · 단계별 창(뉴스, 정산, 상점, 결과)만 다룬다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameScreenController : MonoBehaviour
    {
        [SerializeField] GameLoop game;

        MarketSession Session => game.Session;

        Label dayLabel, phaseLabel, debtLabel, rateLabel, loanCountLabel, netWorthLabel;
        VisualElement newsList, itemBarList;
        ScrollView hintList, shopList;
        Label faceLabel, moodLabel, outfitLabel, itemBarHint;
        VisualElement newsOverlay, pickerOverlay, settlementOverlay, shopOverlay, endOverlay;
        Label newsOverlayTitle, pickerTitle, settlementTitle, settlementSummary, settlementMessage, shopCash, shopMessage, endTitle, endBody;
        VisualElement newsOverlayList, pickerList;
        FloatField repayField;
        PriceChartElement chart;
        VisualElement portrait, endPortrait;
        string lastMessage = "";
        float hintTimer;
        VisualElement titleOverlay;
        Label titleStart;
        float titleBlinkTimer;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            KoreanFont.Apply(root);
            Query(root);
            Bind();
            BindTitle(root);

            game.Changed += RefreshAll;
            game.PhaseChanged += OnPhaseChanged;
            game.Message += OnMessage;
            game.Inventory.Changed += RefreshItemBar;
            Session.Portfolio.Changed += RefreshAll;

            OnPhaseChanged();
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.Changed -= RefreshAll;
            game.PhaseChanged -= OnPhaseChanged;
            game.Message -= OnMessage;
            if (game.Inventory != null) game.Inventory.Changed -= RefreshItemBar;
            if (Session != null && Session.Portfolio != null) Session.Portfolio.Changed -= RefreshAll;
        }

        void Update()
        {
            if (game.Inventory == null) return;
            BlinkTitle();
            RefreshStatus();
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
            phaseLabel = root.Q<Label>("phase-label");
            debtLabel = root.Q<Label>("debt-label");
            rateLabel = root.Q<Label>("rate-label");
            loanCountLabel = root.Q<Label>("loan-count-label");
            netWorthLabel = root.Q<Label>("networth-label");
            newsList = root.Q("news-list");
            hintList = root.Q<ScrollView>("hint-list");
            faceLabel = root.Q<Label>("face-label");
            portrait = root.Q("portrait");
            endPortrait = root.Q("end-portrait");
            moodLabel = root.Q<Label>("mood-label");
            outfitLabel = root.Q<Label>("outfit-label");
            itemBarList = root.Q("item-bar-list");
            itemBarHint = root.Q<Label>("item-bar-hint");
            chart = root.Q<PriceChartElement>("chart");

            newsOverlay = root.Q("news-overlay");
            newsOverlayTitle = root.Q<Label>("news-overlay-title");
            newsOverlayList = root.Q("news-overlay-list");

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

        void Bind()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Q<Button>("news-confirm-button").clicked += game.ConfirmNews;
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

        // ───────── 타이틀 ─────────

        /// <summary>시작할 때 타이틀이 맨 위에 덮여 있고, 아무 곳이나 클릭하면 닫혀 1일차 뉴스가 보인다.</summary>
        void BindTitle(VisualElement root)
        {
            titleOverlay = root.Q("title-overlay");
            titleStart = root.Q<Label>("title-start");
            if (titleOverlay == null) return;
            titleOverlay.BringToFront();
            titleOverlay.RegisterCallback<ClickEvent>(_ => Show(titleOverlay, false));
        }

        void BlinkTitle()
        {
            if (titleOverlay == null || titleOverlay.resolvedStyle.display == DisplayStyle.None) return;
            titleBlinkTimer -= Time.unscaledDeltaTime;
            if (titleBlinkTimer > 0f) return;
            titleBlinkTimer = 0.6f;
            titleStart.ToggleInClassList("title-start--dim");
        }

        // ───────── 이벤트 ─────────

        void OnPhaseChanged()
        {
            lastMessage = "";
            Show(pickerOverlay, false);
            Show(newsOverlay, game.Phase == GamePhase.News);
            Show(settlementOverlay, game.Phase == GamePhase.Settlement);
            Show(shopOverlay, game.Phase == GamePhase.Shop);
            Show(endOverlay, game.Phase == GamePhase.GameOver);
            chart.SetPreview(0);

            if (game.Phase == GamePhase.News) BuildNews();
            if (game.Phase == GamePhase.Settlement) repayField.value = 0f;
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
            dayLabel.text = $"{game.Day}일차";
            phaseLabel.text = game.Phase switch
            {
                GamePhase.News => "1 뉴스 탐색",
                GamePhase.Prep => "2 아이템 사용 · 예약",
                GamePhase.Market => "3 장 시간",
                GamePhase.Settlement => "4 장 결과",
                GamePhase.Shop => "5 아이템 구매",
                _ => "게임 끝",
            };
            debtLabel.text = Format.Money(loan.Debt);
            rateLabel.text = $"이자 {loan.InterestRate * 100f:0}% · 다음 {loan.NextInterestDay(game.Day)}일차";
            loanCountLabel.text = $"추가 대출 {loan.ExtraLoansLeft}회 남음";

            float netWorth = game.NetWorth();
            netWorthLabel.text = Format.SignedMoney(netWorth);
            Format.SetTrendClass(netWorthLabel, netWorth);

            float today = game.TodayReturn();
            var mood = game.Character.GetMood(today);
            bool beforeMarket = game.Phase is GamePhase.News or GamePhase.Prep;
            var image = beforeMarket && game.Character.prepImage != null ? game.Character.prepImage : game.Character.MoodImage(mood);
            SetImage(portrait, image);
            faceLabel.text = CharacterSettings.MoodFace(mood);
            faceLabel.style.display = image == null ? DisplayStyle.Flex : DisplayStyle.None;
            moodLabel.text = $"{CharacterSettings.MoodName(mood)} · 오늘 {Format.SignedPercent(today)}";
            Format.SetTrendClass(moodLabel, today);
            outfitLabel.text = game.Character.OutfitName(netWorth);
        }

        void BuildNews()
        {
            newsOverlayTitle.text = $"{game.Day}일차 아침 뉴스";
            newsOverlayList.Clear();
            newsList.Clear();
            foreach (var n in game.TodayNews)
            {
                var card = new Label(n.Text);
                card.AddToClassList("news-card");
                card.EnableInClassList("news-card--chain", n.Kind != NewsKind.Normal);
                newsOverlayList.Add(card);

                var line = new Label("· " + n.Text);
                line.AddToClassList("news-line");
                newsList.Add(line);
            }
        }

        void RefreshHints()
        {
            hintList.Clear();
            foreach (var h in game.Hints) AddHint(h);
            foreach (var a in game.ActiveAutoSells()) AddHint("[예약 중] " + a);
            if (game.StopRemaining > 0f) AddHint($"[시간 정지] {game.StopRemaining:0.0}초 남음");
            if (game.SlowRemaining > 0f) AddHint($"[슬로우 모션] {game.SlowRemaining:0.0}초 남음");
            if (game.PreviewTicks > 0) AddHint("[망원경] 차트의 주황 선이 앞으로의 가격");
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
            foreach (var (item, count) in owned)
            {
                var chip = new Button(() => OnItemClicked(item)) { text = $"{item.displayName} x{count}" };
                chip.AddToClassList("item-chip");
                chip.EnableInClassList("item-chip--off", !game.CanUseNow(item));
                chip.tooltip = item.description;
                itemBarList.Add(chip);
            }

            string guide = game.Phase switch
            {
                GamePhase.Prep => "정보 아이템을 쓰고, 매수·매도 예약 후 '장 시작'을 누르세요",
                GamePhase.Market => "장 시간용 아이템을 쓸 수 있어요",
                _ => owned.Count == 0 ? "아이템은 장이 끝난 뒤 상점에서 살 수 있어요" : "",
            };
            itemBarHint.text = string.IsNullOrEmpty(lastMessage) ? guide : lastMessage;
        }

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
