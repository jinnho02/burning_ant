using System.Globalization;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>화면에 찍는 숫자 형식과 상승·하락 색 클래스를 한곳에서 정한다.</summary>
    public static class Format
    {
        public const string UpClass = "price-up";
        public const string DownClass = "price-down";

        static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>원 단위. 1억 이상은 "1억 2,345만원"처럼 만 원 아래를 줄인다 (float라 억 단위에서 몇 원 오차가 있어 어차피 못 믿음).</summary>
        public static string Money(float value)
        {
            double a = System.Math.Abs((double)value);
            string s;
            if (a >= 1e8)
            {
                long eok = (long)(a / 1e8), man = (long)((a - eok * 1e8) / 1e4);
                s = man > 0 ? eok.ToString("N0", Culture) + "억 " + man.ToString("N0", Culture) + "만원" : eok.ToString("N0", Culture) + "억원";
            }
            else s = System.Math.Round(a).ToString("N0", Culture) + "원";
            return value < 0f && s != "0원" ? "-" + s : s;
        }

        public static string SignedMoney(float value) => (value >= 0f ? "+" : "-") + Money(System.Math.Abs(value));

        /// <summary>0.0123 → "+1.23%"</summary>
        public static string SignedPercent(float ratio) => (ratio >= 0f ? "+" : "") + (ratio * 100f).ToString("F2", Culture) + "%";

        /// <summary>값이 양수면 상승색, 음수면 하락색, 0이면 기본색.</summary>
        public static void SetTrendClass(VisualElement element, float value)
        {
            element.EnableInClassList(UpClass, value > 0f);
            element.EnableInClassList(DownClass, value < 0f);
        }
    }
}
