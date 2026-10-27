using System;
using System.Globalization;
using UnityEngine;

namespace ThaiMystery
{
    // The story's hidden numbers, read by dialogue conditions ("when" in the JSON) and changed
    // by line and choice effects.
    //
    //   warmth       how Mali feels about him now. Tiers: stranger < 25 <= housemate < 60 <= close
    //   recognition  how much of their past is surfacing in her. Crossing ShutOffAt triggers a
    //                shut-off: she goes cold mid-conversation and it falls back again
    //   confidence   how sure of himself he is, 0 to 100. Below 35 he stutters and words fog
    //                over in the tiles; from 70 he's fluent and gets extra tiles
    //   money        baht earned from jobs
    public class GameState
    {
        public const int Housemate = 25;
        public const int Close = 60;
        public const int ShutOffAt = 30;
        public const int ShutOffDrop = 20;
        public const int LowConfidence = 35;
        public const int HighConfidence = 70;
        public const int FailsBeforeKindMoment = 3;

        public int Warmth { get; private set; } = 10;
        public int Recognition { get; private set; }
        public int Confidence { get; private set; } = 50;
        public int Money { get; private set; }
        public int ShutOffs { get; private set; }

        // Set when recognition crosses ShutOffAt; the dialogue runner cuts to the shut-off scene.
        public bool ShutOffPending { get; private set; }
        // Set after a run of failures; the dialogue runner plays a kind moment next.
        public bool KindMomentPending { get; private set; }
        private int failStreak;

        public bool LowOnConfidence => Confidence < LowConfidence;
        public bool HighOnConfidence => Confidence >= HighConfidence;

        public event Action Changed;
        public event Action<int> MoneyChanged;

        public void Apply(int warmth, int recognition, int confidence, int money)
        {
            if (warmth == 0 && recognition == 0 && confidence == 0 && money == 0) return;
            Warmth = Mathf.Clamp(Warmth + warmth, 0, 100);
            int before = Recognition;
            Recognition = Mathf.Clamp(Recognition + recognition, 0, 100);
            if (before < ShutOffAt && Recognition >= ShutOffAt) ShutOffPending = true;
            Confidence = Mathf.Clamp(Confidence + confidence, 0, 100);
            if (money != 0)
            {
                Money = Mathf.Max(0, Money + money);
                MoneyChanged?.Invoke(money);
            }
            Changed?.Invoke();
        }

        // The vow strikes: recognition falls back and she goes cold.
        public void ShutOff()
        {
            ShutOffPending = false;
            ShutOffs++;
            Recognition = Mathf.Max(0, Recognition - ShutOffDrop);
            Changed?.Invoke();
        }

        // Things he tried to say or catch. A run of failures earns a kind moment, so low
        // confidence never locks the player out.
        public void Succeeded(int confidence)
        {
            failStreak = 0;
            Apply(0, 0, confidence, 0);
        }

        public void Failed(int confidence)
        {
            failStreak++;
            if (failStreak >= FailsBeforeKindMoment)
            {
                failStreak = 0;
                KindMomentPending = true;
            }
            Apply(0, 0, -confidence, 0);
        }

        public void KindMomentPlayed() => KindMomentPending = false;

        public void NotifyFlagsChanged() => Changed?.Invoke();

        // Conditions look like "met_mali", "!warned_door", "warmth>=25" or
        // "warmth>=housemate & !asked_photo". Empty is always true.
        public bool Check(string when)
        {
            if (string.IsNullOrWhiteSpace(when)) return true;
            foreach (var raw in when.Split('&'))
            {
                var clause = raw.Trim();
                if (clause.Length == 0) continue;
                if (!CheckClause(clause)) return false;
            }
            return true;
        }

        private bool CheckClause(string clause)
        {
            foreach (var op in new[] { ">=", "<=", "==", "!=", ">", "<" })
            {
                int i = clause.IndexOf(op, StringComparison.Ordinal);
                if (i <= 0) continue;
                int left = Stat(clause.Substring(0, i).Trim());
                int right = Value(clause.Substring(i + op.Length).Trim());
                switch (op)
                {
                    case ">=": return left >= right;
                    case "<=": return left <= right;
                    case "==": return left == right;
                    case "!=": return left != right;
                    case ">": return left > right;
                    default: return left < right;
                }
            }
            if (clause.StartsWith("!")) return !Game.Flags.Has(clause.Substring(1).Trim());
            return Game.Flags.Has(clause);
        }

        private int Stat(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "warmth": return Warmth;
                case "recognition": return Recognition;
                case "confidence": return Confidence;
                case "money": return Money;
                case "shutoffs": return ShutOffs;
            }
            Debug.LogWarning($"[ThaiMystery] Unknown value '{name}' in a dialogue condition");
            return 0;
        }

        private static int Value(string text)
        {
            switch (text.ToLowerInvariant())
            {
                case "stranger": return 0;
                case "housemate": return Housemate;
                case "close": return Close;
                case "low": return LowConfidence;
                case "high": return HighConfidence;
                case "shutoff": return ShutOffAt;
            }
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return n;
            Debug.LogWarning($"[ThaiMystery] Can't read '{text}' in a dialogue condition");
            return 0;
        }

        public string Describe() =>
            $"Warmth {Warmth} ({WarmthTier})   Recognition {Recognition}/{ShutOffAt}   Confidence {Confidence}   " +
            $"Money {Money}   Shut-offs {ShutOffs}";

        public string WarmthTier => Warmth >= Close ? "close" : Warmth >= Housemate ? "housemate" : "stranger";
    }
}
