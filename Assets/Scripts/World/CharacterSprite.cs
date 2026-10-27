using UnityEngine;

namespace ThaiMystery
{
    // Animates an RPG Maker style 3x4 sheet: rows are down, left, right, up and the walk
    // cycle plays columns 0,1,2,1 with column 1 as the standing pose. If the character has a
    // full walk cycle (SpriteLibrary.GetWalkCycle) that plays instead, in the same total time.
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterSprite : MonoBehaviour
    {
        private static readonly int[] WalkCycle = { 0, 1, 2, 1 };
        private const float FrameTime = 0.15f;
        private const float CycleTime = FrameTime * 4;

        private SpriteRenderer sr;
        private Sprite[,] sheet;
        private Sprite[,] walkCycle;
        private int row;
        private bool walking;
        private float timer;
        private int step;

        public Vector2Int Facing { get; private set; } = Vector2Int.down;

        public void Init(Sprite[,] characterSheet, Vector2Int facing, Sprite[,] fullWalkCycle = null)
        {
            sr = GetComponent<SpriteRenderer>();
            sheet = characterSheet;
            walkCycle = fullWalkCycle;
            SetFacing(facing);
        }

        public void SetFacing(Vector2Int dir)
        {
            if (dir == Vector2Int.zero) return;
            Facing = dir;
            if (dir == Vector2Int.down) row = 0;
            else if (dir == Vector2Int.left) row = 1;
            else if (dir == Vector2Int.right) row = 2;
            else row = 3;
            Apply();
        }

        public void SetWalking(bool value)
        {
            if (walking == value) return;
            walking = value;
            if (!walking) step = 0;
            // Start a walk on a stride frame rather than the standing pose.
            else if (walkCycle != null) step = 1 % walkCycle.GetLength(1);
            timer = 0f;
            Apply();
        }

        private void Update()
        {
            if (!walking) return;
            int frames = walkCycle != null ? walkCycle.GetLength(1) : WalkCycle.Length;
            float frameTime = CycleTime / frames;
            timer += Time.deltaTime;
            if (timer >= frameTime)
            {
                timer -= frameTime;
                step = (step + 1) % frames;
                Apply();
            }
        }

        private void LateUpdate()
        {
            // Lower on screen draws in front, the usual top-down overlap rule.
            if (sr != null) sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10f);
        }

        private void Apply()
        {
            if (sheet == null || sr == null) return;
            if (walking && walkCycle != null) sr.sprite = walkCycle[row, step];
            else sr.sprite = sheet[row, walking ? WalkCycle[step] : 1];
        }

        public static Vector2Int ParseFacing(string s)
        {
            switch ((s ?? "").ToLowerInvariant())
            {
                case "up": return Vector2Int.up;
                case "left": return Vector2Int.left;
                case "right": return Vector2Int.right;
                default: return Vector2Int.down;
            }
        }
    }
}
