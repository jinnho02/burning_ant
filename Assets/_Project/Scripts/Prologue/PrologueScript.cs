using System;
using System.Collections.Generic;
using UnityEngine;

namespace BurningAnt.Prologue
{
    /// <summary>장면마다 화면 가운데에 띄우는 소품 (그림 대신 UI 카드로 그린다).</summary>
    public enum PrologueProp
    {
        None,
        LoanApp,  // 대출 앱: 희망 금액 입력
        Approved, // 대출 앱 + "대출 승인" 도장
        Dream,    // 일주일이면 두 배 망상
        Account,  // 계좌: 현금 / 빚 / 순자산 $0
        Login,    // 주식 앱 로그인 → 게임 시작
    }

    [Serializable]
    public class PrologueLine
    {
        [Tooltip("말하는 사람. 비우면 내레이션, {hero}는 주인공 이름")] public string speaker = "{hero}";
        [TextArea(2, 4), Tooltip("{hero} {debt} {rate} {period} 를 쓸 수 있음")] public string text;
        [Tooltip("오른쪽 아래 주인공 그림. 비우면 이전 그림 유지")] public Texture2D portrait;
        public PrologueProp prop;
        [Tooltip("대사 아래 작은 글씨 (선택)")] public string footnote;
    }

    /// <summary>프롤로그 대사와 연출. 대사는 이 에셋에서 고친다 (Data/PrologueScript.asset).</summary>
    [CreateAssetMenu(menuName = "BurningAnt/Prologue Script", fileName = "PrologueScript")]
    public class PrologueScript : ScriptableObject
    {
        [Tooltip("주인공 이름 (미정이면 그대로)")] public string heroName = "주인공";
        [Tooltip("숫자({debt} {rate} {period})를 가져올 대출 설정")] public Loan.LoanSettings loanSettings;
        [Tooltip("프롤로그가 끝나면 열 씬")] public string nextScene = "DayLoop";
        public List<PrologueLine> lines = new();

        /// <summary>{hero} {debt} {rate} {period} 를 실제 값으로 바꾼다.</summary>
        public string Fill(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("{hero}", heroName);
            if (loanSettings == null) return text;
            return text
                .Replace("{debt}", "$" + loanSettings.startingDebt.ToString("N0", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{rate}", (loanSettings.baseInterestRate * 100f).ToString("0") + "%")
                .Replace("{period}", loanSettings.interestPeriodDays + "일");
        }
    }
}
