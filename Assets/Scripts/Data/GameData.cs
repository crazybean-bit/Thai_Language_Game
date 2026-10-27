using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThaiMystery
{
    // Plain data classes loaded from the JSON files in Assets/Resources/Data.
    // Edit those JSON files to change words, dialogue and the map without touching code.

    [Serializable]
    public class WordEntry
    {
        public string thai;            // the Thai word as it appears in dialogue
        public string[] answers;       // accepted English meanings (case/punctuation insensitive)
        public string romanization;    // revealed once the word is confirmed
        public string group;           // all words in a group must be right before they confirm
        public string note;            // short learning note shown once confirmed
        public bool past;              // tied to his and Mali's past: fogs over when she shuts off
        public string stutter;         // how he stumbles into it when nervous, e.g. "ขอ... ขอบคุณ"
    }

    [Serializable]
    public class WordList
    {
        public WordEntry[] words;
    }

    [Serializable]
    public class ChoiceData
    {
        public string text;            // space separated Thai words he says, or an action in English
        public string next;            // dialogue id to jump to
        public string setFlag;
        public string when;            // only offered when this condition holds (see GameState.Check)
        public int warmth;             // effects when chosen
        public int recognition;
        public int confidence;
        public int money;
    }

    // He answers by building a sentence from word tiles: words he has heard, plus a few wrong ones.
    [Serializable]
    public class TileData
    {
        public string prompt;          // what he's trying to say, in English, e.g. "Thank her"
        public string answer;          // the sentence that counts as right, space separated
        public string[] accept;        // other sentences that also count as right
        public string[] words;         // extra tiles to choose from (the answer's words are always there)
        public string[] bonus;         // extra tiles offered only when he's confident
        public string correct;         // dialogue to play when he gets it right (else the dialogue continues)
        public string wrong;           // dialogue to play when he gets it wrong and no reaction matches
        public TileReaction[] reactions; // specific wrong sentences that get their own reaction
        public bool retry;             // after a wrong answer with no reaction, try again
        public int warmth;             // Mali's reaction to getting it right
    }

    [Serializable]
    public class TileReaction
    {
        public string text;            // the sentence he said
        public string next;            // dialogue to play
    }

    [Serializable]
    public class LineData
    {
        public string speaker;         // shown in the nameplate; empty hides it
        public string portrait;        // overrides the dialogue's portrait; "none" hides it
        public string text;            // Thai words separated by spaces, or plain English for thoughts
        public string setFlag;         // set when the line is shown
        public string heard;           // overrides the dialogue's "heard" for this line
        public string when;            // the line is skipped unless this condition holds
        public int warmth;             // effects when the line is shown
        public int recognition;
        public int confidence;
        public int money;
        public bool fogPast;           // words tied to the past fall back to Heard (a shut-off)
        public ChoiceData[] choices;   // optional: shown after this line
        // A listening moment: after the line, he's asked which word he caught. Catching a word
        // he understands makes it Familiar.
        public string catchWord;
        public string[] catchOptions;  // what he can answer, including catchWord
        public TileData tiles;         // optional: he answers by building a sentence
    }

    [Serializable]
    public class DialogueData
    {
        public string id;
        public string portrait;        // sprite shown beside the box for lines with a speaker
        public LineData[] lines;
        public string next;            // optional dialogue to continue into
        // Whether the protagonist can make out what a speaker says: "always", a flag name
        // (clear once that flag is set) or empty (just noise). Clear lines play their voice
        // clip and teach him every word in them; noise shows only the words he already knows.
        // Lines without a speaker are his own reading and recall, and ignore this.
        public string heard;
    }

    [Serializable]
    public class DialogueList
    {
        public DialogueData[] dialogues;
    }

    [Serializable]
    public class InteractionRule
    {
        public string dialogue;
        public string requireFlag;     // rule only applies when this flag is set
        public string blockFlag;       // rule is skipped when this flag is set
        public string when;            // rule is skipped unless this condition holds
    }

    [Serializable]
    public class ObjectData
    {
        public string id;
        public string sprite;          // file name in Resources/Sprites (no extension)
        public int x;                  // column, counted from the left
        public int y;                  // row, counted from the top (same as the map rows)
        public int height;             // rows it blocks, counting up from its tile (0 or 1 = just its tile)
        public bool character;         // uses a 3x4 RPG Maker style sheet and turns to face the player
        public bool passable;          // the player can walk over it
        public string showWhen;        // only there while this condition holds, e.g. a visitor
        public InteractionRule[] rules;
    }

    [Serializable]
    public class LightData
    {
        public int x;                  // column, like objects
        public int y;                  // row
        public float radius = 3f;      // in tiles
        public float intensity = 1f;
        public string color = "#FFB060";
        public bool flicker;           // candle/lamp style flicker
    }

    [Serializable]
    public class LightingData
    {
        public float darkness = 0.6f;  // 0 = no darkness, 1 = pitch black away from lights
        public string darkColor = "#0B0814";
        public LightData[] lights;
    }

    [Serializable]
    public class MapData
    {
        public string[] rows;          // '#' outer wall, '-' upper wall, '=' lower wall, '.' floor, ',' rug
        public int playerX;
        public int playerY;
        public string playerFacing;    // up, down, left, right
        public string introDialogue;
        public ObjectData[] objects;
        public LightingData lighting;
    }

    public static class GameDataLoader
    {
        public static T Load<T>(string resourcePath) where T : class
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError($"[ThaiMystery] Missing data file Resources/{resourcePath}.json");
                return null;
            }
            return JsonUtility.FromJson<T>(asset.text);
        }
    }

    public class GameFlags
    {
        private readonly HashSet<string> flags = new HashSet<string>();

        public event Action Changed;

        public bool Has(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public void Set(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && flags.Add(flag)) Changed?.Invoke();
        }
    }
}
