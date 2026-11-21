using UnityEngine;

namespace BurningAnt.Character
{
    public enum Mood { Crash, Loss, Flat, Gain, Jackpot }

    /// <summary>
    /// 주인공 반신 캠 기준값. 옷 = 순자산(현금 + 주식 − 빚) 5단계 (기획서 7장),
    /// 표정 = 그날 수익률 5종 (경계값은 기획서에 없어 여기서 정함).
    /// </summary>
    [CreateAssetMenu(menuName = "BurningAnt/Character Settings", fileName = "CharacterSettings")]
    public class CharacterSettings : ScriptableObject
    {
        [Header("옷 (순자산 경계, 오름차순 4개)")]
        public float[] outfitThresholds = { -5000f, 0f, 5000f, 20000f };
        public string[] outfitNames = { "파산 직전 · 늘어난 티셔츠", "궁핍 · 낡은 후드티", "평범 · 깔끔한 셔츠", "여유 · 정장", "부자 · 명품 정장과 시계" };

        [Header("반신 캠 그림 (Art/Character)")]
        [Tooltip("표정 5종: 폭락, 손해, 보합, 이득, 대박 순서")] public Texture2D[] moodImages = new Texture2D[5];
        [Tooltip("장 전(뉴스·아이템 시간)에 보여줄 그림. 비우면 표정 그림")] public Texture2D prepImage;
        [Tooltip("빚을 다 갚았을 때")] public Texture2D winImage;

        [Header("표정 (그날 수익률 경계)")]
        [Tooltip("이 수익률 이하면 폭락")] public float crashBelow = -0.05f;
        [Tooltip("이 수익률 미만이면 손해")] public float lossBelow = -0.01f;
        [Tooltip("이 수익률 초과면 이득")] public float gainAbove = 0.01f;
        [Tooltip("이 수익률 이상이면 대박")] public float jackpotAbove = 0.05f;

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

        public Mood GetMood(float todayReturn)
        {
            if (todayReturn <= crashBelow) return Mood.Crash;
            if (todayReturn < lossBelow) return Mood.Loss;
            if (todayReturn >= jackpotAbove) return Mood.Jackpot;
            if (todayReturn > gainAbove) return Mood.Gain;
            return Mood.Flat;
        }

        public Texture2D MoodImage(Mood mood)
        {
            int i = (int)mood;
            return moodImages != null && i < moodImages.Length ? moodImages[i] : null;
        }

        public static string MoodName(Mood mood) => mood switch
        {
            Mood.Crash => "폭락",
            Mood.Loss => "손해",
            Mood.Gain => "이득",
            Mood.Jackpot => "대박",
            _ => "보합",
        };

        /// <summary>그림이 비어 있을 때 대신 보여줄 글자 얼굴.</summary>
        public static string MoodFace(Mood mood) => mood switch
        {
            Mood.Crash => "(T_T)",
            Mood.Loss => "(-_-;)",
            Mood.Gain => "(^_^)",
            Mood.Jackpot => "(*^o^*)",
            _ => "(-_-)",
        };
    }
}
