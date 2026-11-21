using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// 상점 가방 바닥에 눕혀 놓은 듯한 사다리꼴 칸. 네 꼭짓점은 이 요소 사각형 안의 비율(0~1)로 받고,
    /// 칸 모양 그대로 그리고, 마우스 올림·클릭도 사다리꼴 안에서만 받는다.
    /// </summary>
    public class ShopSlotElement : VisualElement
    {
        // 위왼쪽, 위오른쪽, 아래오른쪽, 아래왼쪽 (이 요소 안 비율)
        readonly Vector2[] corners = new Vector2[4];
        bool hovered;

        public Color FillColor = new(0.06f, 0.016f, 0.03f, 0.45f);
        public Color HoverFillColor = new(0.95f, 0.76f, 0.31f, 0.18f);
        public Color BorderColor = new(0.95f, 0.76f, 0.31f, 0.55f);
        public Color HoverBorderColor = new(0.95f, 0.76f, 0.31f, 1f);
        public float BorderWidth = 2f;

        public ShopSlotElement()
        {
            generateVisualContent += Draw;
            RegisterCallback<PointerEnterEvent>(_ => { hovered = true; MarkDirtyRepaint(); });
            RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; MarkDirtyRepaint(); });
        }

        public void SetCorners(Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft)
        {
            corners[0] = topLeft;
            corners[1] = topRight;
            corners[2] = bottomRight;
            corners[3] = bottomLeft;
            MarkDirtyRepaint();
        }

        Vector2 Local(int i) => new(corners[i].x * layout.width, corners[i].y * layout.height);

        void Draw(MeshGenerationContext ctx)
        {
            if (layout.width <= 0f || layout.height <= 0f) return;
            var p = ctx.painter2D;
            p.BeginPath();
            p.MoveTo(Local(0));
            for (int i = 1; i < 4; i++) p.LineTo(Local(i));
            p.ClosePath();
            p.fillColor = hovered ? HoverFillColor : FillColor;
            p.Fill();
            p.strokeColor = hovered ? HoverBorderColor : BorderColor;
            p.lineWidth = BorderWidth;
            p.lineJoin = LineJoin.Round;
            p.Stroke();
        }

        /// <summary>사다리꼴 안쪽만 칸으로 친다 (볼록 사각형: 네 변 모두 같은 쪽에 있으면 안).</summary>
        public override bool ContainsPoint(Vector2 localPoint)
        {
            if (layout.width <= 0f || layout.height <= 0f) return false;
            bool? sign = null;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = Local(i), b = Local((i + 1) % 4);
                float cross = (b.x - a.x) * (localPoint.y - a.y) - (b.y - a.y) * (localPoint.x - a.x);
                if (Mathf.Approximately(cross, 0f)) continue;
                bool s = cross > 0f;
                if (sign == null) sign = s;
                else if (sign != s) return false;
            }
            return true;
        }
    }
}
