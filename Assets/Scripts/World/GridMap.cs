using System.Collections.Generic;
using UnityEngine;

namespace ThaiMystery
{
    // Tile grid built from the text rows in map.json. Cell (0,0) is the bottom-left tile;
    // map.json is authored top-down, so rows are flipped when building.
    public class GridMap : MonoBehaviour
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        private char[,] tiles;
        private readonly Dictionary<Vector2Int, Interactable> occupants = new Dictionary<Vector2Int, Interactable>();

        public void Build(MapData data)
        {
            Height = data.rows.Length;
            Width = 0;
            foreach (var r in data.rows) Width = Mathf.Max(Width, r.Length);
            tiles = new char[Width, Height];

            for (int row = 0; row < Height; row++)
            {
                string line = data.rows[row];
                for (int col = 0; col < Width; col++)
                {
                    char c = col < line.Length ? line[col] : ' ';
                    var cell = FromRowCol(col, row);
                    tiles[cell.x, cell.y] = c;
                }
            }

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    string spriteName = TileSprite(cell);
                    if (spriteName == null) continue;
                    var go = new GameObject($"Tile {cell.x},{cell.y}");
                    go.transform.SetParent(transform, false);
                    go.transform.position = CellToWorld(cell);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = SpriteLibrary.Get(spriteName);
                    sr.sortingOrder = -1000;
                    AddWallShadows(cell, go.transform);
                }
            }
        }

        // Soft shadows on floor tiles that touch a wall give the room depth.
        private void AddWallShadows(Vector2Int cell, Transform tile)
        {
            if (!IsFloor(cell)) return;
            if (IsWall(cell + Vector2Int.up)) AddOverlay("ao_top", tile);
            if (IsWall(cell + Vector2Int.left)) AddOverlay("ao_left", tile);
            if (IsWall(cell + Vector2Int.right)) AddOverlay("ao_right", tile);
        }

        private static void AddOverlay(string sprite, Transform tile)
        {
            var go = new GameObject(sprite);
            go.transform.SetParent(tile, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteLibrary.Get(sprite);
            sr.sortingOrder = -999;
        }

        private bool IsFloor(Vector2Int c) => InBounds(c) && (tiles[c.x, c.y] == '.' || tiles[c.x, c.y] == ',');

        private bool IsWall(Vector2Int c) => !InBounds(c) || (tiles[c.x, c.y] != '.' && tiles[c.x, c.y] != ',');

        // Picks a sprite for a cell. Floors and walls rotate through variants so the
        // pattern doesn't visibly repeat; rugs pick a border piece from their neighbours.
        private string TileSprite(Vector2Int cell)
        {
            int hash = (cell.x * 73856093) ^ (cell.y * 19349663);
            hash = hash < 0 ? -hash : hash;
            switch (tiles[cell.x, cell.y])
            {
                case '#': return "wall_top";
                case '-': return "wall_upper_" + hash % 2;
                case '=': return "wall_lower_" + hash % 2;
                case '.': return "floor_" + hash % 4;
                case ',': return "rug_" + RugEdges(cell);
                default: return null;
            }
        }

        // Bit mask of the rug's open edges: 1 top, 2 right, 4 bottom, 8 left.
        private int RugEdges(Vector2Int cell)
        {
            int mask = 0;
            if (!IsRug(cell + Vector2Int.up)) mask |= 1;
            if (!IsRug(cell + Vector2Int.right)) mask |= 2;
            if (!IsRug(cell + Vector2Int.down)) mask |= 4;
            if (!IsRug(cell + Vector2Int.left)) mask |= 8;
            return mask;
        }

        private bool IsRug(Vector2Int c) => InBounds(c) && tiles[c.x, c.y] == ',';

        public Vector2Int FromRowCol(int col, int row) => new Vector2Int(col, Height - 1 - row);

        // Sprites use a bottom-centre pivot, so a cell's world position is its bottom-centre.
        public Vector3 CellToWorld(Vector2Int cell) => new Vector3(cell.x + 0.5f, cell.y, 0f);

        public Vector3 Center => new Vector3(Width / 2f, Height / 2f, 0f);

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public bool IsWalkable(Vector2Int c)
        {
            if (!InBounds(c)) return false;
            if (!IsFloor(c)) return false;
            return !(occupants.TryGetValue(c, out var o) && o.Solid);
        }

        public void Register(Interactable obj, Vector2Int cell) => occupants[cell] = obj;

        public Interactable GetInteractable(Vector2Int cell) =>
            occupants.TryGetValue(cell, out var o) && o.Visible ? o : null;
    }
}
