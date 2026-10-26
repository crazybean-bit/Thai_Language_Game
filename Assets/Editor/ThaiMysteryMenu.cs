using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThaiMystery.Editor
{
    public static class ThaiMysteryMenu
    {
        private const string ScenePath = "Assets/Scenes/Prototype.unity";

        [MenuItem("Thai Mystery/Open Prototype Scene")]
        public static void OpenPrototype()
        {
            if (!System.IO.File.Exists(ScenePath)) CreatePrototype();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        // Rebuilds the prototype scene from scratch: one camera and one GameBootstrap.
        [MenuItem("Thai Mystery/Rebuild Prototype Scene")]
        public static void CreatePrototype()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cam.AddComponent<Camera>();
            camera.orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);

            new GameObject("Game").AddComponent<GameBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("Thai Mystery/Clear Saved Guesses")]
        public static void ClearSave()
        {
            WordBook.ClearSave();
            Debug.Log("[ThaiMystery] Saved word guesses cleared.");
        }
    }
}
