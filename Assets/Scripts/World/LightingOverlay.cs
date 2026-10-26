using UnityEngine;

namespace ThaiMystery
{
    // A soft darkness layer over the room with pools of light, baked once into a small
    // texture from the lights in map.json. Bilinear filtering on a low resolution texture
    // gives smooth gradients for free. Flickering lights gently pulse the whole layer.
    [RequireComponent(typeof(SpriteRenderer))]
    public class LightingOverlay : MonoBehaviour
    {
        private const int PixelsPerTile = 8;
        private const int Margin = 1;

        private SpriteRenderer sr;
        private bool flicker;

        public void Build(LightingData data, GridMap map)
        {
            sr = GetComponent<SpriteRenderer>();
            if (data == null || data.darkness <= 0f)
            {
                sr.enabled = false;
                return;
            }

            int w = (map.Width + Margin * 2) * PixelsPerTile;
            int h = (map.Height + Margin * 2) * PixelsPerTile;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Lighting"
            };

            Color dark = ParseColor(data.darkColor, new Color(0.04f, 0.03f, 0.08f));
            var lights = data.lights ?? new LightData[0];
            var centres = new Vector2[lights.Length];
            var colors = new Color[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                var cell = map.FromRowCol(lights[i].x, lights[i].y);
                centres[i] = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                colors[i] = ParseColor(lights[i].color, new Color(1f, 0.7f, 0.4f));
                flicker |= lights[i].flicker;
            }

            var pixels = new Color[w * h];
            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    var world = new Vector2((px + 0.5f) / PixelsPerTile - Margin, (py + 0.5f) / PixelsPerTile - Margin);
                    float light = 0f;
                    Color tint = Color.black;
                    for (int i = 0; i < lights.Length; i++)
                    {
                        float d = Vector2.Distance(world, centres[i]) / Mathf.Max(0.1f, lights[i].radius);
                        if (d >= 1f) continue;
                        float f = (1f - d) * (1f - d) * lights[i].intensity;
                        light += f;
                        tint += colors[i] * f;
                    }
                    float l = Mathf.Clamp01(light);
                    Color warm = light > 0f ? tint / light : dark;
                    // Away from lights: dark and mostly opaque. Near lights: a faint coloured wash.
                    var c = Color.Lerp(dark, warm, l);
                    c.a = Mathf.Lerp(data.darkness, 0.12f * Mathf.Clamp01(light), l);
                    pixels[py * w + px] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, PixelsPerTile);
            sr.sortingOrder = 20000;
            transform.position = new Vector3(-Margin, -Margin, 0f);
        }

        private void Update()
        {
            if (!flicker || sr == null) return;
            float n = Mathf.PerlinNoise(Time.time * 2.5f, 0.3f);
            sr.color = new Color(1f, 1f, 1f, 0.94f + 0.06f * n);
        }

        private static Color ParseColor(string hex, Color fallback) =>
            ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    }
}
