using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ThaiMystery
{
    // How well the protagonist knows a word by ear.
    //   Unheard  nobody has pronounced it to him yet: it shows as a jumble and has no voice
    //   Foggy    he has heard Mali say it; recalling it plays her voice, muffled and distant
    //   Learned  heard and practised enough that he recalls it in his own voice
    public enum WordStage { Unheard, Foggy, Learned }

    // Tracks every Thai word the player has heard, what they think it means,
    // and which words are confirmed. Guesses are saved with PlayerPrefs.
    public class WordBook
    {
        private const string SaveKey = "ThaiMystery.WordBook";

        // Hearing Mali say a word or recalling it yourself each count once.
        public const int ExposuresToLearn = 4;

        [Serializable]
        private class SaveData
        {
            public List<string> discovered = new List<string>();
            public List<string> guessKeys = new List<string>();
            public List<string> guessValues = new List<string>();
            public List<string> confirmed = new List<string>();
            public List<string> exposureKeys = new List<string>();
            public List<int> exposureValues = new List<int>();
        }

        private readonly Dictionary<string, WordEntry> entries = new Dictionary<string, WordEntry>();
        private readonly List<string> discovered = new List<string>();
        private readonly Dictionary<string, string> guesses = new Dictionary<string, string>();
        private readonly HashSet<string> confirmed = new HashSet<string>();
        private readonly Dictionary<string, int> exposures = new Dictionary<string, int>();

        public event Action Changed;
        public event Action<List<string>> GroupConfirmed;
        public event Action<string> WordLearned;

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

        public int Exposures(string thai) => exposures.TryGetValue(thai, out var n) ? n : 0;

        public WordStage StageOf(string thai)
        {
            int n = Exposures(thai);
            return n <= 0 ? WordStage.Unheard : n < ExposuresToLearn ? WordStage.Foggy : WordStage.Learned;
        }

        public bool IsHeard(string thai) => Exposures(thai) > 0;

        // 1 the first time he recalls a word, fading towards 0 as it becomes his own.
        public float Fog(string thai)
        {
            int n = Exposures(thai);
            if (n <= 0) return 1f;
            if (n >= ExposuresToLearn) return 0f;
            return 1f - (float)(n - 1) / (ExposuresToLearn - 1);
        }

        // Someone pronounced the word to him.
        public void Hear(string thai) => AddExposure(thai);

        // He recalled or said the word himself. Only counts once he knows how it sounds.
        public void Practice(string thai)
        {
            if (IsHeard(thai)) AddExposure(thai);
        }

        private void AddExposure(string thai)
        {
            if (string.IsNullOrEmpty(thai)) return;
            var before = StageOf(thai);
            exposures[thai] = Math.Min(Exposures(thai) + 1, 99);
            var after = StageOf(thai);
            if (!discovered.Contains(thai))
            {
                Discover(thai);
                return;
            }
            Save();
            if (after == before) return;
            Changed?.Invoke();
            if (after == WordStage.Learned) WordLearned?.Invoke(thai);
        }

        private void Discover(string thai)
        {
            if (string.IsNullOrEmpty(thai) || discovered.Contains(thai)) return;
            if (!entries.ContainsKey(thai))
                Debug.LogWarning($"[ThaiMystery] '{thai}' appears in dialogue but is not in words.json");
            discovered.Add(thai);
            Save();
            Changed?.Invoke();
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
                exposureKeys = exposures.Keys.ToList(),
                exposureValues = exposures.Values.ToList()
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
            for (int i = 0; i < data.exposureKeys.Count && i < data.exposureValues.Count; i++)
                exposures[data.exposureKeys[i]] = data.exposureValues[i];
            // Saves from before words had to be heard: count what was seen as heard once.
            foreach (var d in discovered)
                if (!exposures.ContainsKey(d)) exposures[d] = 1;
        }

        public static void ClearSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }
}
