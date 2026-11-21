using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// 하루 가격 라인 차트. 세로 범위는 전일 종가 ± 하루 등락 한도로 고정 (위 끝 = 상한가, 아래 끝 = 하한가).
    /// 색은 USS의 --chart-* 속성으로 바꾼다.
    /// </summary>
    [UxmlElement]
    public partial class PriceChartElement : VisualElement
    {
        static readonly CustomStyleProperty<Color> UpColorProperty = new("--chart-up-color");
        static readonly CustomStyleProperty<Color> DownColorProperty = new("--chart-down-color");
        static readonly CustomStyleProperty<Color> BaseColorProperty = new("--chart-base-color");
        static readonly CustomStyleProperty<Color> AverageColorProperty = new("--chart-average-color");

        const float Padding = 8f;

        Color upColor = new(0.886f, 0.294f, 0.290f);
        Color downColor = new(0.216f, 0.541f, 0.867f);
        Color baseColor = new(0.7f, 0.7f, 0.68f);
        Color averageColor = new(0.729f, 0.459f, 0.090f);

        float[] path;
        int visibleCount;
        int totalTicks = 1;
        float prevClose = 1f;
        float limit = 0.1f;
        float? averagePrice;

        public PriceChartElement()
        {
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyle);
        }

        /// <param name="visibleCount">그릴 점 개수. 0이면 가격선 없이 기준선만.</param>
        /// <param name="averagePrice">보유 중이면 평단, 아니면 null.</param>
        public void SetData(float prevClose, float limit, float[] path, int visibleCount, int totalTicks, float? averagePrice)
        {
            this.prevClose = prevClose;
            this.limit = limit;
            this.path = path;
            this.visibleCount = visibleCount;
            this.totalTicks = Mathf.Max(1, totalTicks);
            this.averagePrice = averagePrice;
            MarkDirtyRepaint();
        }

        void OnCustomStyle(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(UpColorProperty, out var c)) upColor = c;
            if (evt.customStyle.TryGetValue(DownColorProperty, out c)) downColor = c;
            if (evt.customStyle.TryGetValue(BaseColorProperty, out c)) baseColor = c;
            if (evt.customStyle.TryGetValue(AverageColorProperty, out c)) averageColor = c;
            MarkDirtyRepaint();
        }

        float Y(float price, float height)
        {
            float t = (price / prevClose - 1f) / limit; // -1(하한) ~ +1(상한)
            return height * 0.5f - t * (height * 0.5f - Padding);
        }

        void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var p = ctx.painter2D;
            float w = rect.width, h = rect.height;

            HorizontalLine(p, Y(prevClose * (1f + limit), h), w, WithAlpha(upColor, 0.5f));
            HorizontalLine(p, Y(prevClose * (1f - limit), h), w, WithAlpha(downColor, 0.5f));
            HorizontalLine(p, Y(prevClose, h), w, baseColor);
            if (averagePrice.HasValue) HorizontalLine(p, Y(averagePrice.Value, h), w, averageColor);

            if (path == null || visibleCount < 1) return;

            int count = Mathf.Min(visibleCount, path.Length);
            float last = path[count - 1];
            p.strokeColor = last >= prevClose ? upColor : downColor;
            p.lineWidth = 2.5f;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            for (int i = 0; i < count; i++)
            {
                var point = new Vector2(i * w / totalTicks, Y(path[i], h));
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.Stroke();
        }

        static void HorizontalLine(Painter2D p, float y, float width, Color color)
        {
            p.strokeColor = color;
            p.lineWidth = 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, y));
            p.LineTo(new Vector2(width, y));
            p.Stroke();
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);
    }
}
