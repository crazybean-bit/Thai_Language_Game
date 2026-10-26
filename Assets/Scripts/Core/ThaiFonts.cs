using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ThaiMystery
{
    // Builds a dynamic TextMeshPro font asset from the bundled Sarabun TTF at runtime.
    // Sarabun covers Thai and Latin, so one asset renders both the Thai dialogue and the
    // player's English guesses. Dynamic mode adds glyphs on demand, so no atlas baking step.
    public static class ThaiFonts
    {
        private const string FontPath = "Fonts/Sarabun-Regular";
        private static TMP_FontAsset main;

        public static TMP_FontAsset Main
        {
            get
            {
                if (main != null) return main;

                var font = Resources.Load<Font>(FontPath);
                if (font == null)
                {
                    Debug.LogError($"[ThaiMystery] Font not found at Resources/{FontPath}.ttf");
                    return null;
                }

                main = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (main == null)
                {
                    Debug.LogError("[ThaiMystery] Could not create a TMP font asset from Sarabun. " +
                                   "Check the TTF import settings have 'Include Font Data' enabled.");
                    return null;
                }
                main.name = "Sarabun (Thai, runtime)";
                return main;
            }
        }
    }
}
