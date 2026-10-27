using UnityEngine;

namespace ThaiMystery
{
    // Anything the player can inspect or talk to. The first rule in map.json whose
    // flags and condition match decides which dialogue plays. Objects with showWhen
    // (a visitor, say) appear and disappear as the condition changes.
    public class Interactable : MonoBehaviour
    {
        private ObjectData data;
        private CharacterSprite character;

        public bool Solid => !data.passable && Visible;
        public bool Visible { get; private set; } = true;

        public void Init(ObjectData objectData, Vector2Int cell)
        {
            data = objectData;
            transform.position = Game.Map.CellToWorld(cell);

            if (data.character)
            {
                gameObject.AddComponent<SpriteRenderer>();
                character = gameObject.AddComponent<CharacterSprite>();
                character.Init(SpriteLibrary.GetCharacterSheet(data.sprite), Vector2Int.down,
                    SpriteLibrary.GetWalkCycle(data.sprite));
            }
            else
            {
                var sr = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteLibrary.Get(data.sprite);
                // Objects on the back wall sit behind anything standing in front of them.
                sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10f) + 1;
            }

            // Tall furniture like the bed blocks every tile it covers.
            for (int i = 0; i < Mathf.Max(1, data.height); i++)
                Game.Map.Register(this, cell + Vector2Int.up * i);

            if (!string.IsNullOrEmpty(data.showWhen))
            {
                Game.State.Changed += UpdateVisibility;
                UpdateVisibility();
            }
        }

        private void OnDestroy()
        {
            if (Game.State != null) Game.State.Changed -= UpdateVisibility;
        }

        private void UpdateVisibility()
        {
            Visible = Game.State.Check(data.showWhen);
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = Visible;
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
                if (!Game.State.Check(rule.when)) continue;
                return rule.dialogue;
            }
            return null;
        }
    }
}
