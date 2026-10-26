using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThaiMystery
{
    // Steps through dialogue from dialogue.json: reveals each line gradually with voice
    // blips (or plays its recorded clip), handles choices, sets flags and records every
    // Thai word the player sees in the journal.
    public class DialogueRunner : MonoBehaviour
    {
        private const float SecondsPerWord = 0.09f;
        private const float SecondsPerLetter = 0.022f;

        private readonly Dictionary<string, DialogueData> dialogues = new Dictionary<string, DialogueData>();
        private DialogueUI ui;
        private VoicePlayer voice;

        private DialogueData current;
        private int lineIndex;
        private bool awaitingChoice;
        private int choiceIndex;
        private Action onFinished;

        private bool revealing;
        private int revealed;
        private float revealTimer;
        private bool hasVoiceClip;

        public bool IsOpen { get; private set; }

        public void Init(DialogueList list, DialogueUI dialogueUI, VoicePlayer voicePlayer)
        {
            ui = dialogueUI;
            voice = voicePlayer;
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

        private LineData Line => current.lines[lineIndex];

        private void ShowLine()
        {
            var line = Line;
            Game.Flags.Set(line.setFlag);
            Discover(line.text);
            awaitingChoice = false;

            ui.ShowLine(line, PortraitFor(line));
            voice.Stop();
            voice.SetSpeaker(line.speaker);
            hasVoiceClip = WordBook.IsThai(line.text ?? "") && voice.PlayLine(current.id, lineIndex);

            revealing = true;
            revealed = 0;
            revealTimer = 0f;
        }

        private string PortraitFor(LineData line)
        {
            string p = !string.IsNullOrEmpty(line.portrait)
                ? line.portrait
                : (string.IsNullOrEmpty(line.speaker) ? null : current.portrait);
            return p == "none" ? null : p;
        }

        private void FinishReveal()
        {
            revealing = false;
            ui.SetRevealed(ui.RevealUnits);

            var line = Line;
            if (line.choices != null && line.choices.Length > 0)
            {
                foreach (var c in line.choices) Discover(c.text);
                awaitingChoice = true;
                choiceIndex = 0;
                ui.ShowChoices(line.choices, Choose);
                ui.HighlightChoice(choiceIndex);
            }
            else
            {
                ui.SetContinueVisible(true);
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
            if (!IsOpen) return;

            if (revealing) TickReveal();
            if (Game.InputConsumed || Game.UI.ModalOpen) return;

            if (GameInput.Replay()) voice.Replay();

            if (revealing)
            {
                if (GameInput.Confirm())
                {
                    Game.ConsumeInputThisFrame();
                    FinishReveal();
                }
                return;
            }

            if (awaitingChoice)
            {
                int count = Line.choices.Length;
                if (GameInput.UpPressed()) choiceIndex = (choiceIndex + count - 1) % count;
                else if (GameInput.DownPressed()) choiceIndex = (choiceIndex + 1) % count;
                else if (GameInput.Confirm()) { Choose(choiceIndex); return; }
                ui.HighlightChoice(choiceIndex);
                return;
            }

            if (GameInput.Confirm()) Advance();
        }

        private void TickReveal()
        {
            float step = ui.IsThaiLine ? SecondsPerWord : SecondsPerLetter;
            revealTimer += Time.deltaTime;
            while (revealTimer >= step && revealed < ui.RevealUnits)
            {
                revealTimer -= step;
                revealed++;
                ui.SetRevealed(revealed);
                // Thai lines blip per word; thoughts blip softly every few letters.
                if (!hasVoiceClip && (ui.IsThaiLine || revealed % 3 == 0)) voice.Blip(!ui.IsThaiLine);
            }
            if (revealed >= ui.RevealUnits) FinishReveal();
        }

        public void OnBoxClicked()
        {
            if (!IsOpen) return;
            if (revealing) FinishReveal();
            else if (!awaitingChoice) Advance();
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
            var choice = Line.choices[index];
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
            revealing = false;
            voice.Stop();
            ui.Hide();
            Game.ConsumeInputThisFrame();
            var callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }
    }
}
