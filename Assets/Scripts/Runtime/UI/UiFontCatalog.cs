using TMPro;
using UnityEngine;

namespace EggRescue
{
    /// <summary>
    /// 界面中文用的 MiSans。图集满了之后可以继续加页，避免新字变成方框。
    /// </summary>
    public sealed class UiFontCatalog : ScriptableObject
    {
        public TMP_FontAsset Font;
        public Sprite ButtonSprite;

        public static TMP_FontAsset Load()
        {
            var catalog = Resources.Load<UiFontCatalog>("UiFont");
            if (catalog != null && catalog.Font != null) return catalog.Font;
            var texts = FindObjectsOfType<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var font = texts[i] != null ? texts[i].font : null;
                if (font != null && font.name.IndexOf("MiSans", System.StringComparison.Ordinal) >= 0)
                    return font;
            }
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null)
                    return texts[i].font;
            }
            return TMP_Settings.defaultFontAsset;
        }

        public static Sprite LoadButtonSprite()
        {
            var catalog = Resources.Load<UiFontCatalog>("UiFont");
            return catalog != null ? catalog.ButtonSprite : null;
        }
    }
}
