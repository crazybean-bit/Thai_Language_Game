using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ThaiMystery
{
    // Modal box for writing down what the player thinks a Thai word means.
    public class GuessPopup
    {
        private readonly GameObject root;
        private readonly TextMeshProUGUI thaiText;
        private readonly TextMeshProUGUI infoText;
        private readonly TMP_InputField field;
        private readonly GameObject listenButton;
        private string current;

        public bool IsOpen => root.activeSelf;

        public GuessPopup(Transform canvas)
        {
            var dim = UIFactory.Panel("Guess Popup", canvas, new Color(0, 0, 0, 0.55f));
            UIFactory.Stretch(dim.rectTransform);
            root = dim.gameObject;

            var box = UIFactory.Frame("Box", dim.transform);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 330));

            var title = UIFactory.Text("Title", box.transform, "What do you think this means?", 24, Theme.InkSoft);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(500, 36));

            thaiText = UIFactory.Text("Thai", box.transform, "", 64, Theme.Ink);
            UIFactory.Place(thaiText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(500, 100));

            // Shown only when he can recall how the word sounds. Listening again is practice.
            var listen = UIFactory.Panel("Listen", box.transform, Theme.Ink);
            UIFactory.Place(listen.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -96), new Vector2(40, 40));
            var icon = UIFactory.Panel("Icon", listen.transform, Theme.Paper, false);
            icon.sprite = SpriteLibrary.Get("ui_speaker");
            UIFactory.Stretch(icon.rectTransform, 6, 6, 6, 6);
            UIFactory.MakeButton(listen.gameObject, Listen);
            listenButton = listen.gameObject;

            field = UIFactory.InputField("Input", box.transform, "type a meaning...", 28);
            UIFactory.Place((RectTransform)field.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 78), new Vector2(420, 54));
            field.onSubmit.AddListener(Submit);

            infoText = UIFactory.Text("Info", box.transform, "", 18, Theme.InkSoft);
            infoText.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Place(infoText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(500, 50));

            root.SetActive(false);
        }

        public void Open(string thai)
        {
            current = thai;
            thaiText.text = thai;
            var words = Game.Words;
            var entry = words.GetEntry(thai);

            if (words.IsConfirmed(thai))
            {
                field.text = words.GetGuess(thai);
                field.interactable = false;
                string rom = entry?.romanization ?? "";
                string note = entry?.note ?? "";
                infoText.text = $"<color=#2F7A45>Confirmed</color>  ·  {rom}  ·  Esc to close\n{note}";
            }
            else
            {
                field.text = words.GetGuess(thai) ?? "";
                field.interactable = true;
                infoText.text = "Enter to save  ·  Esc to cancel\nWords lock in once every word in their group is right.";
            }

            root.SetActive(true);
            bool hasAudio = VoicePlayer.CanRecall(thai);
            listenButton.SetActive(hasAudio);
            if (hasAudio) Game.Voice.PlayWord(thai);

            if (field.interactable)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(field.gameObject);
                field.ActivateInputField();
            }
        }

        private void Listen()
        {
            // Play first: it should sound the way he knew it before this practice.
            if (Game.Voice.PlayWord(current)) Game.Words.Practice(current);
        }

        private void Submit(string value)
        {
            if (!IsOpen) return;
            Game.Words.SetGuess(current, value);
            Close();
        }

        public void Close()
        {
            root.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Game.ConsumeInputThisFrame();
        }
    }
}
