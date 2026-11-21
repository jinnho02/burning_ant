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

        public static string Money(float value) => "$" + value.ToString("N2", Culture);

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
