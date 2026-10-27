using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ThaiMystery
{
    // How well the protagonist knows a word. Each step needs a different kind of evidence,
    // so no single action (like replaying a word) can carry a word all the way:
    //   Noise       nobody has said it clearly to him: blurred, shifting letters, no voice
    //   Heard       someone said it slowly to him: it shows, and replays in their voice, muffled
    //   Understood  its group is confirmed in the journal: the voice is less foggy
    //   Familiar    he caught it in fast speech (a listening moment): the voice blends into his
    //   Owned       he used it himself, in a choice or a sentence: his own voice, solid ink
    public enum WordStage { Noise, Heard, Understood, Familiar, Owned }

    // Tracks every Thai word the player has heard, what they think it means, and how far
    // each word has come. Everything is saved with PlayerPrefs.
    public class WordBook
    {
        private const string SaveKey = "ThaiMystery.WordBook";

        public enum CatchResult { NotHeard, NotUnderstood, Caught, AlreadyCaught }

        [Serializable]
        private class SaveData
        {
            public List<string> discovered = new List<string>();
            public List<string> guessKeys = new List<string>();
            public List<string> guessValues = new List<string>();
            public List<string> confirmed = new List<string>();
            public List<string> caught = new List<string>();
            public List<string> used = new List<string>();
        }

        private readonly Dictionary<string, WordEntry> entries = new Dictionary<string, WordEntry>();
        private readonly List<string> discovered = new List<string>();
        private readonly Dictionary<string, string> guesses = new Dictionary<string, string>();
        private readonly HashSet<string> confirmed = new HashSet<string>();
        private readonly HashSet<string> caught = new HashSet<string>();
        private readonly HashSet<string> used = new HashSet<string>();

        public event Action Changed;
        public event Action<List<string>> GroupConfirmed;
        // A word reached Familiar or Owned.
        public event Action<string, WordStage> StageRaised;

        public IReadOnlyList<string> Discovered => discovered;
        public int TotalWords => entries.Count;
        public int ConfirmedCount => confirmed.Count;

        public WordBook(WordList list)
        {
            if (list?.words != null)
            {
                foreach (var w in list.words)
                {
                    if (string.IsNullOrEmpty(w.thai)) continue;
                    entries[w.thai.Trim()] = w;
                }
            }
            Load();
        }

        public static bool IsThai(string token)
        {
            foreach (char c in token)
                if (c >= '฀' && c <= '๿') return true;
            return false;
        }

        public WordEntry GetEntry(string thai) => entries.TryGetValue(thai, out var e) ? e : null;

        public string GetGuess(string thai) => guesses.TryGetValue(thai, out var g) ? g : null;

        public bool IsConfirmed(string thai) => confirmed.Contains(thai);

        public bool IsHeard(string thai) => discovered.Contains(thai);

        public WordStage StageOf(string thai)
        {
            if (!discovered.Contains(thai)) return WordStage.Noise;
            if (!confirmed.Contains(thai)) return WordStage.Heard;
            if (!caught.Contains(thai)) return WordStage.Understood;
            if (!used.Contains(thai)) return WordStage.Familiar;
            return WordStage.Owned;
        }

        // How muffled the teacher's voice is when he recalls the word: 1 = barely there.
        public float Fog(string thai)
        {
            switch (StageOf(thai))
            {
                case WordStage.Understood: return 0.45f;
                case WordStage.Familiar: return 0.15f;
                case WordStage.Owned: return 0f;
                default: return 1f;
            }
        }

        // Someone said the word slowly and clearly to him.
        public void Hear(string thai)
        {
            if (string.IsNullOrEmpty(thai) || discovered.Contains(thai)) return;
            if (!entries.ContainsKey(thai))
                Debug.LogWarning($"[ThaiMystery] '{thai}' appears in dialogue but is not in words.json");
            discovered.Add(thai);
            Save();
            Changed?.Invoke();
        }

        // He picked the word out of fast speech. Only counts once he knows what it means.
        public CatchResult Catch(string thai)
        {
            if (!discovered.Contains(thai)) return CatchResult.NotHeard;
            if (!confirmed.Contains(thai)) return CatchResult.NotUnderstood;
            if (!caught.Add(thai)) return CatchResult.AlreadyCaught;
            Raised(thai);
            return CatchResult.Caught;
        }

        // He said the word himself. Only counts once he knows what it means.
        public void Use(string thai)
        {
            if (!confirmed.Contains(thai) || !used.Add(thai)) return;
            Raised(thai);
        }

        private void Raised(string thai)
        {
            Save();
            Changed?.Invoke();
            var stage = StageOf(thai);
            if (stage >= WordStage.Familiar) StageRaised?.Invoke(thai, stage);
        }

        // A shut-off: words tied to their past slip back to Heard, unless he owns them.
        public List<string> FogPast()
        {
            var slipped = new List<string>();
            foreach (var w in discovered)
            {
                var entry = GetEntry(w);
                if (entry == null || !entry.past || StageOf(w) == WordStage.Owned) continue;
                bool lost = confirmed.Remove(w);
                lost |= caught.Remove(w);
                if (lost) slipped.Add(w);
            }
            if (slipped.Count > 0)
            {
                Save();
                Changed?.Invoke();
            }
            return slipped;
        }

        public void SetGuess(string thai, string guess)
        {
            if (IsConfirmed(thai)) return;
            guess = (guess ?? "").Trim();
            if (guess.Length == 0) guesses.Remove(thai);
            else guesses[thai] = guess;

            CheckGroup(thai);
            Save();
            Changed?.Invoke();
        }

        public bool IsGuessCorrect(string thai)
        {
            var entry = GetEntry(thai);
            var guess = GetGuess(thai);
            if (entry?.answers == null || string.IsNullOrEmpty(guess)) return false;
            var g = Normalize(guess);
            return entry.answers.Any(a => Normalize(a) == g);
        }

        // A group confirms only when every word in it has been heard and guessed right,
        // so the player can't brute force one word at a time.
        private void CheckGroup(string thai)
        {
            var entry = GetEntry(thai);
            if (entry == null || string.IsNullOrEmpty(entry.group)) return;

            var group = entries.Values.Where(e => e.group == entry.group).Select(e => e.thai.Trim()).ToList();
            if (group.All(w => discovered.Contains(w) && IsGuessCorrect(w)))
            {
                var newly = group.Where(w => !confirmed.Contains(w)).ToList();
                if (newly.Count == 0) return;
                foreach (var w in newly) confirmed.Add(w);
                GroupConfirmed?.Invoke(newly);
            }
        }

        public static string Normalize(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '?') sb.Append(c);
            }
            var r = sb.ToString().Trim();
            while (r.Contains("  ")) r = r.Replace("  ", " ");
            foreach (var prefix in new[] { "to ", "a ", "an ", "the " })
                if (r.StartsWith(prefix)) r = r.Substring(prefix.Length);
            return r;
        }

        private void Save()
        {
            var data = new SaveData
            {
                discovered = new List<string>(discovered),
                guessKeys = guesses.Keys.ToList(),
                guessValues = guesses.Values.ToList(),
                confirmed = confirmed.ToList(),
                caught = caught.ToList(),
                used = used.ToList()
            };
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            if (data == null) return;
            discovered.AddRange(data.discovered);
            for (int i = 0; i < data.guessKeys.Count && i < data.guessValues.Count; i++)
                guesses[data.guessKeys[i]] = data.guessValues[i];
            foreach (var c in data.confirmed) confirmed.Add(c);
            if (data.caught != null) foreach (var c in data.caught) caught.Add(c);
            if (data.used != null) foreach (var u in data.used) used.Add(u);
        }

        public static void ClearSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }
}
