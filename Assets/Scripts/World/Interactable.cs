using UnityEngine;

namespace ThaiMystery
{
    // Anything the player can inspect or talk to. The first rule in the map whose
    // flags and condition match decides which dialogue plays, and can move the player
    // to another map (a door). Objects with showWhen (a visitor, say) appear and
    // disappear as the condition changes.
    public class Interactable : MonoBehaviour
    {
        private ObjectData data;
        private CharacterSprite character;

        public bool Solid => !data.passable && Visible;
        public bool Visible { get; private set; } = true;

        public void Init(ObjectData objectData, Vector2Int cell)
        {
            data = objectData;
            int width = Mathf.Max(1, data.width);
            // Wide things (a boat) are centred across the tiles they cover.
            transform.position = Game.Map.CellToWorld(cell) + Vector3.right * (width - 1) / 2f;

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

            // Tall or wide things like the bed block every tile they cover.
            for (int i = 0; i < Mathf.Max(1, data.height); i++)
                for (int j = 0; j < width; j++)
                    Game.Map.Register(this, cell + Vector2Int.up * i + Vector2Int.right * j);

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

            var rule = PickRule();
            if (rule == null) return;
            System.Action go = null;
            if (!string.IsNullOrEmpty(rule.goTo))
                go = () => Game.World.GoTo(rule.goTo, rule.goX, rule.goY, rule.goFacing);
            if (!string.IsNullOrEmpty(rule.dialogue)) Game.Dialogue.Play(rule.dialogue, go);
            else go?.Invoke();
        }

        private InteractionRule PickRule()
        {
            if (data.rules == null) return null;
            foreach (var rule in data.rules)
            {
                if (!string.IsNullOrEmpty(rule.requireFlag) && !Game.Flags.Has(rule.requireFlag)) continue;
                if (Game.Flags.Has(rule.blockFlag)) continue;
                if (!Game.State.Check(rule.when)) continue;
                return rule;
            }
            return null;
        }
    }
}
