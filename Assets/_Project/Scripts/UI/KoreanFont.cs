using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// 게임 폰트. 본문·숫자·버튼·작은 제목 = Pretendard (굵게는 Pretendard Bold), 큰 제목 몇 개만 Gmarket Sans Bold.
    /// 폰트 파일은 Resources/Fonts (둘 다 SIL OFL, 라이선스는 Fonts/ 폴더).
    /// 폰트 에셋은 실행할 때 ttf로 만든다 (에셋 파일로 두면 에디터에서 플레이할 때마다 파일이 바뀌어 깃에 잡히므로).
    /// </summary>
    public static class KoreanFont
    {
        // 이 클래스가 붙은 큰 제목만 Gmarket Sans Bold. 작은 글씨·버튼은 Gmarket이 뭉개져 보여서 Pretendard로 둔다.
        static readonly string[] TitleClasses = { "top-day", "modal-title", "end-title", "title-start", "shop-title" };

        // 한글은 글자 수가 많아서: 샘플 64pt + 2048 아틀라스(한 장에 약 700자) + 다 차면 아틀라스 추가.
        // 기본값(90pt, 1024 한 장)은 80자 남짓만 들어가 글자가 깨지거나 흐려진다. 힌팅은 끈다 (실제 빌드 캡처에서 획 두께가 들쭉날쭉해 더 깨져 보였음).
        static FontAsset Create(Font f) =>
            FontAsset.CreateFontAsset(f, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);

        const string Marker = "font-title";
        static FontAsset body, title;
        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var regular = Resources.Load<Font>("Fonts/Pretendard-Regular");
            var bold = Resources.Load<Font>("Fonts/Pretendard-Bold");
            var gmarket = Resources.Load<Font>("Fonts/GmarketSansTTFBold");
            if (regular != null) body = Create(regular);
            if (body != null && bold != null)
            {
                // -unity-font-style: bold 글자는 가짜 굵게 대신 진짜 Pretendard Bold로
                var boldAsset = Create(bold);
                if (boldAsset != null && body.fontWeightTable != null && body.fontWeightTable.Length > 7)
                    body.fontWeightTable[7].regularTypeface = boldAsset;
            }
            if (gmarket != null) title = Create(gmarket);

            // 폰트 파일이 없으면 예전처럼 OS 한글 폰트
            if (body == null)
                foreach (var family in new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR" })
                    if ((body = FontAsset.CreateFontAsset(family, "Regular")) != null) break;
            if (body == null) Debug.LogWarning("한글 폰트를 찾지 못했어요. 한글이 네모로 보일 수 있어요.");
        }

        public static void Apply(VisualElement root)
        {
            Load();
            if (body == null) return;
            root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(body));
            if (title == null) return;
            ApplyTitles(root);
            // 나중에 코드로 만들어지는 제목도 바꾸도록 주기적으로 훑는다 (가벼움)
            root.schedule.Execute(() => ApplyTitles(root)).Every(200);
        }

        static void ApplyTitles(VisualElement root)
        {
            foreach (var c in TitleClasses) root.Query(className: c).ForEach(SetTitle);
        }

        static void SetTitle(VisualElement e)
        {
            if (e.ClassListContains(Marker)) return;
            e.AddToClassList(Marker);
            {
                e.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(title));
                e.style.unityFontStyleAndWeight = FontStyle.Normal; // 이미 굵은 폰트라 가짜 굵게는 빼기
            }
        }
    }
}
