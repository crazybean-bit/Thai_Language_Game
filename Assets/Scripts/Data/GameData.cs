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
    }

    [Serializable]
    public class WordList
    {
        public WordEntry[] words;
    }

    [Serializable]
    public class ChoiceData
    {
        public string text;            // space separated Thai words, like a line
        public string next;            // dialogue id to jump to
        public string setFlag;
    }

    [Serializable]
    public class LineData
    {
        public string speaker;         // shown in the nameplate; empty hides it
        public string text;            // Thai words separated by spaces, or plain English for thoughts
        public string setFlag;         // set when the line is shown
        public ChoiceData[] choices;   // optional: shown after this line
    }

    [Serializable]
    public class DialogueData
    {
        public string id;
        public LineData[] lines;
        public string next;            // optional dialogue to continue into
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
    }

    [Serializable]
    public class ObjectData
    {
        public string id;
        public string sprite;          // file name in Resources/Sprites (no extension)
        public int x;                  // column, counted from the left
        public int y;                  // row, counted from the top (same as the map rows)
        public bool character;         // uses a 3x4 RPG Maker style sheet and turns to face the player
        public bool passable;          // the player can walk over it
        public InteractionRule[] rules;
    }

    [Serializable]
    public class MapData
    {
        public string[] rows;          // '#' roof/void wall, '=' wall face, '.' floor, ',' rug
        public int playerX;
        public int playerY;
        public string playerFacing;    // up, down, left, right
        public string introDialogue;
        public ObjectData[] objects;
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

        public bool Has(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public void Set(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) flags.Add(flag);
        }
    }
}
