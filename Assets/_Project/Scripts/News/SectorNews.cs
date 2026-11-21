using System;
using System.Collections.Generic;
using BurningAnt.Market;
using UnityEngine;

namespace BurningAnt.News
{
    /// <summary>기사 템플릿 하나: 최초 기사 → 성공 기사(+%) 또는 실패 기사(−%). 기사에는 종목 이름이 없고 앞에 붙는다.</summary>
    [Serializable]
    public class NewsTemplate
    {
        public string initialHeadline;
        public string successHeadline;
        [Tooltip("성공 시 % (예: 25 = +25%)")] public float successPercent;
        public string failureHeadline;
        [Tooltip("실패 시 % (예: -23 = −23%)")] public float failurePercent;
    }

    /// <summary>업종 하나의 기사 템플릿 목록 (팀 문서 "업종 및 기사 정리"). 같은 업종 종목 4개가 함께 쓴다. Data/News에 업종마다 하나.</summary>
    [CreateAssetMenu(menuName = "BurningAnt/Sector News", fileName = "News")]
    public class SectorNews : ScriptableObject
    {
        public Sector sector;
        public List<NewsTemplate> templates = new();
    }
}
