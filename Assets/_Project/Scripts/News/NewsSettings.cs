using System.Collections.Generic;
using UnityEngine;

namespace BurningAnt.News
{
    /// <summary>뉴스 규칙 (기획서 6장). 기사 문구는 업종별 SectorNews 에셋에 있다.</summary>
    [CreateAssetMenu(menuName = "BurningAnt/News Settings", fileName = "NewsSettings")]
    public class NewsSettings : ScriptableObject
    {
        [Header("하루 뉴스")]
        [Tooltip("매일 무작위로 고른 이 수만큼의 종목에 뉴스가 뜬다 (확정: 4)")] public int newsPerDay = 4;
        [Tooltip("업종별 기사 템플릿 (Data/News)")] public List<SectorNews> library = new();

        // shortcut: 아래 값은 팀 결정 전 제안값.
        [Header("결과 (제안값)")]
        [Range(0f, 1f), Tooltip("템플릿이 성공으로 끝날 확률")] public float successChance = 0.5f;
        [Tooltip("켜면 결과 = 기본 구간 끝 + 템플릿 % (떡상·떡락이 항상 기본 구간보다 큼). 끄면 템플릿 % 그대로")]
        public bool addBaseRange = true;

        [Header("뉴스 변동 폭 (사용자 확정: 기본 구간보다 50~200% 큼)")]
        [Tooltip("켜면 결과 = 기본 구간 × (1 + 템플릿%/기본 구간을 extraMin~extraMax로 자른 값). addBaseRange보다 우선")]
        public bool useExtraRange = true;
        [Tooltip("기본 구간보다 최소 몇 배 더 (0.5 = 50% 더)")] public float extraMin = 0.5f;
        [Tooltip("기본 구간보다 최대 몇 배 더 (2.0 = 200% 더)")] public float extraMax = 2f;

        [Header("특보 구성 (제안값)")]
        [Range(0f, 1f), Tooltip("아침 뉴스 중 장 중에 후속 기사(특보)가 뜰 확률. 안 뜨면 그 종목은 평소처럼 움직이고 결과창에도 후속이 없다")]
        public float followUpChance = 0.6f;
        [Tooltip("아침 뉴스에 없던 종목에 장 중 돌발 특보가 뜨는 수 (최소~최대)")] public int breakingMin = 1;
        [Tooltip("아침 뉴스에 없던 종목에 장 중 돌발 특보가 뜨는 수 (최소~최대)")] public int breakingMax = 2;
        [Range(0f, 1f), Tooltip("반전 결과 확률: 호재 기사인데 내리거나 악재 기사인데 오름 (근거 문구가 붙음)")] public float reversalChance = 0.12f;
        [Tooltip("반전 결과의 변동 폭 (원래 폭 대비)")] public float reversalScale = 0.6f;
        [Tooltip("굴곡 경로가 중간에 가는 최고점 (최종 변동 대비, 1.2 = 120%)")] public float overshootMin = 1.2f;
        [Tooltip("굴곡 경로가 중간에 가는 최고점 (최종 변동 대비, 1.6 = 160%)")] public float overshootMax = 1.6f;

        [Header("결과가 뜨는 시점 (사용자 확정: 장 전체 0~100% 구간에서 균등)")]
        [Range(0f, 1f), Tooltip("(지금은 안 씀) 예전 '장 중 특보 확률'. 시점은 아래 구간에서 균등하게 뽑는다")] public float intradayRevealChance = 1f;
        [Range(0f, 1f), Tooltip("결과가 뜰 수 있는 가장 이른 시점 (장 길이 대비)")] public float earliestReveal = 0f;
        [Range(0f, 1f), Tooltip("결과가 뜰 수 있는 가장 늦은 시점 (장 길이 대비, 1 = 장 마감)")] public float latestReveal = 1f;
    }
}
