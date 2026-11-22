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
        Breaking,  // 돌발 특보: 아침 뉴스에 없던 종목에 장 중 갑자기 뜨는 기사
    }

    /// <summary>
    /// 뉴스 결과까지 가는 가격 모양. 지금 보이는 고점·저점이 끝인지 더 갈지 확신할 수 없게 섞는다.
    /// Jump = 한 번에 · Overshoot = 넘겼다가 돌아옴 · Approach = 넘기지 않고 되돌림 섞으며 다가감 ·
    /// LateSurge = 한동안 조금만 움직이다 막판에 몰아서 · Trend = 장 마감까지 계속 한 방향으로 밀림
    /// </summary>
    public enum MoveShape { Jump, Overshoot, Approach, LateSurge, Trend }

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
        /// <summary>오늘 장 중에 후속 기사(특보)가 뜨는지. false면 그 종목은 평소처럼만 움직이고 결과창에도 후속이 없다.</summary>
        public bool HasFollowUp = true;
        /// <summary>반전 결과: 기사(성공·실패)와 반대로 움직임. 근거는 ReversalReason.</summary>
        public bool Reversal;
        public string ReversalReason;
        /// <summary>굴곡 경로의 중간 최고점 (최종 변동 대비 배수). Shape가 Overshoot일 때만 1보다 크다.</summary>
        public float Overshoot = 1f;
        /// <summary>가격이 결과 값까지 가는 모양 (읽히지 않게 섞는다).</summary>
        public MoveShape Shape = MoveShape.Jump;

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

        /// <summary>
        /// 하루 기사: 아침 뉴스 newsPerDay개 (그중 followUpChance만큼만 장 중 후속 특보가 뜸) +
        /// 아침 뉴스에 없던 종목의 돌발 특보 breakingMin~breakingMax개 (Kind = Breaking, 장 중에 갑자기 뜸).
        /// </summary>
        public List<NewsItem> GenerateDay(int day, IReadOnlyList<StockState> stocks, int ticksPerDay)
        {
            usedToday.Clear();
            var order = stocks.OrderBy(_ => rng.Next()).ToList();
            var news = new List<NewsItem>();
            int idx = 0;
            for (; idx < order.Count && news.Count(n => n.Kind == NewsKind.Normal) < settings.newsPerDay; idx++)
            {
                var item = Create(order[idx], NewsKind.Normal, ticksPerDay);
                if (item == null) continue;
                if (rng.NextDouble() >= settings.followUpChance)
                {
                    item.HasFollowUp = false; // 오늘은 후속 없음: 평소처럼 움직이고 특보·결과 기사 없음
                    item.RevealTick = int.MaxValue;
                }
                news.Add(item);
            }
            int breaking = rng.Next(Mathf.Max(0, settings.breakingMin), Mathf.Max(settings.breakingMin, settings.breakingMax) + 1);
            for (int made = 0; idx < order.Count && made < breaking; idx++)
            {
                var item = Create(order[idx], NewsKind.Breaking, ticksPerDay);
                if (item == null) continue;
                news.Add(item);
                made++;
            }
            return news;
        }

        static readonly string[] UpButDown =
        {
            "이미 주가에 다 반영돼 있었다는 평가에 차익 실현 매물이 쏟아졌어요",
            "기대가 너무 컸던 탓에 '생각보다 별로'라는 실망 매물이 나왔어요",
            "대주주가 이 소식에 맞춰 보유 주식을 대량 매도했어요",
        };
        static readonly string[] DownButUp =
        {
            "악재가 이미 주가에 반영돼 '바닥'이라는 저가 매수세가 몰렸어요",
            "공매도 세력이 한꺼번에 되사들이며(숏커버링) 주가가 튀었어요",
            "회사가 즉시 대책과 자사주 매입을 발표해 오히려 신뢰가 올라갔어요",
        };

        /// <summary>오늘 가격 경로를 정한다. 장 전(PreMarket)에만 부른다.</summary>
        public void ApplyToPrices(IReadOnlyList<StockState> stocks, IReadOnlyList<NewsItem> news, MarketSettings market)
        {
            foreach (var stock in stocks)
            {
                var item = news.FirstOrDefault(n => n.Stock == stock);
                stock.PrepareDay(item != null
                    && item.HasFollowUp
                    ? PricePathGenerator.GenerateWithReveal(stock.PrevClose, stock.DailyLimit, item.MoveTick, item.Effect, market, rng, item.SettleTick, item.Steps, item.Overshoot, (int)item.Shape)
                    : PricePathGenerator.Generate(stock.PrevClose, stock.DailyLimit, market, rng));
            }
        }

        /// <summary>뉴스 없던 종목 예지: 템플릿 하나와 결과를 뽑고 그 종목의 오늘 경로를 그 값으로 다시 만든다. 템플릿이 없으면 null.</summary>
        public NewsItem CreateForesight(StockState stock, MarketSettings market)
        {
            var item = Create(stock, NewsKind.Foresight, market.ticksPerDay);
            if (item == null) return null;
            item.Foreseen = true;
            stock.PrepareDay(PricePathGenerator.GenerateWithReveal(stock.PrevClose, stock.DailyLimit, item.MoveTick, item.Effect, market, rng, item.SettleTick, item.Steps, item.Overshoot, (int)item.Shape));
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

            // 움직이는 모양 (읽히지 않게 섞음): 넘겼다 돌아옴 25% · 넘기지 않고 다가감 35% · 막판 몰아치기 20% ·
            // 마감까지 한 방향 20%. 모두 중간 목표 5~8개를 거치며 되돌림이 섞이고, 결과 값에 정확히 닿는다.
            // 길이는 장 길이의 25~60% (마감까지 한 방향은 장 마감까지). 남은 시간이 아주 짧으면 한 번에.
            int steps = 1, settleTick = moveTick;
            var shape = MoveShape.Jump;
            float overshoot = 1f;
            int room = ticksPerDay - moveTick;
            if (room >= 6)
            {
                double r = rng.NextDouble();
                shape = r < 0.25 ? MoveShape.Overshoot : r < 0.60 ? MoveShape.Approach : r < 0.80 ? MoveShape.LateSurge : MoveShape.Trend;
                int span = Mathf.Max(6, Mathf.RoundToInt(ticksPerDay * (0.25f + (float)rng.NextDouble() * 0.35f)));
                settleTick = shape == MoveShape.Trend ? ticksPerDay : moveTick + Mathf.Min(span, room);
                int len = settleTick - moveTick;
                steps = Mathf.Clamp(rng.Next(5, 9), 3, Mathf.Max(3, len / 3 + 1)); // 중간 목표 수 5~8개 (구간마다 3틱 이상)
                if (shape == MoveShape.Overshoot)
                    overshoot = settings.overshootMin + (float)rng.NextDouble() * Mathf.Max(0f, settings.overshootMax - settings.overshootMin);
            }
            var created = new NewsItem
            {
                Stock = stock,
                Kind = kind,
                Template = template,
                Success = success,
                RevealAtClose = atClose,
                RevealTick = revealTick,
                MoveTick = moveTick,
                Overshoot = overshoot,
                Shape = shape,
                SettleTick = settleTick,
                Steps = steps,
                Effect = TargetChange(stock, template, success),
            };
            // 반전 결과: 기사와 반대로 (폭은 reversalScale배), 근거 문구를 붙인다
            if (rng.NextDouble() < settings.reversalChance)
            {
                created.Reversal = true;
                created.Effect = Mathf.Max(-0.95f, -created.Effect * settings.reversalScale);
                var reasons = success ? UpButDown : DownButUp;
                created.ReversalReason = reasons[rng.Next(reasons.Length)];
            }
            return created;
        }
    }
}
