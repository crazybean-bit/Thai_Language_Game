using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThaiMystery
{
    // Steps through dialogue from dialogue.json: reveals each line gradually with voice
    // blips (or plays its recorded clip), handles choices and sets flags.
    //
    // Words are learned by ear. A speaker's line the protagonist can make out (see
    // DialogueData.heard) plays its clip and teaches him its words; one he can't plays
    // muffled and too fast to follow, and teaches nothing.
    // Thai lines without a speaker are things he reads or recalls: words he has heard
    // play back as he remembers them, and each recall is practice.
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
        private bool blipping;

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
            awaitingChoice = false;

            string text = line.text ?? "";
            bool thai = WordBook.IsThai(text);
            bool spoken = !string.IsNullOrEmpty(line.speaker);
            bool clear = thai && spoken && CanMakeOut(line);
            var words = ThaiWords(text);
            // Before the line is drawn, so the words he just heard show clearly.
            if (clear) foreach (var w in words) Game.Words.Hear(w);

            ui.ShowLine(line, PortraitFor(line));
            voice.Stop();
            voice.SetSpeaker(line.speaker);
            if (!thai) blipping = true;
            else if (clear) blipping = !voice.PlayLine(current.id, lineIndex);
            else if (spoken) blipping = !voice.PlayGarbled(current.id, lineIndex);
            else
            {
                // Reading or remembering: no blips, just the words he knows, as he knows them.
                blipping = false;
                voice.PlayRecall(words);
                foreach (var w in words) Game.Words.Practice(w);
            }

            revealing = true;
            revealed = 0;
            revealTimer = 0f;
        }

        private bool CanMakeOut(LineData line)
        {
            string heard = !string.IsNullOrEmpty(line.heard) ? line.heard : current.heard;
            if (string.IsNullOrEmpty(heard)) return false;
            return heard == "always" || Game.Flags.Has(heard);
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

        private static List<string> ThaiWords(string text)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(text)) return words;
            foreach (var token in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (WordBook.IsThai(token)) words.Add(token);
            return words;
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
                if (blipping && (ui.IsThaiLine || revealed % 3 == 0)) voice.Blip(!ui.IsThaiLine);
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
            // Saying a word out loud is practice too.
            foreach (var w in ThaiWords(choice.text)) Game.Words.Practice(w);
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
