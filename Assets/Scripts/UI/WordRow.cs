using System;
using System.Collections.Generic;
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
                var rt = UIFactory.Rect("Word " + token, container);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);

                var word = UIFactory.Text("Thai", rt, token, thaiSize, Theme.Ink);
                float w = word.GetPreferredValues(token).x;

                if (thai)
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

        public static void Clear(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(container.GetChild(i).gameObject);
        }
    }
}
