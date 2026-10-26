using System.Collections.Generic;
using UnityEngine;

namespace ThaiMystery
{
    // Loads PNGs from Resources/Sprites and turns them into sprites at runtime, so the
    // prototype doesn't depend on hand-tuned import settings. 32 pixels = 1 tile = 1 unit.
    public static class SpriteLibrary
    {
        public const int PixelsPerUnit = 32;

        private static readonly Dictionary<string, Sprite> singles = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite[,]> sheets = new Dictionary<string, Sprite[,]>();

        public static Sprite Get(string name)
        {
            if (singles.TryGetValue(name, out var s)) return s;
            var tex = LoadTexture(name);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), PixelsPerUnit);
            s.name = name;
            singles[name] = s;
            return s;
        }

        // A 9-sliced sprite for UI panels. Border is in source pixels.
        public static Sprite GetSliced(string name, int border)
        {
            string key = name + "#sliced";
            if (singles.TryGetValue(key, out var s)) return s;
            var tex = LoadTexture(name);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            s.name = key;
            singles[key] = s;
            return s;
        }

        // RPG Maker style character sheet: 3 columns (step, idle, step) by 4 rows (down, left, right, up).
        // Returned as [row, column].
        public static Sprite[,] GetCharacterSheet(string name)
        {
            if (sheets.TryGetValue(name, out var sheet)) return sheet;
            var tex = LoadTexture(name);
            if (tex == null) return null;

            sheet = new Sprite[4, 3];
            float fw = tex.width / 3f;
            float fh = tex.height / 4f;
            float ppu = PixelsPerUnit * (fw / 32f);
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    // Texture rows start at the bottom, sheet rows at the top.
                    var rect = new Rect(col * fw, tex.height - (row + 1) * fh, fw, fh);
                    var sp = Sprite.Create(tex, rect, new Vector2(0.5f, 0f), ppu);
                    sp.name = $"{name}_{row}_{col}";
                    sheet[row, col] = sp;
                }
            }
            sheets[name] = sheet;
            return sheet;
        }

        private static Texture2D LoadTexture(string name)
        {
            var tex = Resources.Load<Texture2D>("Sprites/" + name);
            if (tex == null)
            {
                Debug.LogError($"[ThaiMystery] Missing sprite Resources/Sprites/{name}.png");
                return null;
            }
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }
    }
}
