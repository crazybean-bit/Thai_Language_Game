using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThaiMystery
{
    // Sentence tiles: he answers by putting words he has heard in order. Click a tile (or
    // press its number) to add it, Backspace or the Back button removes the last word, and
    // "Say it" (or Enter) says the sentence.
    //
    // Only words he has heard become tiles. When he's nervous the tiles come out shuffled
    // and a couple of them fog over; when he's confident, bonus tiles for a more natural
    // reply can appear.
    public class TilePanel
    {
        private const float Width = 900f;
        private const int FoggedWhenNervous = 2;

        private readonly GameObject root;
        private readonly TextMeshProUGUI prompt;
        private readonly RectTransform sentenceRow;
        private readonly RectTransform tileRow;
        private readonly System.Random random = new System.Random();
        private readonly List<string> tiles = new List<string>();
        private readonly List<string> sentence = new List<string>();
        private Action<List<string>> onSay;

        public bool IsOpen => root.activeSelf;

        public TilePanel(RectTransform parent, float bottom)
        {
            var frame = UIFactory.Frame("Tiles", parent);
            UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(Width, 250));
            root = frame.gameObject;

            prompt = UIFactory.Text("Prompt", frame.transform, "", 22, Theme.InkSoft, TextAlignmentOptions.MidlineLeft);
            prompt.fontStyle = FontStyles.Italic;
            UIFactory.Place(prompt.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -14), new Vector2(Width - 56, 30));

            var line = UIFactory.Panel("Sentence", frame.transform, new Color(0.17f, 0.13f, 0.12f, 0.08f), false);
            UIFactory.Place(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -50), new Vector2(Width - 230, 76));
            sentenceRow = line.rectTransform;

            Button("Back", frame.transform, new Vector2(-150, -50), new Vector2(56, 76), RemoveLast);
            Button("Say it", frame.transform, new Vector2(-28, -50), new Vector2(110, 76), Say);

            tileRow = UIFactory.Rect("Tile Row", frame.transform);
            UIFactory.Place(tileRow, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -140), new Vector2(Width - 56, 96));

            root.SetActive(false);
        }

        private static void Button(string label, Transform parent, Vector2 position, Vector2 size, Action onClick)
        {
            var bg = UIFactory.Panel(label, parent, Theme.Ink);
            UIFactory.Place(bg.rectTransform, new Vector2(1, 1), new Vector2(1, 1), position, size);
            var text = UIFactory.Text("Label", bg.transform, label, 24, Theme.Paper);
            UIFactory.Stretch(text.rectTransform);
            UIFactory.MakeButton(bg.gameObject, onClick);
        }

        public void Show(TileData data, bool nervous, bool confident, Action<List<string>> say)
        {
            onSay = say;
            sentence.Clear();
            prompt.text = string.IsNullOrEmpty(data.prompt) ? "Say something..." : data.prompt;

            tiles.Clear();
            AddTiles(data.answer?.Split(' '));
            AddTiles(data.words);
            if (confident) AddTiles(data.bonus);
            // Always shuffled, so the tile order never gives the answer away.
            for (int i = tiles.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
            }

            var fogged = new HashSet<int>();
            if (nervous)
                while (fogged.Count < Mathf.Min(FoggedWhenNervous, tiles.Count - 1))
                    fogged.Add(random.Next(tiles.Count));

            BuildTiles(fogged);
            RefreshSentence();
            root.SetActive(true);
        }

        // Only words he has heard can be tiles: he can't say what he's never heard.
        private void AddTiles(string[] words)
        {
            if (words == null) return;
            foreach (var raw in words)
            {
                var w = raw.Trim();
                if (w.Length == 0 || tiles.Contains(w) || !Game.Words.IsHeard(w)) continue;
                tiles.Add(w);
            }
        }

        private void BuildTiles(HashSet<int> fogged)
        {
            WordRow.Clear(tileRow);
            const float gap = 10f;
            float x = 0f;
            for (int i = 0; i < tiles.Count; i++)
            {
                string word = tiles[i];
                var bg = UIFactory.Frame("Tile " + word, tileRow, "ui_card");
                var rt = bg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);

                var thai = UIFactory.Text("Thai", rt, word, 34, WordRow.InkFor(Game.Words.StageOf(word)));
                float w = Mathf.Max(70f, thai.GetPreferredValues(word).x + 28f);
                UIFactory.Place(thai.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(w, 52));

                string guess = Game.Words.GetGuess(word);
                var hint = UIFactory.Text("Guess", rt, fogged.Contains(i) ? "" : guess ?? "?", 15,
                    Game.Words.IsConfirmed(word) ? Theme.Confirmed : Theme.InkSoft);
                UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(w, 22));

                if (i < 9)
                {
                    var key = UIFactory.Text("Key", rt, (i + 1).ToString(), 13, Theme.Unknown, TextAlignmentOptions.TopLeft);
                    UIFactory.Place(key.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -4), new Vector2(20, 16));
                }

                // Nerves: he knows this word, but right now it won't come into focus.
                if (fogged.Contains(i)) BlurredWord.Apply(thai, word, scramble: false);

                rt.sizeDelta = new Vector2(w, 90);
                rt.anchoredPosition = new Vector2(x, 0);
                x += w + gap;
                int index = i;
                UIFactory.MakeButton(bg.gameObject, () => Add(index));
            }
        }

        private void Add(int index)
        {
            if (index < 0 || index >= tiles.Count) return;
            sentence.Add(tiles[index]);
            RefreshSentence();
        }

        private void RemoveLast()
        {
            if (sentence.Count == 0) return;
            sentence.RemoveAt(sentence.Count - 1);
            RefreshSentence();
        }

        private void Say()
        {
            if (sentence.Count == 0) return;
            onSay?.Invoke(new List<string>(sentence));
        }

        private void RefreshSentence()
        {
            WordRow.Clear(sentenceRow);
            if (sentence.Count == 0)
            {
                var empty = UIFactory.Text("Empty", sentenceRow, "Click the words to build a sentence", 20, Theme.Unknown);
                UIFactory.Stretch(empty.rectTransform);
                return;
            }
            WordRow.Build(sentenceRow, string.Join(" ", sentence), 34, 15, Width - 250, null, out _);
        }

        public void HandleKeys()
        {
            int digit = GameInput.DigitPressed();
            if (digit > 0) Add(digit - 1);
            else if (GameInput.Backspace()) RemoveLast();
            else if (GameInput.Submit()) Say();
        }

        public void Hide() => root.SetActive(false);
    }
}
