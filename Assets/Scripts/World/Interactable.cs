using UnityEngine;

namespace ThaiMystery
{
    // Anything the player can inspect or talk to. The first rule in map.json whose
    // flags match decides which dialogue plays.
    public class Interactable : MonoBehaviour
    {
        private ObjectData data;
        private CharacterSprite character;

        public bool Solid => !data.passable;

        public void Init(ObjectData objectData, Vector2Int cell)
        {
            data = objectData;
            transform.position = Game.Map.CellToWorld(cell);

            if (data.character)
            {
                gameObject.AddComponent<SpriteRenderer>();
                character = gameObject.AddComponent<CharacterSprite>();
                character.Init(SpriteLibrary.GetCharacterSheet(data.sprite), Vector2Int.down);
            }
            else
            {
                var sr = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteLibrary.Get(data.sprite);
                // Objects on the back wall sit behind anything standing in front of them.
                sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10f) + 1;
            }

            Game.Map.Register(this, cell);
        }

        public void Interact(PlayerController player)
        {
            if (character != null)
                character.SetFacing(player.Cell - Game.Map.FromRowCol(data.x, data.y));

            string dialogue = PickDialogue();
            if (!string.IsNullOrEmpty(dialogue)) Game.Dialogue.Play(dialogue);
        }

        private string PickDialogue()
        {
            if (data.rules == null) return null;
            foreach (var rule in data.rules)
            {
                if (!string.IsNullOrEmpty(rule.requireFlag) && !Game.Flags.Has(rule.requireFlag)) continue;
                if (Game.Flags.Has(rule.blockFlag)) continue;
                return rule.dialogue;
            }
            return null;
        }
    }
}
