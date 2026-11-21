using UnityEngine;

namespace BurningAnt.Character
{
    /// <summary>표정 7단계: 그날 수익률 -30% 이하 · -20% · -10% · 보합 · +10% · +20% · +30% 이상.</summary>
    public enum Mood { Crash, Bad, Loss, Flat, Gain, Great, Jackpot }

    /// <summary>
    /// 주인공 반신 캠 기준값. 표정 = 그날 수익률 7단계 (경계 ±10·20·30%로 고정).
    /// 옷(순자산 단계)은 화면에 더 이상 글자로 띄우지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "BurningAnt/Character Settings", fileName = "CharacterSettings")]
    public class CharacterSettings : ScriptableObject
    {
        [Header("옷 (순자산 경계, 오름차순 4개)")]
        public float[] outfitThresholds = { -50_000_000f, 0f, 50_000_000f, 200_000_000f };
        public string[] outfitNames = { "파산 직전 · 늘어난 티셔츠", "궁핍 · 낡은 후드티", "평범 · 깔끔한 셔츠", "여유 · 정장", "부자 · 명품 정장과 시계" };

        [Header("반신 캠 그림 (Art/Character)")]
        [Tooltip("표정 7종: -30%, -20%, -10%, 보합, +10%, +20%, +30% 순서")] public Texture2D[] moodImages = new Texture2D[7];
        [Tooltip("장 전(뉴스·아이템 시간)에 보여줄 그림. 비우면 표정 그림")] public Texture2D prepImage;
        [Tooltip("빚을 다 갚았을 때")] public Texture2D winImage;

        [Header("말풍선 대사 (지금은 화면에 안 띄움)")]
        [Tooltip("표정 7종: -30%, -20%, -10%, 보합, +10%, +20%, +30% 순서")]
        public string[] moodLines = { "무리무리무리!!", "제발… 제발…", "으으… 무리무리…", "아직 몰라, 버텨!", "가즈아~!", "이거 되는데?!", "무리… 아니었어?!" };
        [Tooltip("장 전(뉴스·아이템 시간) 대사")] public string prepLine = "오늘은 뭘 살까~?";

        // 표정이 너무 자주 바뀌지 않게 경계를 ±10·20·30%로 고정 (팀 결정)
        static readonly float[] MoodSteps = { 0.10f, 0.20f, 0.30f };

        /// <summary>0(파산 직전) ~ 4(부자)</summary>
        public int OutfitStage(float netWorth)
        {
            int stage = 0;
            foreach (float t in outfitThresholds)
                if (netWorth >= t) stage++;
            return stage;
        }

        public string OutfitName(float netWorth)
        {
            int stage = OutfitStage(netWorth);
            return stage < outfitNames.Length ? outfitNames[stage] : "";
        }

        /// <summary>+10% 이상 이득, +20% 이상 크게 이득, +30% 이상 대박. 손해 쪽도 같은 경계 (-10%, -20%, -30% 이하).</summary>
        public Mood GetMood(float todayReturn)
        {
            const float eps = 1e-5f; // 딱 10%도 그 단계로
            int level = 0;
            foreach (float step in MoodSteps)
                if (Mathf.Abs(todayReturn) >= step - eps) level++;
            return (Mood)((int)Mood.Flat + (todayReturn >= 0f ? level : -level));
        }

        public Texture2D MoodImage(Mood mood)
        {
            int i = (int)mood;
            return moodImages != null && i < moodImages.Length ? moodImages[i] : null;
        }

        public string MoodLine(Mood mood)
        {
            int i = (int)mood;
            return moodLines != null && i < moodLines.Length ? moodLines[i] : "";
        }

        public static string MoodName(Mood mood) => mood switch
        {
            Mood.Crash => "폭락",
            Mood.Bad => "큰 손해",
            Mood.Loss => "손해",
            Mood.Gain => "이득",
            Mood.Great => "큰 이득",
            Mood.Jackpot => "대박",
            _ => "보합",
        };

        /// <summary>그림이 비어 있을 때 대신 보여줄 글자 얼굴.</summary>
        public static string MoodFace(Mood mood) => mood switch
        {
            Mood.Crash => "(T_T)",
            Mood.Bad => "(;_;)",
            Mood.Loss => "(-_-;)",
            Mood.Gain => "(^_^)",
            Mood.Great => "(^o^)",
            Mood.Jackpot => "(*^o^*)",
            _ => "(-_-)",
        };
    }
}
