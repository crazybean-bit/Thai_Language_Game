using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThaiMystery
{
    // Steps through dialogue from dialogue.json: reveals each line gradually with voice
    // blips (or plays its recorded clip), handles choices, listening moments and sentence
    // tiles, applies effects to GameState and sets flags.
    //
    // Words are learned by ear. A speaker's line the protagonist can make out (see
    // DialogueData.heard) plays its clip and lets him hear its words; one he can't plays
    // muffled and too fast to follow. Thai lines without a speaker are things he reads or
    // recalls: words he has heard play back as he remembers them (see WordStage).
    //
    // Two things can cut in: a Mali shut-off when recognition crosses its threshold
    // (dialogue "mali_shutoff"), and a kind moment after a run of failures ("kind_moment").
    public class DialogueRunner : MonoBehaviour
    {
        private const float SecondsPerWord = 0.09f;
        private const float SecondsPerLetter = 0.022f;
        private const string ShutOffDialogue = "mali_shutoff";
        private const string KindMomentDialogue = "kind_moment";
        private const string Me = "Me";

        private enum Mode { Revealing, Waiting, Choosing, Tiles }

        private readonly Dictionary<string, DialogueData> dialogues = new Dictionary<string, DialogueData>();
        private readonly System.Random shuffle = new System.Random();
        private DialogueUI ui;
        private VoicePlayer voice;

        private DialogueData current;
        private int lineIndex;
        private Mode mode;
        private Action onFinished;

        // Options on screen: the line's choices, or the words for a listening question.
        private readonly List<ChoiceData> options = new List<ChoiceData>();
        private Action<int> onOption;
        private int optionIndex;

        // While his own sentence is on screen, what happens when the player continues.
        private Action afterSaying;

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

        // Moves to another dialogue, unless a shut-off cuts in first.
        private void ContinueTo(string id)
        {
            if (CutIn()) return;
            if (!string.IsNullOrEmpty(id) && dialogues.TryGetValue(id, out var next)) Begin(next);
            else NextLine();
        }

        // Mali's shut-off interrupts whatever she was saying.
        private bool CutIn()
        {
            if (!Game.State.ShutOffPending || current.id == ShutOffDialogue) return false;
            if (!dialogues.TryGetValue(ShutOffDialogue, out var shutOff)) return false;
            Game.State.ShutOff();
            Begin(shutOff);
            return true;
        }

        private LineData Line => current.lines[lineIndex];

        private void ShowLine()
        {
            while (lineIndex < current.lines.Length && !Game.State.Check(Line.when)) lineIndex++;
            if (lineIndex >= current.lines.Length)
            {
                Finish();
                return;
            }

            var line = Line;
            Game.Flags.Set(line.setFlag);
            Game.State.Apply(line.warmth, line.recognition, line.confidence, line.money);
            if (line.fogPast)
            {
                var slipped = Game.Words.FogPast();
                if (slipped.Count > 0) Game.UI.ShowToast($"<color=#C9B79C>The words slip away:</color>  {string.Join("   ", slipped)}");
            }

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
            }
            StartReveal();
        }

        private void StartReveal()
        {
            mode = Mode.Revealing;
            afterSaying = null;
            revealed = 0;
            revealTimer = 0f;
        }

        // Shows a sentence he says himself and plays it in his voice.
        private void Say(string text, bool hesitant, bool confident, Action then)
        {
            ui.ShowLine(new LineData { speaker = Me, text = text, portrait = "none" }, null);
            voice.SetSpeaker(Me);
            voice.PlaySentence(ThaiWords(text), hesitant, confident);
            blipping = false;
            StartReveal();
            afterSaying = then;
        }

        private bool CanMakeOut(LineData line)
        {
            string heard = !string.IsNullOrEmpty(line.heard) ? line.heard : current.heard;
            if (string.IsNullOrEmpty(heard)) return false;
            return heard == "always" || Game.State.Check(heard);
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
            ui.SetRevealed(ui.RevealUnits);
            mode = Mode.Waiting;
            if (afterSaying != null)
            {
                ui.SetContinueVisible(true);
                return;
            }

            var line = Line;
            if (!string.IsNullOrEmpty(line.catchWord) && line.catchOptions != null && line.catchOptions.Length > 0)
                AskWhatHeCaught(line);
            else if (line.tiles != null && !string.IsNullOrEmpty(line.tiles.answer))
                ShowTiles(line.tiles);
            else if (line.choices != null && line.choices.Length > 0)
                OfferChoices(line.choices);
            else
                ui.SetContinueVisible(true);
        }

        private void ShowOptions(List<ChoiceData> list, Action<int> chosen)
        {
            options.Clear();
            options.AddRange(list);
            onOption = chosen;
            optionIndex = 0;
            mode = Mode.Choosing;
            ui.ShowChoices(options.ToArray(), PickOption);
            ui.HighlightChoice(optionIndex);
        }

        private void PickOption(int index)
        {
            if (mode != Mode.Choosing) return;
            mode = Mode.Waiting;
            ui.HideChoices();
            Game.ConsumeInputThisFrame();
            onOption?.Invoke(index);
        }

        // ---------------------------------------------------------------- choices

        private void OfferChoices(ChoiceData[] choices)
        {
            var offered = new List<ChoiceData>();
            foreach (var c in choices)
                if (Game.State.Check(c.when)) offered.Add(c);
            if (offered.Count == 0)
            {
                ui.SetContinueVisible(true);
                return;
            }
            ShowOptions(offered, i => Choose(offered[i]));
        }

        private void Choose(ChoiceData choice)
        {
            Game.Flags.Set(choice.setFlag);
            Game.State.Apply(choice.warmth, choice.recognition, choice.confidence, choice.money);
            var words = ThaiWords(choice.text);
            if (words.Count == 0)
            {
                ContinueTo(choice.next);
                return;
            }
            // Saying a word he understands counts as using it.
            foreach (var w in words) Game.Words.Use(w);
            Say(choice.text, Game.State.LowOnConfidence, Game.State.HighOnConfidence, () => ContinueTo(choice.next));
        }

        // ---------------------------------------------------------------- listening moments

        private void AskWhatHeCaught(LineData line)
        {
            var words = new List<string>(line.catchOptions);
            if (!words.Contains(line.catchWord)) words.Add(line.catchWord);
            for (int i = words.Count - 1; i > 0; i--)
            {
                int j = shuffle.Next(i + 1);
                (words[i], words[j]) = (words[j], words[i]);
            }
            var asked = words.ConvertAll(w => new ChoiceData { text = w });
            Game.UI.ShowToast("<color=#C9B79C>What did I catch?</color>");
            ShowOptions(asked, i => Caught(line.catchWord, words[i]));
        }

        private void Caught(string target, string picked)
        {
            if (picked != target)
            {
                Game.State.Failed(4);
                Game.UI.ShowToast("<color=#C9B79C>No... that wasn't it.</color>");
            }
            else
            {
                switch (Game.Words.Catch(target))
                {
                    case WordBook.CatchResult.NotUnderstood:
                        Game.State.Succeeded(2);
                        Game.UI.ShowToast($"I caught {target}<color=#C9B79C>, but I still don't know what it means.</color>");
                        break;
                    case WordBook.CatchResult.Caught:
                        Game.State.Succeeded(5);
                        break;
                    default:
                        Game.State.Succeeded(2);
                        Game.UI.ShowToast($"<color=#C9B79C>I caught</color>  {target}");
                        break;
                }
            }
            NextLine();
        }

        // ---------------------------------------------------------------- sentence tiles

        private void ShowTiles(TileData tiles)
        {
            mode = Mode.Tiles;
            ui.ShowTiles(tiles, Game.State.LowOnConfidence, Game.State.HighOnConfidence, said => Said(tiles, said));
        }

        private void Said(TileData tiles, List<string> said)
        {
            if (mode != Mode.Tiles) return;
            mode = Mode.Waiting;
            ui.HideTiles();
            Game.ConsumeInputThisFrame();

            string sentence = string.Join(" ", said);
            bool right = Matches(sentence, tiles.answer);
            if (!right && tiles.accept != null)
                foreach (var a in tiles.accept)
                    right |= Matches(sentence, a);

            if (right)
            {
                foreach (var w in said) Game.Words.Use(w);
                Game.State.Succeeded(8);
                Game.State.Apply(tiles.warmth, 0, 0, 0);
                Say(sentence, Game.State.LowOnConfidence, Game.State.HighOnConfidence, () => ContinueTo(tiles.correct));
                return;
            }

            Game.State.Failed(8);
            Say(sentence, true, false, () =>
            {
                if (tiles.reactions != null)
                    foreach (var r in tiles.reactions)
                        if (Matches(sentence, r.text)) { ContinueTo(r.next); return; }
                if (!string.IsNullOrEmpty(tiles.wrong)) ContinueTo(tiles.wrong);
                else if (tiles.retry)
                {
                    Game.UI.ShowToast("<color=#C9B79C>That's not it. Try again.</color>");
                    ShowTiles(tiles);
                }
                else NextLine();
            });
        }

        private static bool Matches(string said, string expected) =>
            string.Join(" ", ThaiWords(said)) == string.Join(" ", ThaiWords(expected ?? ""));

        // ---------------------------------------------------------------- flow

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

            if (mode == Mode.Revealing) TickReveal();
            if (Game.InputConsumed || Game.UI.ModalOpen) return;

            if (GameInput.Replay()) voice.Replay();

            switch (mode)
            {
                case Mode.Revealing:
                    if (GameInput.Confirm())
                    {
                        Game.ConsumeInputThisFrame();
                        FinishReveal();
                    }
                    break;
                case Mode.Choosing:
                    int count = options.Count;
                    if (GameInput.UpPressed()) optionIndex = (optionIndex + count - 1) % count;
                    else if (GameInput.DownPressed()) optionIndex = (optionIndex + 1) % count;
                    else if (GameInput.Confirm()) { PickOption(optionIndex); return; }
                    ui.HighlightChoice(optionIndex);
                    break;
                case Mode.Tiles:
                    ui.HandleTileKeys();
                    break;
                default:
                    if (GameInput.Confirm()) Continue();
                    break;
            }
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
            if (mode == Mode.Revealing) FinishReveal();
            else if (mode == Mode.Waiting) Continue();
        }

        private void Continue()
        {
            Game.ConsumeInputThisFrame();
            if (afterSaying != null)
            {
                var then = afterSaying;
                afterSaying = null;
                then();
                return;
            }
            NextLine();
        }

        private void NextLine()
        {
            if (CutIn()) return;
            lineIndex++;
            if (lineIndex < current.lines.Length) ShowLine();
            else Finish();
        }

        private void Finish()
        {
            if (CutIn()) return;
            if (!string.IsNullOrEmpty(current.next) && dialogues.TryGetValue(current.next, out var next))
            {
                Begin(next);
                return;
            }
            if (Game.State.KindMomentPending && current.id != KindMomentDialogue &&
                dialogues.TryGetValue(KindMomentDialogue, out var kind))
            {
                Game.State.KindMomentPlayed();
                Begin(kind);
                return;
            }

            IsOpen = false;
            mode = Mode.Waiting;
            voice.Stop();
            ui.Hide();
            Game.ConsumeInputThisFrame();
            var callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }
    }
}
