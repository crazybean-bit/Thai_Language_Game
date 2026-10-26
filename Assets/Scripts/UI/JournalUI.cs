using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThaiMystery
{
    // The word journal: a grid of every Thai word seen so far. Each card's header shows the
    // player's guess; click a card to change it. Confirmed words turn green and show how
    // the word is pronounced.
    public class JournalUI
    {
        private readonly GameObject root;
        private readonly RectTransform grid;
        private readonly TextMeshProUGUI progress;
        private readonly TextMeshProUGUI empty;
        private readonly System.Action<string> onCardClicked;

        public bool IsOpen => root.activeSelf;

        public JournalUI(Transform canvas, System.Action<string> onCardClicked)
        {
            this.onCardClicked = onCardClicked;

            var bg = UIFactory.Panel("Journal", canvas, Theme.Backdrop);
            UIFactory.Stretch(bg.rectTransform);
            root = bg.gameObject;

            var page = UIFactory.Frame("Page", bg.transform);
            UIFactory.Stretch(page.rectTransform, 110, 110, 40, 40);

            var title = UIFactory.Text("Title", page.transform, "Journal", 34, Theme.Ink, TextAlignmentOptions.MidlineLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -18), new Vector2(400, 50));

            progress = UIFactory.Text("Progress", page.transform, "", 22, Theme.InkSoft, TextAlignmentOptions.MidlineRight);
            UIFactory.Place(progress.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -18), new Vector2(520, 50));

            var hint = UIFactory.Text("Hint", page.transform, "Click a word to write what you think it means  ·  J / Tab / Esc to close",
                18, Theme.InkSoft);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(900, 30));

            // Scroll view: viewport masks the grid, the grid grows with its cards.
            var viewport = UIFactory.Panel("Viewport", page.transform, new Color(0, 0, 0, 0.001f));
            UIFactory.Stretch(viewport.rectTransform, 30, 30, 76, 50);
            viewport.gameObject.AddComponent<RectMask2D>();

            grid = UIFactory.Rect("Grid", viewport.transform);
            grid.anchorMin = new Vector2(0, 1);
            grid.anchorMax = new Vector2(1, 1);
            grid.pivot = new Vector2(0.5f, 1);
            grid.sizeDelta = Vector2.zero;
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(230, 150);
            layout.spacing = new Vector2(16, 16);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.padding = new RectOffset(0, 0, 8, 8);
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = grid;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            empty = UIFactory.Text("Empty", page.transform, "No words yet. Talk to people and inspect things.", 24, Theme.InkSoft);
            UIFactory.Stretch(empty.rectTransform);

            root.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            root.SetActive(true);
            Rebuild();
        }

        public void Close()
        {
            root.SetActive(false);
            Game.ConsumeInputThisFrame();
        }

        public void Rebuild()
        {
            if (!IsOpen) return;
            WordRow.Clear(grid);
            var words = Game.Words;
            IReadOnlyList<string> found = words.Discovered;
            empty.gameObject.SetActive(found.Count == 0);
            progress.text = $"Seen {found.Count}  ·  Confirmed {words.ConfirmedCount} / {words.TotalWords}";

            foreach (var thai in found) BuildCard(thai);
        }

        private void BuildCard(string thai)
        {
            var words = Game.Words;
            bool confirmed = words.IsConfirmed(thai);
            string guess = words.GetGuess(thai);

            var card = UIFactory.Rect("Card " + thai, grid);
            var hit = card.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0.001f);
            UIFactory.MakeButton(card.gameObject, () => onCardClicked(thai));

            var header = UIFactory.Panel("Header", card, confirmed ? Theme.Confirmed : Theme.Header, false);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(230, 44));
            var guessText = UIFactory.Text("Guess", header.transform, string.IsNullOrEmpty(guess) ? "????" : guess, 24, Theme.Paper);
            UIFactory.Stretch(guessText.rectTransform, 6, 6, 0, 0);
            guessText.overflowMode = TextOverflowModes.Ellipsis;

            var body = UIFactory.Frame("Body", card, "ui_card");
            body.raycastTarget = false;
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(222, 102));

            var thaiText = UIFactory.Text("Thai", body.transform, thai, 40, Theme.Ink);
            UIFactory.Stretch(thaiText.rectTransform, 4, 4, 4, confirmed ? 26 : 4);

            if (confirmed)
            {
                var entry = words.GetEntry(thai);
                var rom = UIFactory.Text("Romanization", body.transform, entry?.romanization ?? "", 18, Theme.Confirmed);
                UIFactory.Place(rom.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(210, 24));
            }
        }
    }
}
