using System.Collections.Generic;
using System.Linq;
using BurningAnt.Market;
using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// StockWindow.uxml과 MarketSession을 연결한다. 화면 구조는 UXML, 모양은 USS, 규칙은 MarketSession에 있고
    /// 여기서는 이벤트를 받아 화면을 갱신하고 버튼 입력을 주문으로 바꾸기만 한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class StockWindowController : MonoBehaviour
    {
        [SerializeField] MarketSession session;

        readonly List<StockListItem> listItems = new();
        StockState selected;
        bool useMax;

        public StockState Selected => selected;
        // 업종 목록의 종목 + 맨 위 "보유 중" 상자의 종목 (같은 종목이 두 군데 있을 수 있다)
        readonly List<StockListItem> heldItems = new();
        readonly List<StockListItem> allItems = new();
        VisualElement heldBox;
        public IReadOnlyList<StockListItem> ListItems => allItems;
        public event System.Action SelectionChanged;

        Label holdingCountLabel, nameLabel, typeBadge, phaseBadge, priceLabel, changeLabel, gapLabel;
        Label upperLabel, lowerLabel, chartHint, sharesLabel, averageLabel, profitLabel, returnLabel;
        Label cashLabel, reservationLabel, messageLabel;
        Button cancelReservationButton;
        VisualElement listContainer, progressFill;
        PriceChartElement chart;
        IntegerField quantityField;
        Button maxButton, buyButton, sellButton, startButton;
        Button maxSellButton; // 최대 매도량

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            KoreanFont.Apply(root);
            QueryElements(root);
            BuildStockList();
            BindInput();

            session.PhaseChanged += OnPhaseChanged;
            session.Ticked += Refresh;
            session.ReservationsChanged += Refresh;
            session.ReservationsExecuted += OnReservationsExecuted;
            session.Portfolio.Changed += Refresh;

            Select(session.Stocks[0]);
        }

        void OnDestroy()
        {
            if (session == null) return;
            session.PhaseChanged -= OnPhaseChanged;
            session.Ticked -= Refresh;
            session.ReservationsChanged -= Refresh;
            session.ReservationsExecuted -= OnReservationsExecuted;
            if (session.Portfolio != null) session.Portfolio.Changed -= Refresh;
        }

        void QueryElements(VisualElement root)
        {
            holdingCountLabel = root.Q<Label>("holding-count");
            listContainer = root.Q("stock-list");
            if (listContainer is ScrollView sv)
            {
                sv.mode = ScrollViewMode.Vertical;
                sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }
            nameLabel = root.Q<Label>("stock-name");
            typeBadge = root.Q<Label>("stock-type");
            phaseBadge = root.Q<Label>("phase-badge");
            priceLabel = root.Q<Label>("price");
            changeLabel = root.Q<Label>("change");
            gapLabel = root.Q<Label>("gap");
            upperLabel = root.Q<Label>("upper-limit");
            lowerLabel = root.Q<Label>("lower-limit");
            progressFill = root.Q("progress-fill");
            chart = root.Q<PriceChartElement>("chart");
            chartHint = root.Q<Label>("chart-hint");
            sharesLabel = root.Q<Label>("stat-shares");
            averageLabel = root.Q<Label>("stat-average");
            profitLabel = root.Q<Label>("stat-profit");
            returnLabel = root.Q<Label>("stat-return");
            quantityField = root.Q<IntegerField>("quantity");
            maxButton = root.Q<Button>("max-button");
            maxSellButton = root.Q<Button>("max-sell-button");
            cashLabel = root.Q<Label>("cash");
            buyButton = root.Q<Button>("buy-button");
            sellButton = root.Q<Button>("sell-button");
            startButton = root.Q<Button>("start-button");
            reservationLabel = root.Q<Label>("reservations");
            cancelReservationButton = root.Q<Button>("cancel-reservation-button");
            messageLabel = root.Q<Label>("message");
        }

        readonly Dictionary<Sector, Label> groupHeaders = new();
        Sector? openSector;

        /// <summary>업종 이름만 보이고, 업종을 누르면 그 업종 종목이 펼쳐진다 (한 번에 한 업종).</summary>
        void BuildStockList()
        {
            listContainer.Clear();
            listItems.Clear();
            heldItems.Clear();
            groupHeaders.Clear();

            // 맨 위: 보유 중인 종목만 따로 모은 상자
            heldBox = new VisualElement();
            heldBox.AddToClassList("held-box");
            var heldTitle = new Label("보유 중");
            heldTitle.AddToClassList("held-box-title");
            heldBox.Add(heldTitle);
            foreach (var stock in session.Stocks)
            {
                var item = new StockListItem(stock, Select);
                heldItems.Add(item);
                heldBox.Add(item);
            }
            listContainer.Add(heldBox);
            foreach (var group in session.Stocks.GroupBy(s => s.Definition.sector).OrderBy(g => g.Key))
            {
                var sector = group.Key;
                var header = new Label();
                header.AddToClassList("stock-group");
                header.RegisterCallback<ClickEvent>(_ =>
                {
                    openSector = openSector == sector ? null : sector;
                    RefreshGroups();
                });
                groupHeaders[sector] = header;
                listContainer.Add(header);
                foreach (var stock in group)
                {
                    var item = new StockListItem(stock, Select);
                    listItems.Add(item);
                    listContainer.Add(item);
                }
            }
            openSector = session.Stocks[0].Definition.sector;
            allItems.Clear();
            allItems.AddRange(listItems);
            allItems.AddRange(heldItems);
        }

        void RefreshGroups()
        {
            foreach (var (sector, header) in groupHeaders)
            {
                bool open = openSector == sector;
                bool news = listItems.Any(i => i.Stock.Definition.sector == sector && i.HasBadge);
                bool held = listItems.Any(i => i.Stock.Definition.sector == sector && session.Portfolio.GetHolding(i.Stock).Shares > 0);
                string marks = (news ? "<color=#D85A30>● 뉴스</color>  " : "") + (held ? "<color=#378ADD>● 보유</color>" : "");
                header.text = (open ? "▾ " : "▸ ") + sector.ToKorean() + (marks.Length > 0 ? "\n<size=12>" + marks + "</size>" : "");
                header.EnableInClassList("stock-group--open", open);
            }
            foreach (var item in listItems)
                item.style.display = openSector == item.Stock.Definition.sector ? DisplayStyle.Flex : DisplayStyle.None;
            bool anyHeld = false;
            foreach (var item in heldItems)
            {
                bool held = session.Portfolio.GetHolding(item.Stock).Shares > 0;
                anyHeld |= held;
                item.style.display = held ? DisplayStyle.Flex : DisplayStyle.None;
            }
            heldBox.style.display = anyHeld ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Update()
        {
            // 단축키: Q 매수, W 매도 (수량 입력 중에는 무시)
            if (selected == null || IsTyping()) return;
            if (Input.GetKeyDown(KeyCode.Q) && CanPress(buyButton)) Submit(OrderSide.Buy);
            if (Input.GetKeyDown(KeyCode.W) && CanPress(sellButton)) Submit(OrderSide.Sell);
        }

        bool IsTyping() =>
            quantityField.panel?.focusController?.focusedElement is VisualElement focused &&
            (focused.GetFirstOfType<IntegerField>() != null || focused.GetFirstOfType<FloatField>() != null || focused.GetFirstOfType<TextField>() != null);

        static bool CanPress(Button button) =>
            button.enabledInHierarchy && button.resolvedStyle.display == DisplayStyle.Flex && button.visible;

        void BindInput()
        {
            buyButton.clicked += () => Submit(OrderSide.Buy);
            sellButton.clicked += () => Submit(OrderSide.Sell);
            // '최대': 지금 현금과 주가로 살 수 있는 최대 주식 수를 수량 칸에 채운다 (칸은 그대로 열려 있어 고칠 수 있음).
            // 장 전엔 시가를 모르므로 전날 종가 기준 (시가가 다르면 예약 체결 때 모자랄 수 있음)
            maxButton.clicked += FillMaxQuantity;
            if (maxSellButton != null) maxSellButton.clicked += FillMaxSellQuantity;
            if (cancelReservationButton != null)
                cancelReservationButton.clicked += () =>
                {
                    if (selected != null && session.CancelReservations(selected)) ShowMessage($"{selected.Name} 예약을 취소했어요");
                    Refresh();
                };
            quantityField.RegisterValueChangedCallback(_ => { useMax = false; ShowMessage(""); Refresh(); });
            startButton.clicked += () =>
            {
                ShowMessage("");
                if (session.Phase == MarketPhase.PreMarket) session.StartMarket();
                else if (session.Phase == MarketPhase.Closed) session.NextDay();
            };
        }

        void FillMaxQuantity()
        {
            if (selected == null) return;
            float price = session.CurrentPrice(selected);
            int max = session.Portfolio.MaxBuyable(price);
            useMax = false;
            quantityField.SetValueWithoutNotify(max); // 알림 없이 (값 변경 콜백이 안내 문구를 지우지 않게)
            ShowMessage(max > 0
                ? $"최대 매수량 {max:N0}주 ({(session.Phase == MarketPhase.PreMarket ? "전날 종가" : "현재가")} {Format.Money(price)} 기준)"
                : "현금이 부족해서 살 수 있는 주식이 없어요");
            Refresh();
        }

        /// <summary>'최대 매도량': 선택 종목의 보유 주식 수 전부를 수량 칸에 채운다.</summary>
        void FillMaxSellQuantity()
        {
            if (selected == null) return;
            int have = session.Portfolio.GetHolding(selected).Shares;
            useMax = false;
            quantityField.SetValueWithoutNotify(have);
            ShowMessage(have > 0 ? $"최대 매도량 {have:N0}주 (보유 전부)" : "이 종목은 보유한 주식이 없어요");
            Refresh();
        }

        void Select(StockState stock)
        {
            selected = stock;
            Refresh();
            SelectionChanged?.Invoke();
        }

        void Submit(OrderSide side)
        {
            if (!useMax && quantityField.value < 1)
            {
                ShowMessage("수량을 1주 이상 입력하세요");
                return;
            }

            var order = new OrderRequest(selected, side, quantityField.value, useMax);
            if (session.Phase == MarketPhase.PreMarket)
            {
                if (!session.Reserve(order)) { ShowMessage("지금은 예약할 수 없어요"); return; }
                // 앞 예약이 현금(또는 보유 주식)을 다 써서 이 예약이 체결되지 않을 거면 '예약했어요' 대신 그렇게 알린다
                var mine = session.EstimateReservationFills().FirstOrDefault(f => f.order.Stock == selected && f.order.Side == side);
                ShowMessage(mine.shares > 0
                    ? (mine.shares < mine.order.Quantity && !mine.order.UseMax
                        ? $"예약했어요 · 장 시작 때 {(side == OrderSide.Buy ? "살 수 있는" : "가진")} {mine.shares:N0}주만큼 체결될 예정 (입력 {mine.order.Quantity:N0}주)"
                        : "예약했어요")
                    : side == OrderSide.Buy ? "앞선 매수 예약이 현금을 다 써서 이 매수는 체결되지 않아요" : "팔 주식이 없어서 이 매도는 체결되지 않아요");
            }
            else
            {
                // 입력 수량이 가능한 수보다 많으면 가능한 만큼 최대로 체결된다 (MarketSession.CapToPossible)
                var r = session.Trade(order);
                string msg = Describe(r);
                if (r.Success && r.Shares < order.Quantity)
                    msg += $" (입력 {order.Quantity:N0}주 중 {(side == OrderSide.Buy ? "살 수 있는" : "가진")} {r.Shares:N0}주만)";
                ShowMessage(msg);
            }
        }

        void OnPhaseChanged()
        {
            if (session.Phase == MarketPhase.Closed) ShowMessage("장 마감");
            Refresh();
        }

        void OnReservationsExecuted(IReadOnlyList<TradeResult> results) =>
            ShowMessage("시가 체결: " + string.Join(" / ", results.Select(Describe)));

        void ShowMessage(string text) => messageLabel.text = text;

        string Describe(TradeResult r)
        {
            string side = r.Side == OrderSide.Buy ? "매수" : "매도";
            return r.Error switch
            {
                TradeError.None => $"{r.Stock.Name} {r.Shares}주 {side} @ {Format.Money(r.Price)}",
                TradeError.InvalidQuantity => "수량을 1주 이상 입력하세요",
                TradeError.NotEnoughCash when r.Side == OrderSide.Sell => $"{r.Stock.Name}: 마이너스를 메울 현금이 부족해서 팔 수 없어요",
                TradeError.NotEnoughCash => $"{r.Stock.Name}: 현금이 부족해요",
                TradeError.NotEnoughShares => $"{r.Stock.Name}: 보유 수량이 부족해요",
                TradeError.TooManyHoldings => $"{r.Stock.Name}: 최대 {session.Portfolio.MaxHoldings}종목까지 보유할 수 있어요",
                TradeError.MarketClosed => "장이 열려 있을 때만 거래할 수 있어요",
                _ => r.Error.ToString(),
            };
        }

        void Refresh()
        {
            if (selected == null) return;

            var phase = session.Phase;
            bool pre = phase == MarketPhase.PreMarket;
            var portfolio = session.Portfolio;

            foreach (var item in allItems)
                item.Refresh(session.CurrentPrice(item.Stock), pre, portfolio.GetHolding(item.Stock).Shares > 0, item.Stock == selected);
            holdingCountLabel.text = $"보유 {portfolio.HoldingCount}/{portfolio.MaxHoldings}";
            RefreshGroups();

            RefreshHeader(phase, pre);
            RefreshChart(pre);
            RefreshStats();
            RefreshOrderArea(phase, pre);
        }

        void RefreshHeader(MarketPhase phase, bool pre)
        {
            float price = session.CurrentPrice(selected);
            float change = price / selected.PrevClose - 1f;

            nameLabel.text = selected.Name;
            typeBadge.text = $"{selected.Definition.sector.ToKorean()} · 평소 ±{Mathf.RoundToInt(selected.DailyLimit * 100f)}% · 손익 {session.Portfolio.Leverage:0}배";

            // 단계·일차 표시는 전체 화면 오른쪽 위에서 따로 보여준다. 여기서는 장 중·마감만.
            phaseBadge.style.display = pre ? DisplayStyle.None : DisplayStyle.Flex;
            phaseBadge.text = phase == MarketPhase.Open ? $"장 중 · {Mathf.CeilToInt(session.RemainingSeconds)}초 남음" : "장 마감";
            phaseBadge.EnableInClassList("phase-badge--open", phase == MarketPhase.Open);

            priceLabel.text = Format.Money(price);
            Format.SetTrendClass(priceLabel, pre ? 0f : change);
            changeLabel.text = pre ? "전일 종가" : (change >= 0f ? "▲ " : "▼ ") + Format.SignedPercent(change);
            Format.SetTrendClass(changeLabel, pre ? 0f : change);
            gapLabel.text = pre ? "" : "· 시가 갭 " + Format.SignedPercent(selected.OpenPrice / selected.PrevClose - 1f);

            upperLabel.text = Format.Money(selected.UpperLimit);
            lowerLabel.text = Format.Money(selected.LowerLimit);

            int ticks = session.Settings.ticksPerDay;
            progressFill.style.width = Length.Percent(pre ? 0f : 100f * session.Tick / ticks);
        }

        void RefreshChart(bool pre)
        {
            var holding = session.Portfolio.GetHolding(selected);
            float? average = holding.Shares > 0 ? holding.AveragePrice : null;
            int visible = pre ? 0 : session.Tick + 1;
            chart.SetData(selected.PrevClose, selected.DailyLimit, selected.Path, visible, session.Settings.ticksPerDay, average);
            chartHint.style.display = pre ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void RefreshStats()
        {
            var holding = session.Portfolio.GetHolding(selected);
            float price = session.CurrentPrice(selected);
            float profit = holding.ProfitLoss(price);
            float rate = holding.ReturnRate(price);

            sharesLabel.text = $"{holding.Shares}주";
            averageLabel.text = holding.Shares > 0 ? Format.Money(holding.AveragePrice) : "-";
            profitLabel.text = Format.SignedMoney(profit);
            Format.SetTrendClass(profitLabel, profit);
            returnLabel.text = Format.SignedPercent(rate);
            Format.SetTrendClass(returnLabel, rate);
        }

        void RefreshOrderArea(MarketPhase phase, bool pre)
        {
            cashLabel.text = Format.Money(session.Portfolio.Cash);
            maxButton.EnableInClassList("max-button--on", useMax);
            quantityField.EnableInClassList("quantity-field--disabled", useMax);

            bool closed = phase == MarketPhase.Closed;
            buyButton.text = pre ? "매수 예약" : "매수";
            sellButton.text = pre ? "매도 예약" : "매도";
            buyButton.style.display = closed ? DisplayStyle.None : DisplayStyle.Flex;
            sellButton.style.display = closed ? DisplayStyle.None : DisplayStyle.Flex;
            // 장 중에는 시작·다음 날 버튼이 없다 (장은 멈출 수 없음).
            startButton.style.display = phase == MarketPhase.Open ? DisplayStyle.None : DisplayStyle.Flex;
            startButton.text = pre ? "장 시작" : "다음 날";

            // 예약 줄에는 장 시작 때 실제로 체결될 예약만 (앞 예약이 현금을 다 쓰면 뒤 매수 예약은 안 보임)
            var fills = session.EstimateReservationFills().Where(f => f.shares > 0).ToList();
            reservationLabel.text = fills.Count == 0
                ? (session.Reservations.Count > 0 ? "예약한 주문은 현금·보유 수량이 없어 체결되지 않아요" : "")
                : "예약: " + string.Join(", ", fills.Select(f =>
                      $"{f.order.Stock.Name} {(f.order.Side == OrderSide.Buy ? "매수" : "매도")} {(f.order.UseMax ? "최대" : f.shares.ToString("N0") + "주")}"))
                  + "\n장 시작 때 시가로 자동 체결 (전날 종가 기준 예상, 시가에 따라 수량이 조금 달라질 수 있어요)";
            bool hasReservation = pre && selected != null && session.Reservations.Any(o => o.Stock == selected);
            if (cancelReservationButton != null)
            {
                cancelReservationButton.style.display = hasReservation ? DisplayStyle.Flex : DisplayStyle.None;
                if (selected != null) cancelReservationButton.text = $"{selected.Name} 예약 취소";
            }
        }
    }
}
