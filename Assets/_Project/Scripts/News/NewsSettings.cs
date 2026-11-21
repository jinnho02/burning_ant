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

        [Header("결과가 뜨는 시점 (제안값)")]
        [Range(0f, 1f), Tooltip("결과가 장 중(특보)에 뜰 확률. 나머지는 장 마감에 뜸")] public float intradayRevealChance = 0.5f;
        [Range(0f, 1f), Tooltip("특보가 뜰 수 있는 가장 이른 시점 (장 길이 대비)")] public float earliestReveal = 0.15f;
        [Range(0f, 1f), Tooltip("특보가 뜰 수 있는 가장 늦은 시점 (장 길이 대비)")] public float latestReveal = 0.85f;
    }
}
