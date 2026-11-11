using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ThaiMystery
{
    // Ends one day and starts the next. A line with "endDay" sets it off once its dialogue
    // closes:
    //
    //   1. Fade to black: the end-of-day card lists the words he first heard today.
    //   2. Night forgetting: a few shaky words fade (more when his confidence is low, none
    //      when it's high), and the card says which. See WordBook.FadeOvernight.
    //   3. The night's dialogue, "night_<day>" in dialogue.json, if there is one: a memory
    //      flash ("flash": true washes the screen pale) or something he wakes up to.
    //   4. Morning: the day number goes up, he wakes beside the bed, and "morning_<day>"
    //      plays if there is one. Objects with "showWhen": "day==2" and the like appear.
    //
    // A line with "endAct" shows the act's closing card instead ("act_<n>_end" lines are
    // ordinary dialogue; the card is just a pause on black).
    public class DayCycle : MonoBehaviour
    {
        // Where he wakes up each morning.
        private const string WakeMap = "house";
        private const int WakeX = 2;
        private const int WakeY = 4;
        private const string WakeFacing = "left";

        // How many words fade overnight, by how the day left him feeling.
        private const int FadeWhenNervous = 3;
        private const int FadeWhenSteady = 1;
        private const int FadeWhenConfident = 0;
        private const int ListAtMost = 14;

        private bool running;

        public bool Running => running;

        public void EndDay()
        {
            if (!running) StartCoroutine(Night());
        }

        public void EndAct()
        {
            if (!running) StartCoroutine(ActCard());
        }

        private IEnumerator Night()
        {
            running = true;
            Game.State.NightStarted();
            int day = Game.State.Day;
            var ui = Game.UI;

            yield return ui.FadeOut(1.4f);
            ui.Night.Show();
            ui.ClearFade();

            var heard = Game.Words.HeardOn(day);
            var faded = Game.Words.FadeOvernight(FadeCount());
            ui.Night.ShowCard($"The end of day {day}", Summary(heard, faded));
            yield return WaitForConfirm();
            ui.Night.HideCard();

            string night = "night_" + day;
            if (Game.Dialogue.Has(night))
            {
                bool done = false;
                Game.Dialogue.Play(night, () => done = true);
                while (!done) yield return null;
            }

            yield return ui.FadeOut(0.6f);
            Game.State.NextDay();
            Game.Player.Dress(PlayerController.LookForToday());
            ui.Night.Hide();
            if (Game.World.Load(WakeMap) != null)
                Game.Player.PlaceAt(Game.Map.FromRowCol(WakeX, WakeY), CharacterSprite.ParseFacing(WakeFacing));
            yield return ui.FadeIn(1.6f);
            running = false;

            string morning = "morning_" + Game.State.Day;
            if (Game.Dialogue.Has(morning)) Game.Dialogue.Play(morning);
        }

        private static int FadeCount()
        {
            if (Game.State.HighOnConfidence) return FadeWhenConfident;
            if (Game.State.LowOnConfidence) return FadeWhenNervous;
            return FadeWhenSteady;
        }

        private static string Summary(List<string> heard, List<string> faded)
        {
            const string soft = "<color=#9A8C7E>";
            var text = new System.Text.StringBuilder();
            if (heard.Count == 0)
                text.Append("No new words today.");
            else
            {
                text.Append(heard.Count == 1 ? "One new word today:\n" : $"{heard.Count} new words today:\n");
                int shown = Mathf.Min(heard.Count, ListAtMost);
                text.Append("<size=34>").Append(string.Join("   ", heard.GetRange(0, shown))).Append("</size>");
                if (heard.Count > shown) text.Append($"   {soft}and {heard.Count - shown} more</color>");
            }
            text.Append("\n\n");
            if (faded.Count > 0)
            {
                text.Append(Game.State.LowOnConfidence
                    ? "It was a hard day. As I drift off, the shakiest words blur:\n"
                    : "As I drift off, a word I barely knew blurs:\n");
                text.Append("<size=34>").Append(string.Join("   ", faded)).Append("</size>\n");
                text.Append($"{soft}<size=20>They're not gone. Hear one, read it, or click it in the journal to bring it back.</size></color>");
            }
            else
                text.Append($"{soft}I hold on to every word tonight.</color>");
            return text.ToString();
        }

        private IEnumerator ActCard()
        {
            running = true;
            Game.State.EndActShown();
            var ui = Game.UI;
            yield return ui.FadeOut(2f);
            ui.Night.Show();
            ui.ClearFade();
            ui.Night.ShowCard("End of Act 1",
                "The town is waiting past the end of the lane.\n\n<color=#9A8C7E>Act 2: The town</color>");
            yield return WaitForConfirm();
            yield return ui.FadeOut(0.4f);
            ui.Night.Hide();
            yield return ui.FadeIn(1.2f);
            running = false;
        }

        private static IEnumerator WaitForConfirm()
        {
            // Let the key that closed the last line go first.
            yield return new WaitForSeconds(0.6f);
            while (true)
            {
                if (!Game.InputConsumed && !Game.UI.ModalOpen && GameInput.Confirm())
                {
                    Game.ConsumeInputThisFrame();
                    yield break;
                }
                yield return null;
            }
        }
    }
}
