using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>
    /// 게임 폰트. 본문·숫자 = Pretendard (굵게는 Pretendard Bold), 제목·버튼·강조 = Gmarket Sans Bold.
    /// 폰트 파일은 Resources/Fonts (둘 다 SIL OFL, 라이선스는 Fonts/ 폴더).
    /// 폰트 에셋은 실행할 때 ttf로 만든다 (에셋 파일로 두면 에디터에서 플레이할 때마다 파일이 바뀌어 깃에 잡히므로).
    /// </summary>
    public static class KoreanFont
    {
        // 이 클래스가 붙은 글자는 Gmarket Sans Bold (제목·강조). 버튼은 전부 Gmarket.
        static readonly string[] TitleClasses =
        {
            "top-day", "news-band-title", "item-bar-title", "side-title", "modal-title", "end-title",
            "title-start", "report-section-title", "tutorial-title", "pay-box-title", "shop-title",
            "held-box-title", "telescope-title", "prop-title", "panel-title", "news-paper-date",
        };

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
            if (regular != null) body = FontAsset.CreateFontAsset(regular);
            if (body != null && bold != null)
            {
                // -unity-font-style: bold 글자는 가짜 굵게 대신 진짜 Pretendard Bold로
                var boldAsset = FontAsset.CreateFontAsset(bold);
                if (boldAsset != null && body.fontWeightTable != null && body.fontWeightTable.Length > 7)
                    body.fontWeightTable[7].regularTypeface = boldAsset;
            }
            if (gmarket != null) title = FontAsset.CreateFontAsset(gmarket);

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
            // 나중에 코드로 만들어지는 제목·버튼(결과 창, 상점 등)도 바꾸도록 주기적으로 훑는다 (가벼움)
            root.schedule.Execute(() => ApplyTitles(root)).Every(200);
        }

        static void ApplyTitles(VisualElement root)
        {
            foreach (var c in TitleClasses) root.Query(className: c).ForEach(SetTitle);
            root.Query<Button>().ForEach(SetTitle);
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
