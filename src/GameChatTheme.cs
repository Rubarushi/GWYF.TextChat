using System.Linq;
using TMPro;
using UnityEngine;

namespace GWYF.TextChat
{
    // Borrow only the game's font; chat has no background or frame assets.
    internal static class GameChatTheme
    {
        internal static readonly Color Paper = new Color32(247, 242, 226, 255);
        internal static readonly Color Gold = new Color32(255, 219, 82, 255);

        internal static TMP_FontAsset FindFont()
        {
            return Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .Where(t => t.font != null && t.gameObject.scene.IsValid() &&
                    t.transform.root.name != "TextChatCanvas")
                .GroupBy(t => t.font)
                .OrderByDescending(g => g.Sum(t => (t.isActiveAndEnabled ? 10 : 1) +
                    ((t.text ?? "").Any(c => c >= '\uac00' && c <= '\ud7a3') ? 25 : 0)))
                .Select(g => g.Key).FirstOrDefault() ?? TMP_Settings.defaultFontAsset;
        }

    }
}
