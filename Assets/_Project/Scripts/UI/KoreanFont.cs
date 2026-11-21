using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace BurningAnt.UI
{
    /// <summary>기본 런타임 폰트에는 한글이 없어서 한글 폰트를 루트에 지정한다.</summary>
    public static class KoreanFont
    {
        // shortcut: OS에 설치된 폰트를 참조함 (빌드에 폰트가 안 들어감). 출시 전 Noto Sans KR 등 배포 가능한 폰트를 Fonts/에 넣어 교체.
        static readonly string[] Families = { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR" };
        static FontAsset cached;

        public static void Apply(VisualElement root)
        {
            if (cached == null)
            {
                foreach (var family in Families)
                {
                    cached = FontAsset.CreateFontAsset(family, "Regular");
                    if (cached != null) break;
                }
            }

            if (cached == null)
            {
                Debug.LogWarning("한글 폰트를 찾지 못했어요. 한글이 네모로 보일 수 있어요.");
                return;
            }
            root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(cached));
        }
    }
}
