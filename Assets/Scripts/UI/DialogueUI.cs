using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThaiMystery
{
    // The message box at the bottom of the screen: nameplate, an optional character
    // portrait, Thai words with the player's guesses under them, and choice buttons.
    // Text appears gradually; DialogueRunner drives the reveal with SetRevealed.
    public class DialogueUI
    {
        private const float BoxWidth = 1100f;
        private const float BoxHeight = 210f;
        private const float TextInset = 40f;
        private const float PortraitInset = 200f;

        private readonly GameObject root;
        private readonly RectTransform words;
        private readonly GameObject namePlate;
        private readonly TextMeshProUGUI nameText;
        private readonly TextMeshProUGUI thoughtText;
        private readonly GameObject continueArrow;
        private readonly Image portrait;
        private readonly RectTransform choicesRoot;
        private readonly Action<string> onWordClicked;

        private readonly List<WordChip> chips = new List<WordChip>();
        private readonly List<Image> choiceBackgrounds = new List<Image>();
        private bool thaiLine;

        // How many steps the current line reveals in: words for Thai, characters for thoughts.
        public int RevealUnits { get; private set; }
        public bool IsThaiLine => thaiLine;

        public DialogueUI(Transform canvas, Action onBoxClicked, Action<string> onWordClicked)
        {
            this.onWordClicked = onWordClicked;

            var rootRt = UIFactory.Rect("Dialogue", canvas);
            UIFactory.Stretch(rootRt);
            root = rootRt.gameObject;

            var box = UIFactory.Frame("Box", rootRt);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24),
                new Vector2(BoxWidth, BoxHeight));
            UIFactory.MakeButton(box.gameObject, onBoxClicked).transition = Selectable.Transition.None;

            words = UIFactory.Rect("Words", box.transform);
            UIFactory.Stretch(words, TextInset, TextInset, 26, 20);

            thoughtText = UIFactory.Text("Thought", box.transform, "", 30, Theme.InkSoft);
            thoughtText.fontStyle = FontStyles.Italic;
            thoughtText.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Stretch(thoughtText.rectTransform, 60, 60, 30, 30);

            var plate = UIFactory.Panel("Nameplate", rootRt, Theme.Header);
            UIFactory.Place(plate.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 0),
                new Vector2(-BoxWidth / 2 + 30, 24 + BoxHeight - 6), new Vector2(220, 48));
            namePlate = plate.gameObject;
            nameText = UIFactory.Text("Name", plate.transform, "", 28, Theme.Paper);
            UIFactory.Stretch(nameText.rectTransform);

            var arrow = UIFactory.Panel("Continue", box.transform, Theme.Ink, false);
            arrow.sprite = SpriteLibrary.Get("ui_arrow");
            UIFactory.Place(arrow.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 20), new Vector2(24, 24));
            continueArrow = arrow.gameObject;

            // The portrait stands at the right edge, in front of the box, like a visual novel.
            portrait = UIFactory.Panel("Portrait", rootRt, Color.white, false);
            portrait.preserveAspect = true;
            UIFactory.Place(portrait.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 0), new Vector2(272, 340));
            portrait.gameObject.SetActive(false);

            choicesRoot = UIFactory.Rect("Choices", rootRt);
            UIFactory.Place(choicesRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-120, 24 + BoxHeight + 60), new Vector2(460, 10));

            root.SetActive(false);
        }

        public void ShowLine(LineData line, string portraitSprite)
        {
            root.SetActive(true);
            HideChoices();

            bool hasName = !string.IsNullOrEmpty(line.speaker);
            namePlate.SetActive(hasName);
            nameText.text = hasName ? line.speaker : "";

            var sprite = string.IsNullOrEmpty(portraitSprite) ? null : SpriteLibrary.Get(portraitSprite);
            portrait.sprite = sprite;
            portrait.gameObject.SetActive(sprite != null);
            float rightInset = TextInset + (sprite != null ? PortraitInset : 0f);
            words.offsetMax = new Vector2(-rightInset, words.offsetMax.y);

            WordRow.Clear(words);
            chips.Clear();
            string text = line.text ?? "";
            thaiLine = WordBook.IsThai(text);

            if (thaiLine)
            {
                thoughtText.text = "";
                chips.AddRange(WordRow.Build(words, text, 46, 20, BoxWidth - TextInset - rightInset, onWordClicked, out _));
                RevealUnits = words.childCount;
            }
            else
            {
                // The protagonist's own thoughts are in the player's language.
                thoughtText.text = text;
                RevealUnits = text.Length;
            }
            SetRevealed(0);
        }

        public void SetRevealed(int count)
        {
            if (thaiLine)
            {
                for (int i = 0; i < words.childCount; i++)
                    words.GetChild(i).gameObject.SetActive(i < count);
            }
            else
            {
                thoughtText.maxVisibleCharacters = count;
            }
            continueArrow.SetActive(false);
        }

        public void SetContinueVisible(bool visible) => continueArrow.SetActive(visible);

        public void ShowChoices(ChoiceData[] choices, Action<int> onChoose)
        {
            HideChoices();
            continueArrow.SetActive(false);
            float y = 0f;
            for (int i = choices.Length - 1; i >= 0; i--)
            {
                int index = i;
                var bg = UIFactory.Frame("Choice " + i, choicesRoot);
                var rt = bg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);

                var content = UIFactory.Rect("Words", rt);
                content.anchorMin = content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 1);
                content.anchoredPosition = new Vector2(20, -12);
                content.sizeDelta = new Vector2(420, 10);
                chips.AddRange(WordRow.Build(content, choices[i].text, 34, 17, 420, null, out float h));

                rt.sizeDelta = new Vector2(460, h + 24);
                rt.anchoredPosition = new Vector2(0, y);
                y += h + 34;

                UIFactory.MakeButton(bg.gameObject, () => onChoose(index)).transition = Selectable.Transition.None;
                choiceBackgrounds.Insert(0, bg);
            }
        }

        public void HighlightChoice(int index)
        {
            for (int i = 0; i < choiceBackgrounds.Count; i++)
                choiceBackgrounds[i].color = i == index ? Theme.Highlight : Color.white;
        }

        public void HideChoices()
        {
            WordRow.Clear(choicesRoot);
            choiceBackgrounds.Clear();
        }

        public void Hide()
        {
            HideChoices();
            root.SetActive(false);
        }

        public void RefreshGuesses()
        {
            foreach (var c in chips) c.Refresh();
        }
    }
}
