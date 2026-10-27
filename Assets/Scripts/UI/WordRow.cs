using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThaiMystery
{
    // One Thai word in a line of dialogue, with the player's current guess written under it,
    // the way 7 Days to End with You annotates its script.
    public class WordChip
    {
        public string Thai;
        public TextMeshProUGUI GuessText;

        public void Refresh()
        {
            if (GuessText == null) return;
            var words = Game.Words;
            string guess = words.GetGuess(Thai);
            if (words.IsConfirmed(Thai))
            {
                GuessText.text = guess;
                GuessText.color = Theme.Confirmed;
            }
            else if (!string.IsNullOrEmpty(guess))
            {
                GuessText.text = guess;
                GuessText.color = Theme.InkSoft;
            }
            else
            {
                GuessText.text = "?";
                GuessText.color = Theme.Unknown;
            }
        }
    }

    // Lays a line of space separated words out left to right, wrapping and centring rows.
    // Thai has no spaces between words in normal writing; the game's script adds them so
    // each word can be studied on its own.
    //
    // Words the protagonist has never heard pronounced are drawn as a blurred, shifting jumble
    // of letters he can't focus on (BlurredWord), and can't be clicked. The ink darkens as a
    // word moves through its stages, and is solid once he owns it.
    public static class WordRow
    {
        public static List<WordChip> Build(RectTransform container, string text, float thaiSize, float guessSize,
            float maxWidth, Action<string> onWordClicked, out float totalHeight)
        {
            var chips = new List<WordChip>();
            var rows = new List<List<(RectTransform rt, float w)>> { new List<(RectTransform, float)>() };

            const float gap = 10f;
            float chipH = thaiSize * 1.55f + guessSize * 1.3f;
            float rowWidth = 0f;

            foreach (var token in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                bool thai = WordBook.IsThai(token);
                var stage = thai ? Game.Words.StageOf(token) : WordStage.Owned;
                bool jumbled = stage == WordStage.Noise;
                string shown = jumbled ? Jumble(token) : token;
                var rt = UIFactory.Rect("Word " + token, container);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);

                var color = jumbled ? Theme.Unknown : InkFor(stage);
                var word = UIFactory.Text("Thai", rt, shown, thaiSize, color);
                float w = word.GetPreferredValues(shown).x;

                if (thai && !jumbled)
                {
                    var chip = new WordChip { Thai = token };
                    chip.GuessText = UIFactory.Text("Guess", rt, "?", guessSize, Theme.Unknown);
                    chip.Refresh();
                    w = Mathf.Max(w, chip.GuessText.GetPreferredValues("mmmm").x, chip.GuessText.GetPreferredValues(chip.GuessText.text).x);
                    chips.Add(chip);

                    var guessRt = chip.GuessText.rectTransform;
                    guessRt.anchorMin = new Vector2(0, 0);
                    guessRt.anchorMax = new Vector2(1, 0);
                    guessRt.pivot = new Vector2(0.5f, 0);
                    guessRt.anchoredPosition = Vector2.zero;
                    guessRt.sizeDelta = new Vector2(0, guessSize * 1.3f);

                    if (onWordClicked != null)
                    {
                        var hit = rt.gameObject.AddComponent<Image>();
                        hit.color = new Color(1, 1, 1, 0.001f);
                        var btn = UIFactory.MakeButton(rt.gameObject, () => onWordClicked(token));
                        btn.transition = Selectable.Transition.ColorTint;
                        btn.targetGraphic = hit;
                        btn.colors = new ColorBlock
                        {
                            normalColor = new Color(1, 1, 1, 0.001f),
                            highlightedColor = new Color(0.55f, 0.42f, 0.25f, 0.25f),
                            pressedColor = new Color(0.55f, 0.42f, 0.25f, 0.4f),
                            selectedColor = new Color(1, 1, 1, 0.001f),
                            disabledColor = new Color(1, 1, 1, 0.001f),
                            colorMultiplier = 1, fadeDuration = 0.08f
                        };
                    }
                }

                var wordRt = word.rectTransform;
                wordRt.anchorMin = new Vector2(0, 1);
                wordRt.anchorMax = new Vector2(1, 1);
                wordRt.pivot = new Vector2(0.5f, 1);
                wordRt.anchoredPosition = Vector2.zero;
                wordRt.sizeDelta = new Vector2(0, thaiSize * 1.55f);
                if (jumbled) BlurredWord.Apply(word, token);

                w += 16f;
                rt.sizeDelta = new Vector2(w, chipH);

                if (rowWidth > 0 && rowWidth + w > maxWidth)
                {
                    rows.Add(new List<(RectTransform, float)>());
                    rowWidth = 0;
                }
                rows[rows.Count - 1].Add((rt, w));
                rowWidth += w + gap;
            }

            float y = 0f;
            foreach (var row in rows)
            {
                float width = -gap;
                foreach (var item in row) width += item.w + gap;
                float x = Mathf.Max(0, (maxWidth - width) / 2f);
                foreach (var item in row)
                {
                    item.rt.anchoredPosition = new Vector2(x, -y);
                    x += item.w + gap;
                }
                y += chipH + 4f;
            }
            totalHeight = y;
            return chips;
        }

        // Ink for a word he can make out: faint when just heard, solid once owned.
        public static Color InkFor(WordStage stage)
        {
            switch (stage)
            {
                case WordStage.Heard: return Color.Lerp(Theme.Unknown, Theme.InkSoft, 0.5f);
                case WordStage.Understood: return Theme.InkSoft;
                case WordStage.Familiar: return Color.Lerp(Theme.InkSoft, Theme.Ink, 0.55f);
                default: return Theme.Ink;
            }
        }

        private const string Consonants = "กขคงจฉชซญดตถทนบปผพฟมยรลวสหอฮ";
        private const string Marks = "\u0e34\u0e35\u0e37\u0e48\u0e49";

        // A stand-in for a word he can't make out yet: about as long, never the same letters.
        // Each variant is a different jumble of the same length.
        public static string Jumble(string thai, int variant = 0)
        {
            uint seed = 2166136261 ^ (uint)variant * 2654435761;
            foreach (char c in thai) seed = (seed ^ c) * 16777619;
            uint Next() { seed = seed * 1664525 + 1013904223; return seed >> 8; }

            var sb = new StringBuilder();
            foreach (char c in thai)
            {
                if (IsMark(c)) continue;
                sb.Append(Consonants[(int)(Next() % (uint)Consonants.Length)]);
                if (Next() % 4 == 0) sb.Append(Marks[(int)(Next() % (uint)Marks.Length)]);
            }
            return sb.ToString();
        }

        // Thai vowel and tone marks sit above or below the letter before them.
        private static bool IsMark(char c) => c == '\u0e31' || (c >= '\u0e34' && c <= '\u0e3a') || (c >= '\u0e47' && c <= '\u0e4e');

        public static void Clear(RectTransform container)
        {
            // Detach first: Destroy only happens at the end of the frame, and callers
            // rebuild (and count children) straight away.
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}

namespace ThaiMystery
{
    // An unheard word: faint, soft-edged copies of a jumble that drift apart and keep
    // changing letters, so it reads as something he can't focus on rather than a real word.
    public class BlurredWord : MonoBehaviour
    {
        private const int Ghosts = 2;
        private const float SecondsPerShift = 0.22f;

        private readonly List<TextMeshProUGUI> layers = new List<TextMeshProUGUI>();
        private readonly List<Material> materials = new List<Material>();
        private string thai;
        private bool scramble;
        private float timer;
        private int shift;

        // scramble: false keeps the real letters, just out of focus (a word he knows but
        // can't bring to mind right now).
        public static void Apply(TextMeshProUGUI word, string thai, bool scramble = true)
        {
            var blur = word.gameObject.AddComponent<BlurredWord>();
            blur.thai = thai;
            blur.scramble = scramble;
            blur.AddLayer(word, 0.5f, 0.45f);
            var source = word.rectTransform;
            for (int i = 0; i < Ghosts; i++)
            {
                var ghost = UIFactory.Text("Ghost", source.parent, word.text, word.fontSize, word.color);
                var rt = ghost.rectTransform;
                rt.anchorMin = source.anchorMin;
                rt.anchorMax = source.anchorMax;
                rt.pivot = source.pivot;
                rt.sizeDelta = source.sizeDelta;
                blur.AddLayer(ghost, 0.22f, 0.75f);
            }
            blur.Shift();
        }

        private void AddLayer(TextMeshProUGUI text, float alpha, float softness)
        {
            var c = text.color;
            c.a = alpha;
            text.color = c;
            // Soft edges on the signed distance field read as out of focus. fontMaterial gives
            // this text its own copy of the material, so other text stays sharp.
            var material = text.fontMaterial;
            material.SetFloat("_OutlineSoftness", softness);
            text.extraPadding = true;
            materials.Add(material);
            layers.Add(text);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= SecondsPerShift)
            {
                timer = 0f;
                shift++;
                Shift();
            }
            float t = Time.time;
            for (int i = 1; i < layers.Count; i++)
            {
                float phase = t * (1.3f + 0.4f * i) + i * 2.1f;
                layers[i].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(phase) * 4f, Mathf.Cos(phase * 0.7f) * 2f);
            }
        }

        private void Shift()
        {
            for (int i = 0; i < layers.Count; i++)
                layers[i].text = scramble ? WordRow.Jumble(thai, shift * (Ghosts + 1) + i) : thai;
        }

        private void OnDestroy()
        {
            foreach (var m in materials) Destroy(m);
        }
    }
}
