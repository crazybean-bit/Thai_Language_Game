using System.Collections;
using UnityEngine;

namespace ThaiMystery
{
    // Drop this on an empty GameObject in an empty scene and press Play. It loads the JSON
    // data and builds the map, characters, camera and UI in code.
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Clears saved word guesses on Play. Handy while testing the opening.")]
        [SerializeField] private bool resetSaveOnPlay = true;

        private IEnumerator Start()
        {
            if (resetSaveOnPlay) WordBook.ClearSave();

            var map = GameDataLoader.Load<MapData>("Data/map");
            var words = GameDataLoader.Load<WordList>("Data/words");
            var dialogue = GameDataLoader.Load<DialogueList>("Data/dialogue");
            if (map == null || words == null || dialogue == null) yield break;

            Game.Flags = new GameFlags();
            Game.Words = new WordBook(words);

            var mapGo = new GameObject("Map");
            Game.Map = mapGo.AddComponent<GridMap>();
            Game.Map.Build(map);

            if (map.objects != null)
            {
                foreach (var obj in map.objects)
                {
                    var go = new GameObject(obj.id);
                    go.transform.SetParent(mapGo.transform, false);
                    go.AddComponent<Interactable>().Init(obj, Game.Map.FromRowCol(obj.x, obj.y));
                }
            }

            var lightGo = new GameObject("Lighting");
            lightGo.AddComponent<SpriteRenderer>();
            lightGo.AddComponent<LightingOverlay>().Build(map.lighting, Game.Map);

            var playerGo = new GameObject("Player");
            playerGo.AddComponent<SpriteRenderer>();
            Game.Player = playerGo.AddComponent<PlayerController>();
            Game.Player.Init(SpriteLibrary.GetCharacterSheet("player"), Game.Map.FromRowCol(map.playerX, map.playerY),
                CharacterSprite.ParseFacing(map.playerFacing));

            SetupCamera(playerGo.transform);

            var uiGo = new GameObject("UI");
            Game.UI = uiGo.AddComponent<GameUI>();
            Game.UI.Build();

            Game.Voice = uiGo.AddComponent<VoicePlayer>();
            Game.Dialogue = uiGo.AddComponent<DialogueRunner>();
            Game.Dialogue.Init(dialogue, Game.UI.DialogueBox, Game.Voice);

            yield return Game.UI.FadeIn(1.8f);
            if (!string.IsNullOrEmpty(map.introDialogue)) Game.Dialogue.Play(map.introDialogue);
        }

        private static void SetupCamera(Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x12, 0x0F, 0x0E, 0xFF);
            cam.gameObject.AddComponent<CameraFollow>().Init(target);
        }
    }
}
