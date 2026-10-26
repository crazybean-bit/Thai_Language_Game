using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThaiMystery
{
    // Steps through dialogue from dialogue.json: shows lines, handles choices,
    // sets flags and records every Thai word the player sees in the journal.
    public class DialogueRunner : MonoBehaviour
    {
        private readonly Dictionary<string, DialogueData> dialogues = new Dictionary<string, DialogueData>();
        private DialogueUI ui;

        private DialogueData current;
        private int lineIndex;
        private bool awaitingChoice;
        private int choiceIndex;
        private Action onFinished;

        public bool IsOpen { get; private set; }

        public void Init(DialogueList list, DialogueUI dialogueUI)
        {
            ui = dialogueUI;
            if (list?.dialogues == null) return;
            foreach (var d in list.dialogues) dialogues[d.id] = d;
        }

        public void Play(string id, Action done = null)
        {
            if (!dialogues.TryGetValue(id, out var d) || d.lines == null || d.lines.Length == 0)
            {
                Debug.LogWarning($"[ThaiMystery] Dialogue '{id}' not found in dialogue.json");
                done?.Invoke();
                return;
            }
            IsOpen = true;
            onFinished = done;
            Game.ConsumeInputThisFrame();
            Begin(d);
        }

        private void Begin(DialogueData d)
        {
            current = d;
            lineIndex = 0;
            ShowLine();
        }

        private void ShowLine()
        {
            var line = current.lines[lineIndex];
            Game.Flags.Set(line.setFlag);
            Discover(line.text);
            ui.ShowLine(line);

            awaitingChoice = line.choices != null && line.choices.Length > 0;
            if (awaitingChoice)
            {
                foreach (var c in line.choices) Discover(c.text);
                choiceIndex = 0;
                ui.ShowChoices(line.choices, Choose);
                ui.HighlightChoice(choiceIndex);
            }
        }

        private static void Discover(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var token in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (WordBook.IsThai(token)) Game.Words.Discover(token);
        }

        private void Update()
        {
            if (!IsOpen || Game.InputConsumed || Game.UI.ModalOpen) return;

            if (awaitingChoice)
            {
                int count = current.lines[lineIndex].choices.Length;
                if (GameInput.UpPressed()) choiceIndex = (choiceIndex + count - 1) % count;
                else if (GameInput.DownPressed()) choiceIndex = (choiceIndex + 1) % count;
                else if (GameInput.Confirm()) { Choose(choiceIndex); return; }
                ui.HighlightChoice(choiceIndex);
                return;
            }

            if (GameInput.Confirm()) Advance();
        }

        public void OnBoxClicked()
        {
            if (IsOpen && !awaitingChoice) Advance();
        }

        private void Advance()
        {
            Game.ConsumeInputThisFrame();
            lineIndex++;
            if (lineIndex < current.lines.Length) ShowLine();
            else Finish();
        }

        private void Choose(int index)
        {
            if (!awaitingChoice) return;
            var choice = current.lines[lineIndex].choices[index];
            awaitingChoice = false;
            ui.HideChoices();
            Game.Flags.Set(choice.setFlag);
            Game.ConsumeInputThisFrame();

            if (!string.IsNullOrEmpty(choice.next) && dialogues.TryGetValue(choice.next, out var next)) Begin(next);
            else Advance();
        }

        private void Finish()
        {
            if (!string.IsNullOrEmpty(current.next) && dialogues.TryGetValue(current.next, out var next))
            {
                Begin(next);
                return;
            }

            IsOpen = false;
            ui.Hide();
            Game.ConsumeInputThisFrame();
            var callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }
    }
}
