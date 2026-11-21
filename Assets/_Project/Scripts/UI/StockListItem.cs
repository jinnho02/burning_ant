using System;
using BurningAnt.Market;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>왼쪽 종목 리스트의 한 줄: 이름 · 보유 표시 / 가격 · 등락률.</summary>
    public class StockListItem : VisualElement
    {
        readonly Label nameLabel = new() { name = "stock-item-name" };
        readonly Label heldLabel = new("보유") { name = "stock-item-held" };
        readonly Label badgeLabel = new() { name = "stock-item-badge" };
        readonly Label priceLabel = new() { name = "stock-item-price" };
        readonly Label changeLabel = new() { name = "stock-item-change" };

        public StockState Stock { get; }

        public StockListItem(StockState stock, Action<StockState> onSelect)
        {
            Stock = stock;
            AddToClassList("stock-item");
            nameLabel.AddToClassList("stock-item__name");
            heldLabel.AddToClassList("stock-item__held");
            priceLabel.AddToClassList("stock-item__price");
            changeLabel.AddToClassList("stock-item__change");
            badgeLabel.AddToClassList("stock-item__badge");
            badgeLabel.style.display = DisplayStyle.None;

            var nameRow = new VisualElement();
            nameRow.AddToClassList("row");
            nameRow.Add(nameLabel);
            nameRow.Add(badgeLabel);

            var top = new VisualElement();
            top.AddToClassList("row");
            top.AddToClassList("row--spread");
            top.Add(nameRow);
            top.Add(heldLabel);

            var bottom = new VisualElement();
            bottom.AddToClassList("row");
            bottom.AddToClassList("row--spread");
            bottom.Add(priceLabel);
            bottom.Add(changeLabel);

            Add(top);
            Add(bottom);
            nameLabel.text = stock.Name;
            RegisterCallback<ClickEvent>(_ => onSelect(stock));
        }

        /// <summary>이름 옆 작은 표시 (예: "뉴스"). null이면 숨김. kind는 USS 클래스 접미사.</summary>
        public bool HasBadge { get; private set; }

        public void SetBadge(string text, string kind = "news")
        {
            HasBadge = !string.IsNullOrEmpty(text);
            badgeLabel.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            badgeLabel.text = text ?? "";
            badgeLabel.EnableInClassList("stock-item__badge--news", kind == "news");
            badgeLabel.EnableInClassList("stock-item__badge--hint", kind == "hint");
        }

        public void Refresh(float price, bool preMarket, bool held, bool selected)
        {
            EnableInClassList("stock-item--selected", selected);
            heldLabel.style.display = DisplayStyle.None; // 보유 종목은 목록 맨 위 "보유 중" 상자에 따로 모아 보여준다
            priceLabel.text = Format.Money(price);

            float change = price / Stock.PrevClose - 1f;
            changeLabel.text = preMarket ? "전일 종가" : Format.SignedPercent(change);
            Format.SetTrendClass(changeLabel, preMarket ? 0f : change);
        }
    }
}
