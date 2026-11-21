using System.Collections.Generic;
using System.Linq;
using BurningAnt.Market;
using UnityEngine;

namespace BurningAnt.News
{
    public enum NewsKind
    {
        Normal,       // 일반 뉴스 (±1~4%)
        ChainStart,   // 연쇄 1단계 (예: 계약 임박)
        ChainSuccess, // 연쇄 2단계 성공
        ChainFail,    // 연쇄 2단계 실패
    }

    public class NewsItem
    {
        public StockState Stock;
        public NewsKind Kind;
        public string Text;
        public float Effect; // 그날 결과에 더해지는 등락률 (0.03 = +3%)
    }

    /// <summary>진행 중인 연쇄 뉴스. 결과는 1단계가 나온 날 이미 정해져 있다 (연쇄 예측 아이템이 엿봄).</summary>
    public class ChainEvent
    {
        public StockState Stock;
        public bool WillSucceed;
        public int ResolveDay;
    }

    /// <summary>
    /// 그날 뉴스를 만들고, 뉴스를 반영해 종목별 오늘 가격 경로를 확정한다 (기획서 3장: 그래프는 뉴스 단계에서 확정).
    /// </summary>
    public class NewsGenerator
    {
        readonly NewsSettings settings;
        readonly System.Random rng;

        public ChainEvent ActiveChain { get; private set; }

        public NewsGenerator(NewsSettings settings, System.Random rng)
        {
            this.settings = settings;
            this.rng = rng;
        }

        public List<NewsItem> GenerateDay(int day, IReadOnlyList<StockState> stocks)
        {
            var news = new List<NewsItem>();
            var used = new HashSet<StockState>();

            // 어제 시작된 연쇄 뉴스의 결과 발표
            if (ActiveChain != null && ActiveChain.ResolveDay <= day)
            {
                var c = ActiveChain;
                news.Add(c.WillSucceed
                    ? Make(c.Stock, NewsKind.ChainSuccess, settings.chainSuccessNews, Range(settings.chainSuccessEffectMin, settings.chainSuccessEffectMax))
                    : Make(c.Stock, NewsKind.ChainFail, settings.chainFailNews, -Range(settings.chainFailEffectMin, settings.chainFailEffectMax)));
                used.Add(c.Stock);
                ActiveChain = null;
            }

            int count = rng.Next(settings.minNewsPerDay, settings.maxNewsPerDay + 1);
            count = Mathf.Min(count, stocks.Count);

            // 새 연쇄 뉴스 1단계
            if (ActiveChain == null && news.Count == 0 && news.Count < count && rng.NextDouble() < settings.chainStartChance)
            {
                var stock = PickUnused(stocks, used);
                news.Add(Make(stock, NewsKind.ChainStart, settings.chainStartNews, Range(settings.chainStartEffectMin, settings.chainStartEffectMax)));
                ActiveChain = new ChainEvent
                {
                    Stock = stock,
                    WillSucceed = rng.NextDouble() < settings.chainSuccessChance,
                    ResolveDay = day + 1,
                };
            }

            while (news.Count < count)
            {
                var stock = PickUnused(stocks, used);
                bool good = rng.NextDouble() < 0.5;
                float effect = Range(settings.effectMin, settings.effectMax);
                news.Add(Make(stock, NewsKind.Normal, good ? settings.goodNews : settings.badNews, good ? effect : -effect));
            }
            return news;
        }

        /// <summary>뉴스 영향을 더해 오늘 가격 경로를 만들어 각 종목에 넣는다. 장 전(PreMarket)에만 부른다.</summary>
        public void ApplyToPrices(IReadOnlyList<StockState> stocks, IReadOnlyList<NewsItem> news, MarketSettings market)
        {
            foreach (var stock in stocks)
            {
                float limit = stock.DailyLimit;
                float newsEffect = news.Where(n => n.Stock == stock).Sum(n => n.Effect);
                // 뉴스 없는 몫: 기존 PricePathGenerator와 같은 무작위 추세. 뉴스 몫을 더한 뒤 한도 안으로 자른다.
                float trend = Mathf.Clamp(Signed() * limit * market.maxTrendRatio + newsEffect, -limit, limit);
                // 시가 갭: 기획서에 규칙이 없어 기존 구현 그대로 (한도 × maxGapRatio 안에서 무작위)
                float gap = Signed() * limit * market.maxGapRatio;
                stock.PrepareDay(PricePathGenerator.Generate(stock.PrevClose, limit, gap, trend, market, rng));
            }
        }

        NewsItem Make(StockState stock, NewsKind kind, string[] templates, float effect)
        {
            string template = templates != null && templates.Length > 0 ? templates[rng.Next(templates.Length)] : "{0} 관련 소식";
            return new NewsItem { Stock = stock, Kind = kind, Text = string.Format(template, stock.Name), Effect = effect };
        }

        StockState PickUnused(IReadOnlyList<StockState> stocks, HashSet<StockState> used)
        {
            var candidates = stocks.Where(s => !used.Contains(s)).ToList();
            var pick = candidates.Count > 0 ? candidates[rng.Next(candidates.Count)] : stocks[rng.Next(stocks.Count)];
            used.Add(pick);
            return pick;
        }

        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        float Signed() => (float)(rng.NextDouble() * 2.0 - 1.0);
    }
}
