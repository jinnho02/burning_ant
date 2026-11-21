using System.Collections.Generic;
using BurningAnt.Items;
using BurningAnt.Market;
using BurningAnt.News;

namespace BurningAnt.Core
{
    /// <summary>
    /// 하루 동안 일어난 일을 모아 둔 기록. 2단계 "오늘의 흐름" 표시와 4단계 결과창이 이 데이터를 읽는다.
    /// GameLoop가 채우고, UI는 읽기만 한다.
    /// </summary>
    public class DailyReport
    {
        public class StockLine
        {
            public StockState Stock;
            public float StartValue;   // 뉴스 단계(전일 종가 기준) 평가금
            public float EndValue;     // 장 마감 직후(마이너스 정산 전) 평가금
            public float NetCash;      // 매도로 받은 돈 − 매수에 쓴 돈 (수수료 포함)
            public int Buys, Sells, BoughtShares, SoldShares;
            public float Fees;
            public float ProfitLoss => EndValue - StartValue + NetCash;
            public bool Active => Buys > 0 || Sells > 0 || StartValue != 0f || EndValue != 0f;
        }

        public class ItemUse
        {
            public ItemDefinition Item;
            public GamePhase Phase;        // 쓴 단계 (Prep = 장 전, Market = 장 중)
            public StockState Target;      // 대상 종목 (없으면 null)
            public string Text;            // 화면에 보인 결과 문장
            public bool? HintCorrect;      // 방향·연쇄 힌트가 맞았는지 (힌트가 아니면 null)
            public bool HintRevealedTomorrow; // 연쇄 예측처럼 다음 날에야 맞았는지 알 수 있는 힌트
        }

        public int Day;
        public float StartAssets;
        public float EndAssets;
        public readonly Dictionary<StockState, StockLine> Stocks = new();
        public readonly List<ItemUse> ItemUses = new();
        public readonly List<NewsItem> News = new();

        public bool NewsChecked;
        public int ReservationsPlaced;     // 장 시작 직전에 걸려 있던 예약 수
        public int ReservationsFilled;     // 시가에 체결된 예약 수
        public int AutoSellsFired;         // 손절·익절 발동 횟수

        public float Interest;             // 오늘 붙은 이자
        public float SettlementCharge;     // 마이너스 정산으로 잔고에서 빠진 돈
        public readonly List<StockState> ClosedStocks = new();
        public float ExtraLoan;            // 오늘 받은 추가 대출
        public float EmergencyLoan;        // 오늘 받은 긴급 대출
        public float Repaid;               // 오늘 갚은 돈
        public bool Closed;                // 장이 끝나 결과가 확정됐는지

        public float ProfitLoss => EndAssets - StartAssets;
        public float ReturnRate => StartAssets > 0f ? ProfitLoss / StartAssets : 0f;

        public float TotalFees
        {
            get
            {
                float sum = 0f;
                foreach (var line in Stocks.Values) sum += line.Fees;
                return sum;
            }
        }

        public StockLine Line(StockState stock)
        {
            if (!Stocks.TryGetValue(stock, out var line))
            {
                line = new StockLine { Stock = stock };
                Stocks[stock] = line;
            }
            return line;
        }
    }
}
