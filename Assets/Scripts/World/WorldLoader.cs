using System.Collections;
using UnityEngine;

namespace ThaiMystery
{
    // Builds a map from Resources/Data/Maps/<id>.json (tiles, objects, lighting) and swaps
    // between maps when the player goes through a door. Flags and story values carry over;
    // the map itself is rebuilt fresh each visit.
    public class WorldLoader : MonoBehaviour
    {
        private const float FadeSeconds = 0.35f;

        private GameObject root;
        private bool travelling;

        public string Current { get; private set; }

        public MapData Load(string id)
        {
            var data = GameDataLoader.Load<MapData>("Data/Maps/" + id);
            if (data == null) return null;

            if (root != null) Destroy(root);
            root = new GameObject("Map " + id);
            Game.Map = root.AddComponent<GridMap>();
            Game.Map.Build(data);

            if (data.objects != null)
            {
                foreach (var obj in data.objects)
                {
                    var go = new GameObject(obj.id);
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<Interactable>().Init(obj, Game.Map.FromRowCol(obj.x, obj.y));
                }
            }

            var lightGo = new GameObject("Lighting");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.AddComponent<SpriteRenderer>();
            lightGo.AddComponent<LightingOverlay>().Build(data.lighting, Game.Map);

            Current = id;
            return data;
        }

        public void GoTo(string id, int x, int y, string facing)
        {
            if (!travelling) StartCoroutine(Travel(id, x, y, facing));
        }

        private IEnumerator Travel(string id, int x, int y, string facing)
        {
            travelling = true;
            yield return Game.UI.FadeOut(FadeSeconds);
            if (Load(id) != null)
                Game.Player.PlaceAt(Game.Map.FromRowCol(x, y), CharacterSprite.ParseFacing(facing));
            else
                Debug.LogWarning($"[ThaiMystery] Can't go to map '{id}': no Resources/Data/Maps/{id}.json");
            yield return Game.UI.FadeIn(FadeSeconds);
            travelling = false;
        }
    }
}
