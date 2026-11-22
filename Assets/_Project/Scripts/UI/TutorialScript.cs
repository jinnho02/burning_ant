using System;
using System.Collections.Generic;
using BurningAnt.Core;
using UnityEngine;

namespace BurningAnt.UI
{
    /// <summary>
    /// 튜토리얼 단계 목록. 1일차에 각 단계(뉴스·아이템·장·결과·상점)에 처음 들어갈 때 그 단계의 항목이 차례로 뜬다.
    /// target은 강조할 화면 요소의 name (UXML의 name). 비우거나 못 찾으면 화면 가운데에 설명만 뜬다.
    /// 문구를 바꾸려면 Data/에 에셋을 만들어 고치거나, 에셋이 없으면 CreateDefault()의 기본 문구를 쓴다.
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

            // 1단계 · 뉴스
            Add(GamePhase.News, "news-paper-list", "아침 뉴스", "매일 아침 무작위 4종목에 뉴스가 떠요. 뉴스가 뜬 종목은 평소보다 훨씬 크게 움직여요.\n후속 기사는 장 중 특보로 뜨는데, 모든 뉴스에 후속이 나오지는 않아요. 아침 뉴스에 없던 종목에 돌발 특보가 뜨기도 해요.");
            Add(GamePhase.News, "news-paper-button", "다음 단계로", "다 읽었으면 눌러서 2단계(아이템·예약)로 넘어가요.");
            // 2단계 · 아이템 · 예약
            Add(GamePhase.Prep, "left-column", "업종과 종목", "업종을 누르면 그 업종 종목이 펼쳐져요. ● 뉴스는 오늘 뉴스가 뜬 업종, ● 보유는 내가 가진 종목이 있는 업종이에요.");
            Add(GamePhase.Prep, "item-bar-list", "아이템", "상점에서 산 아이템이에요. 모두 한 번 쓰면 사라져요.\n'장 전'은 지금, '장 중'은 장이 열린 뒤에, '장 전·장 중'은 둘 다 쓸 수 있어요.\n정보 아이템은 틀릴 수도 있어요 (적중률이 붙어 있어요).");
            Add(GamePhase.Prep, "loan-button", "추가 대출", "아이템 시간(2단계)이나 장 시간(3단계)에 한 판에 2번, 1번에 2억씩 빌릴 수 있어요.\n대신 하루 이자 30%가 다다음 날부터 붙어요.");
            Add(GamePhase.Prep, "buy-button", "예약 주문", "지금 거는 매수·매도는 장이 열리는 순간 시가로 체결돼요 (시가는 장이 시작돼야 보여요).\n단, 시가가 전날 종가보다 크게 불리하게 열리면(매수는 비싸게, 매도는 싸게) 그 예약은 취소돼요.\n손익은 넣은 돈의 3배로 움직이고, 수수료는 없어요.");
            Add(GamePhase.Prep, "start-button", "장 시작", "준비가 끝나면 장을 시작해요. 장은 60초 동안 열리고 멈출 수 없어요.");
            // 3단계 · 장
            Add(GamePhase.Market, "chart", "60초 장", "가격이 실시간으로 움직여요. 뉴스 결과가 나오면 오른쪽 위 '오늘의 뉴스' 맨 위에 특보가 뜨고, 그 순간부터 그 종목이 크게 움직여요. 호재인지 악재인지는 기사를 읽고 판단하세요.\n(이 설명이 떠 있는 동안은 시간이 멈춰 있어요.)");
            Add(GamePhase.Market, "buy-button", "매수 · 매도", "Q 매수, W 매도. 손익은 3배라서 많이 떨어지면 넣은 돈보다 더 잃을 수 있어요.\n장 마감 때 마이너스인 종목은 강제로 정리되고, 그 마이너스만큼 현금에서 빠져요.");
            Add(GamePhase.Market, "interest-label", "이자율", "여기 숫자는 오늘 이자가 붙는 대출들의 이자율이에요. 대출마다 따로 붙어요.\n· 기본 대출: 3일차부터 매일 5%\n· 추가 대출: 30% (빌린 다다음 날부터)\n· 러시앤러시·사나머니: 20% · 햇살론: 30% (다음 날부터)\n이자는 4단계 결과에서 내요.");
            // 4단계 · 결과
            Add(GamePhase.Settlement, "settlement-report", "하루 결과", "오늘 매매로 번 돈, 돈의 흐름, 내 종목, 시장 전체, 쓴 아이템, 뉴스 결과, 빚을 정리해서 보여줘요.");
            Add(GamePhase.Settlement, "repay-button", "빚 갚기 · 이자 갚기", "빚 갚기로 원하는 만큼 갚아요 (오늘 이자 → 이자 높은 대출 순으로 갚아져요).\n3일차부터는 왼쪽 '이자 갚기'로 그날 이자를 내요. 안 내면 상점으로 갈 때 빚에 붙어요.\n빚 0원 + 자산 10억이면 성공!");
            Add(GamePhase.Settlement, "", "파산과 햇살론", "장 마감 뒤 순자산(현금 + 주식 - 빚)이 -5억 밑이면 파산이에요.\n현금이 마이너스면 햇살론(1번에 최대 2억, 한 판에 2번, 하루 이자 30%)을 받을 수 있어요. 빚도 같이 늘어서 순자산은 그대로예요.");
            // 5단계 · 상점
            Add(GamePhase.Shop, "shop-grid", "상점", "매일 아이템 8개가 무작위로 나와요. 마우스를 올리면 설명과 가격, 클릭하면 구매예요.");
            Add(GamePhase.Shop, "next-day-button", "다음 날", "다 샀으면 눌러서 다음 날 아침 뉴스로 넘어가요.");
            return s;
        }
    }
}
