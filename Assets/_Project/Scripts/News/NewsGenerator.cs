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
        /// <summary>결과 기사(특보)가 뜨는 틱. 주가도 이 틱부터 움직인다 (MoveTick과 같음).</summary>
        public int RevealTick;
        /// <summary>주가가 움직이기 시작하는 틱. 지금은 특보와 동시 (= RevealTick).</summary>
        public int MoveTick;
        /// <summary>주가가 결과 값(Effect)에 다 닿는 틱. RevealTick과 같으면 한 번에 뛴다.</summary>
        public int SettleTick;
        /// <summary>움직이는 방식: 1 = 한 번에, 2~4 = 그 횟수만큼 계단식, 0 = 여러 틱에 걸쳐 차근차근.</summary>
        public int Steps = 1;
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
        /// <summary>특보가 뜬 뒤 주가가 움직이기 시작할 때까지 틱 수. 사용자 확정: 0 = 특보와 동시에 움직임.</summary>
        public const int MinFlashLeadTicks = 0, MaxFlashLeadTicks = 0;

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
                    ? PricePathGenerator.GenerateWithReveal(stock.PrevClose, stock.DailyLimit, item.MoveTick, item.Effect, market, rng, item.SettleTick, item.Steps)
                    : PricePathGenerator.Generate(stock.PrevClose, stock.DailyLimit, market, rng));
            }
        }

        /// <summary>뉴스 없던 종목 예지: 템플릿 하나와 결과를 뽑고 그 종목의 오늘 경로를 그 값으로 다시 만든다. 템플릿이 없으면 null.</summary>
        public NewsItem CreateForesight(StockState stock, MarketSettings market)
        {
            var item = Create(stock, NewsKind.Foresight, market.ticksPerDay);
            if (item == null) return null;
            item.Foreseen = true;
            stock.PrepareDay(PricePathGenerator.GenerateWithReveal(stock.PrevClose, stock.DailyLimit, item.MoveTick, item.Effect, market, rng, item.SettleTick, item.Steps));
            return item;
        }

        /// <summary>결과 변동률: 기본 구간 끝 + 템플릿 % (addBaseRange) 또는 템플릿 % 그대로. 주가가 0 밑으로 가지 않게 −95%까지.</summary>
        public float TargetChange(StockState stock, NewsTemplate t, bool success)
        {
            float percent = (success ? t.successPercent : t.failurePercent) / 100f;
            float sign = percent >= 0f ? 1f : -1f;
            float change;
            if (settings.useExtraRange)
            {
                // 사용자 확정: 뉴스(최초·결과 기사)로 움직이는 폭은 기본 구간보다 대략 50~200% 크다.
                // 템플릿 %를 기본 구간 대비 비율로 보고 그 비율만큼 더 움직인다 (0.5~2.0으로 자름).
                float extra = Mathf.Clamp(Mathf.Abs(percent) / Mathf.Max(0.01f, stock.DailyLimit), settings.extraMin, settings.extraMax);
                change = sign * stock.DailyLimit * (1f + extra);
            }
            else change = settings.addBaseRange ? sign * (stock.DailyLimit + Mathf.Abs(percent)) : percent;
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
            // 특보 시점: 장 진행 earliestReveal~latestReveal(0~100%) 구간에서 균등하게 무작위.
            // 특보가 뜨는 틱에 주가도 바로 움직이기 시작한다 (간격 0틱)
            int lead = rng.Next(MinFlashLeadTicks, MaxFlashLeadTicks + 1);
            int lastReveal = Mathf.Max(1, ticksPerDay - MaxFlashLeadTicks);
            int earliest = Mathf.Clamp(Mathf.RoundToInt(ticksPerDay * settings.earliestReveal), 1, lastReveal);
            int latest = Mathf.Clamp(Mathf.RoundToInt(ticksPerDay * settings.latestReveal), earliest, lastReveal);
            int revealTick = rng.Next(earliest, latest + 1);
            int moveTick = Mathf.Min(ticksPerDay, revealTick + lead);
            bool atClose = false; // 결과는 모두 장 중 특보로 뜬다

            // 움직이는 방식: 한 번에 30% · 계단식(2~4번) 35% · 차근차근 35%. 길이는 장 길이의 15~40% (7~24초, 눈에 띄게),
            // 장 마감 전에 다 닿도록 남은 시간에 맞춘다 (남은 시간이 짧으면 자연히 한 번에)
            int steps = 1, settleTick = moveTick;
            int room = ticksPerDay - moveTick;
            double roll = rng.NextDouble();
            if (roll >= 0.3 && room >= 4)
            {
                int span = Mathf.Max(3, Mathf.RoundToInt(ticksPerDay * (0.15f + (float)rng.NextDouble() * 0.25f)));
                settleTick = moveTick + Mathf.Min(span, room);
                steps = roll < 0.65 ? rng.Next(2, 5) : 0;
                if (steps > settleTick - moveTick + 1) steps = settleTick - moveTick + 1;
            }
            return new NewsItem
            {
                Stock = stock,
                Kind = kind,
                Template = template,
                Success = success,
                RevealAtClose = atClose,
                RevealTick = revealTick,
                MoveTick = moveTick,
                SettleTick = settleTick,
                Steps = steps,
                Effect = TargetChange(stock, template, success),
            };
        }
    }
}
