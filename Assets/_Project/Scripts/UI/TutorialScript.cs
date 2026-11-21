using System;
using System.Collections.Generic;
using BurningAnt.Core;
using UnityEngine;

namespace BurningAnt.UI
{
    /// <summary>
    /// 튜토리얼 단계 목록. 1일차에 각 단계(뉴스·아이템·장·결과·상점)에 처음 들어갈 때 그 단계의 항목이 차례로 뜬다.
    /// target은 강조할 화면 요소의 name (UXML의 name). 비우거나 못 찾으면 화면 가운데에 설명만 뜬다.
    /// 문구는 아직 임시 — Data/에 에셋을 만들어 고치거나, 에셋이 없으면 CreateDefault()의 임시 문구를 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "BurningAnt/Tutorial Script", fileName = "TutorialScript")]
    public class TutorialScript : ScriptableObject
    {
        [Serializable]
        public class Step
        {
            public GamePhase phase;
            [Tooltip("강조할 요소의 name. 비우면 가운데에 설명만")] public string target;
            public string title;
            [TextArea(2, 5)] public string text;
        }

        public List<Step> steps = new();

        public static TutorialScript CreateDefault()
        {
            var s = CreateInstance<TutorialScript>();
            void Add(GamePhase phase, string target, string title, string text) =>
                s.steps.Add(new Step { phase = phase, target = target, title = title, text = text });

            // shortcut: 임시 문구. 기획 확정 후 Data/TutorialScript 에셋으로 교체.
            Add(GamePhase.News, "news-paper-list", "(임시) 아침 뉴스", "매일 아침 4종목에 뉴스가 떠요. 그 종목은 오늘 크게 움직이지만, 오를지 내릴지는 결과 기사(장 중 특보 또는 장 마감)가 나와야 알 수 있어요.");
            Add(GamePhase.News, "news-paper-button", "(임시) 다음 단계로", "다 읽었으면 눌러서 아이템·예약 단계로 넘어가요.");
            Add(GamePhase.Prep, "flow-card", "(임시) 오늘의 흐름", "오늘 한 일(뉴스, 예약, 쓴 아이템)과 아직 안 쓴 아이템이 여기에 정리돼요.");
            Add(GamePhase.Prep, "item-bar-list", "(임시) 아이템", "상점에서 산 아이템이에요. '장 전' 아이템은 지금, '장 중' 아이템은 장이 열린 뒤에 써요.");
            Add(GamePhase.Prep, "start-button", "(임시) 예약하고 장 시작", "매수·매도 예약은 장이 열릴 때 시가로 체결돼요. 준비가 끝나면 장을 시작해요.");
            Add(GamePhase.Market, "chart", "(임시) 60초 장", "가격이 실시간으로 움직여요. 설명이 떠 있는 동안은 시간이 멈춰 있어요.");
            Add(GamePhase.Market, "buy-button", "(임시) 매수·매도", "Q 매수, W 매도. 손익은 넣은 돈의 3배로 움직여요.");
            Add(GamePhase.Settlement, "settlement-report", "(임시) 하루 결과", "오늘 손익을 종목·아이템·뉴스·대출별로 보여 줘요.");
            Add(GamePhase.Settlement, "repay-button", "(임시) 빚 갚기", "원하는 만큼 갚을 수 있어요. 빚을 다 갚으면 승리!");
            Add(GamePhase.Shop, "shop-grid", "(임시) 상점", "매일 8개가 무작위로 나와요. 마우스를 올리면 설명, 클릭하면 구매.");
            return s;
        }
    }
}
