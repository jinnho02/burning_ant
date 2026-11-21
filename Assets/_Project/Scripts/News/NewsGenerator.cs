using System.Collections.Generic;
using System.Linq;
using BurningAnt.Market;
using UnityEngine;

namespace BurningAnt.News
{
    public enum NewsKind
    {
        Normal,    // 1단계 뉴스: 모두에게 공개 (결과는 숨김)
        Foresight, // 예지 아이템으로만 알게 된 템플릿 (뉴스 없던 종목)
    }

    /// <summary>오늘 한 종목에 걸린 기사 템플릿과 그 결과. 결과는 처음부터 정해져 있고 공개만 나중에 된다.</summary>
    public class NewsItem
    {
        public StockState Stock;
        public NewsKind Kind;
        public NewsTemplate Template;
        public bool Success;
        /// <summary>결과 기사가 뜨고 주가가 고정값에 닿는 틱 (장 마감 = 틱 수).</summary>
        public int RevealTick;
        public bool RevealAtClose;
        public bool Revealed;
        public bool Foreseen;
        /// <summary>전날 종가 대비 고정 변동률 (0.43 = +43%).</summary>
        public float Effect;

        public string Text => $"{Stock.Name}, {Template.initialHeadline}";
        public string ResultText => $"{Stock.Name}, {(Success ? Template.successHeadline : Template.failureHeadline)}";
    }

    /// <summary>
    /// 하루 뉴스: 무작위 종목 newsPerDay개에 그 업종 템플릿을 하나씩 건다 (같은 날 같은 기사 두 번 안 씀).
    /// 뉴스 종목은 템플릿 결과대로, 나머지는 기본 변동률 구간 안에서 가격 경로를 만든다. 예지는 템플릿을 하나 더 꺼내 고정한다.
    /// </summary>
    public class NewsGenerator
    {
        readonly NewsSettings settings;
        readonly System.Random rng;
        readonly HashSet<NewsTemplate> usedToday = new();

        public NewsGenerator(NewsSettings settings, System.Random rng)
        {
            this.settings = settings;
            this.rng = rng;
        }

        public List<NewsItem> GenerateDay(int day, IReadOnlyList<StockState> stocks, int ticksPerDay)
        {
            usedToday.Clear();
            var order = stocks.OrderBy(_ => rng.Next()).ToList();
            var news = new List<NewsItem>();
            foreach (var stock in order)
            {
                if (news.Count >= settings.newsPerDay) break;
                var item = Create(stock, NewsKind.Normal, ticksPerDay);
                if (item != null) news.Add(item);
            }
            return news;
        }

        /// <summary>오늘 가격 경로를 정한다. 장 전(PreMarket)에만 부른다.</summary>
        public void ApplyToPrices(IReadOnlyList<StockState> stocks, IReadOnlyList<NewsItem> news, MarketSettings market)
        {
            foreach (var stock in stocks)
            {
                var item = news.FirstOrDefault(n => n.Stock == stock);
                stock.PrepareDay(item != null
                    ? PricePathGenerator.GenerateWithReveal(stock.PrevClose, stock.DailyLimit, item.RevealTick, item.Effect, market, rng)
                    : PricePathGenerator.Generate(stock.PrevClose, stock.DailyLimit, market, rng));
            }
        }

        /// <summary>뉴스 없던 종목 예지: 템플릿 하나와 결과를 뽑고 그 종목의 오늘 경로를 그 값으로 다시 만든다. 템플릿이 없으면 null.</summary>
        public NewsItem CreateForesight(StockState stock, MarketSettings market)
        {
            var item = Create(stock, NewsKind.Foresight, market.ticksPerDay);
            if (item == null) return null;
            item.Foreseen = true;
            stock.PrepareDay(PricePathGenerator.GenerateWithReveal(stock.PrevClose, stock.DailyLimit, item.RevealTick, item.Effect, market, rng));
            return item;
        }

        /// <summary>결과 변동률: 기본 구간 끝 + 템플릿 % (addBaseRange) 또는 템플릿 % 그대로. 주가가 0 밑으로 가지 않게 −95%까지.</summary>
        public float TargetChange(StockState stock, NewsTemplate t, bool success)
        {
            float percent = (success ? t.successPercent : t.failurePercent) / 100f;
            float change = settings.addBaseRange ? Mathf.Sign(percent) * (stock.DailyLimit + Mathf.Abs(percent)) : percent;
            return Mathf.Max(-0.95f, change);
        }

        NewsItem Create(StockState stock, NewsKind kind, int ticksPerDay)
        {
            var candidates = settings.library
                .Where(l => l != null && l.sector == stock.Definition.sector)
                .SelectMany(l => l.templates)
                .Where(t => !usedToday.Contains(t))
                .ToList();
            if (candidates.Count == 0) return null;

            var template = candidates[rng.Next(candidates.Count)];
            usedToday.Add(template);
            bool success = rng.NextDouble() < settings.successChance;
            bool atClose = rng.NextDouble() >= settings.intradayRevealChance;
            int earliest = Mathf.Clamp(Mathf.RoundToInt(ticksPerDay * settings.earliestReveal), 1, ticksPerDay - 1);
            int latest = Mathf.Clamp(Mathf.RoundToInt(ticksPerDay * settings.latestReveal), earliest, ticksPerDay - 1);
            return new NewsItem
            {
                Stock = stock,
                Kind = kind,
                Template = template,
                Success = success,
                RevealAtClose = atClose,
                RevealTick = atClose ? ticksPerDay : rng.Next(earliest, latest + 1),
                Effect = TargetChange(stock, template, success),
            };
        }
    }
}
