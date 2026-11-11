using System.Collections;
using UnityEngine;

namespace ThaiMystery
{
    // Tile by tile movement like RPG Maker: hold a direction to keep walking,
    // press Space/Enter/Z to inspect whatever is on the tile you're facing.
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float tilesPerSecond = 4f;

        private CharacterSprite character;
        private bool moving;

        public Vector2Int Cell { get; private set; }

        public void Init(Sprite[,] sheet, Vector2Int startCell, Vector2Int facing, Sprite[,] walkCycle = null)
        {
            character = gameObject.AddComponent<CharacterSprite>();
            character.Init(sheet, facing, walkCycle);
            Cell = startCell;
            transform.position = Game.Map.CellToWorld(startCell);
        }

        // His look changes with the days: on day 1 he's just washed up (player_day1, if that
        // sprite exists); from day 2 on he has his ordinary look (player).
        public static string LookForToday()
        {
            const string washedUp = "player_day1";
            return Game.State.Day <= 1 && SpriteLibrary.Has(washedUp) ? washedUp : "player";
        }

        public void Dress(string look)
        {
            var sheet = SpriteLibrary.GetCharacterSheet(look);
            if (sheet == null) return;
            character.Init(sheet, character.Facing, SpriteLibrary.GetWalkCycle(look));
        }

        // Puts him somewhere new, e.g. on the other side of a door.
        public void PlaceAt(Vector2Int cell, Vector2Int facing)
        {
            StopAllCoroutines();
            moving = false;
            Cell = cell;
            transform.position = Game.Map.CellToWorld(cell);
            character.SetFacing(facing);
            character.SetWalking(false);
        }

        private void Update()
        {
            if (moving) return;

            if (!Game.PlayerCanAct)
            {
                character.SetWalking(false);
                return;
            }

            if (GameInput.Confirm())
            {
                TryInteract();
                return;
            }

            var dir = GameInput.MoveDirection();
            if (dir == Vector2Int.zero)
            {
                character.SetWalking(false);
                return;
            }

            character.SetFacing(dir);
            var target = Cell + dir;
            if (Game.Map.IsWalkable(target)) StartCoroutine(Step(target));
            else character.SetWalking(false);
        }

        private IEnumerator Step(Vector2Int target)
        {
            moving = true;
            character.SetWalking(true);
            Vector3 from = transform.position;
            Vector3 to = Game.Map.CellToWorld(target);
            Cell = target;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * tilesPerSecond;
                transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }
            transform.position = to;
            moving = false;
            // Exits off the edge of a map go off as soon as he steps on them.
            var trigger = Game.Map.GetStepTrigger(Cell);
            if (trigger != null && Game.PlayerCanAct)
            {
                character.SetWalking(false);
                trigger.Interact(this);
            }
        }

        private void TryInteract()
        {
            var target = Game.Map.GetInteractable(Cell + character.Facing);
            if (target == null) return;
            character.SetWalking(false);
            target.Interact(this);
        }
    }
}
