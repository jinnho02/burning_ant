using System.Collections.Generic;
using BurningAnt.Character;
using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// 주인공 그림 한 장에 코드로 움직임을 준다 (새 그림 없이).
    /// · 숨쉬기: 아래를 축으로 아주 살짝 커졌다 작아짐 (항상)
    /// · 들썩임: 이득일수록 더 높이·빨리 통통 (이득·큰 이득·대박)
    /// · 떨림 + 땀방울: 손해가 클수록 더 떨고 땀이 자주 흐름 (손해·큰 손해·폭락)
    /// · 표정이 바뀌는 순간 통 튀는 반응, 가끔 머리 위에 중얼거림 말풍선
    /// transform(translate·scale)만 바꾸므로 배치에는 영향이 없다.
    /// </summary>
    public class CharacterMotion
    {
        readonly VisualElement portrait, area;
        readonly Label mumble;
        readonly List<(VisualElement drop, float born, float x, float y)> drops = new();
        readonly System.Random rng = new();

        Mood mood = Mood.Flat;
        bool calm = true; // 장 전(뉴스·아이템 시간): 숨쉬기만
        float popStart = -10f, nextDrop, nextMumble, mumbleUntil;

        static readonly string[][] Mumbles =
        {
            new[] { "무리무리무리…", "끝났다…", "엄마…", "이건 꿈이야…" },   // 폭락
            new[] { "제발… 제발…", "왜 나만…", "손절할까…" },               // 큰 손해
            new[] { "으으…", "아직 괜찮아…", "버텨야 해…" },                // 손해
            new[] { "흠…", "오늘은 뭐가 오를까…", "빚이… 빚이…" },           // 보합
            new[] { "오?", "가즈아~", "흐흐…" },                            // 이득
            new[] { "이거 되는데?!", "흐흐흐…", "빚 갚자!" },                // 큰 이득
            new[] { "무리… 아니었어?!", "부자다~!", "꺄아아!" },             // 대박
        };

        public CharacterMotion(VisualElement portrait, VisualElement area)
        {
            this.portrait = portrait;
            this.area = area;
            portrait.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
            mumble = new Label { pickingMode = PickingMode.Ignore };
            mumble.AddToClassList("mumble");
            mumble.style.display = DisplayStyle.None;
            area.Add(mumble);
            nextMumble = Time.unscaledTime + 4f;
        }

        /// <summary>매 갱신 때 지금 표정을 알려 준다. 바뀌면 통 튀는 반응.</summary>
        public void SetMood(Mood m, bool beforeMarket)
        {
            if (m != mood || beforeMarket != calm) popStart = Time.unscaledTime;
            mood = m;
            calm = beforeMarket;
        }

        public void Update()
        {
            if (portrait.panel == null || portrait.resolvedStyle.display == DisplayStyle.None) return;
            float t = Time.unscaledTime;
            int level = calm ? 0 : (int)mood - (int)Mood.Flat; // -3(폭락) ~ +3(대박)

            // 숨쉬기 (3.2초 주기, 세로 1%)
            float breath = Mathf.Sin(t * Mathf.PI * 2f / 3.2f);
            float sy = 1f + 0.01f * breath, sx = 1f - 0.004f * breath;
            float x = 0f, y = 0f;

            if (level > 0)
            {
                // 들썩임: 이득 단계마다 더 높고 빠르게
                float speed = 1.6f + level * 0.7f, height = 2f + level * 3f;
                float hop = Mathf.Abs(Mathf.Sin(t * Mathf.PI * speed));
                y -= hop * height;
                sy += hop * 0.006f * level;
            }
            else if (level < 0)
            {
                // 떨림: 손해 단계마다 더 세게 (폭락은 바들바들)
                float amp = -level * 0.9f;
                x += (Mathf.PerlinNoise(t * 9f, 0.3f) - 0.5f) * 2f * amp * (level <= -2 ? 2f : 1f);
                y += (Mathf.PerlinNoise(0.7f, t * 9f) - 0.5f) * amp;
                if (level == -1) x += Mathf.Sin(t * 1.3f) * 2f; // 손해는 느리게 흔들흔들
            }

            // 표정이 바뀐 순간: 0.4초 동안 통 튀기
            float pop = t - popStart;
            if (pop < 0.4f)
            {
                float k = Mathf.Sin(pop / 0.4f * Mathf.PI) * (1f - pop / 0.4f * 0.5f);
                sy += 0.05f * k;
                sx += 0.03f * k;
                y -= 6f * k;
            }

            portrait.style.translate = new Translate(x, y);
            portrait.style.scale = new Scale(new Vector2(sx, sy));

            UpdateSweat(t, level);
            UpdateMumble(t, level);
        }

        void UpdateSweat(float t, int level)
        {
            var box = portrait.layout;
            if (level < 0 && t >= nextDrop && box.width > 0f && drops.Count < 4)
            {
                var drop = new VisualElement { pickingMode = PickingMode.Ignore };
                drop.AddToClassList("sweat-drop");
                drop.generateVisualContent += DrawDrop;
                area.Add(drop);
                // 얼굴 옆 (그림 위쪽 1/4 높이, 가운데에서 좌우로)
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float dx = box.x + box.width * (0.5f + side * (0.12f + (float)rng.NextDouble() * 0.08f));
                float dy = box.y + box.height * (0.18f + (float)rng.NextDouble() * 0.1f);
                drops.Add((drop, t, dx, dy));
                // 폭락은 0.5~1초마다, 큰 손해 1.2~2초, 손해 3~5초
                float gap = level <= -3 ? 0.5f : level == -2 ? 1.2f : 3f;
                nextDrop = t + gap + (float)rng.NextDouble() * gap;
            }

            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var (drop, born, dx, dy) = drops[i];
                float age = t - born;
                if (age > 1.1f || level >= 0) { drop.RemoveFromHierarchy(); drops.RemoveAt(i); continue; }
                drop.style.left = dx;
                drop.style.top = dy + age * age * 60f; // 점점 빨라지며 흘러내림
                drop.style.opacity = age < 0.15f ? age / 0.15f : 1f - Mathf.Clamp01((age - 0.7f) / 0.4f);
            }
        }

        // 물방울 모양 (위가 뾰족한 눈물)
        static void DrawDrop(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            float w = r.width, h = r.height;
            var p = ctx.painter2D;
            p.fillColor = new Color(0.55f, 0.8f, 1f, 0.95f);
            p.strokeColor = new Color(0.2f, 0.45f, 0.75f, 0.9f);
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(w * 0.5f, 0f));
            p.BezierCurveTo(new Vector2(w * 0.5f, h * 0.3f), new Vector2(w, h * 0.45f), new Vector2(w, h * 0.68f));
            p.Arc(new Vector2(w * 0.5f, h * 0.68f), w * 0.5f, 0f, 180f);
            p.BezierCurveTo(new Vector2(0f, h * 0.45f), new Vector2(w * 0.5f, h * 0.3f), new Vector2(w * 0.5f, 0f));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        void UpdateMumble(float t, int level)
        {
            if (mumble.style.display == DisplayStyle.Flex && t >= mumbleUntil)
                mumble.style.display = DisplayStyle.None;
            if (t < nextMumble) return;

            var lines = Mumbles[Mathf.Clamp(level + 3, 0, Mumbles.Length - 1)];
            mumble.text = lines[rng.Next(lines.Length)];
            var box = portrait.layout;
            mumble.style.left = box.x + box.width * 0.08f;
            mumble.style.top = box.y + box.height * 0.04f;
            mumble.style.display = DisplayStyle.Flex;
            mumble.BringToFront();
            mumbleUntil = t + 2.2f;
            // 감정이 셀수록 자주 중얼거린다
            float gap = Mathf.Abs(level) >= 2 ? 5f : 9f;
            nextMumble = t + gap + (float)rng.NextDouble() * 4f;
        }
    }
}
