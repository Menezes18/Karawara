#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools
{
    public static class LocalSessionReviewRegression
    {
        public static void NewGameUsesInitialSceneAfterPreviousLoad()
        {
            var owner = new GameObject("G2 isolated initial-scene regression");
            var previous = WorldSaveGameManager.instance;
            try
            {
                var saves = owner.AddComponent<WorldSaveGameManager>();
                saves.enabled = false;
                saves.SaveRootOverride = Path.Combine(Path.GetTempPath(), "Karawara-G2-initial-scene-" + Guid.NewGuid().ToString("N"));
                typeof(WorldSaveGameManager).GetField("worldSceneIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(saves, 3);
                var result = saves.PrepareNewGame(CharacterSlot.CharacterSlot_01);
                if (!result.Success) throw new InvalidOperationException(result.Message);
                if (result.Value.sceneIndex != 1) throw new InvalidOperationException("New game reused previous scene " + result.Value.sceneIndex + " instead of initial scene 1.");
            }
            finally { WorldSaveGameManager.instance = previous; UnityEngine.Object.DestroyImmediate(owner); }
        }

        public static void AuthoredPlayerStaysInSceneUntilSessionBind()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run the authored player lifecycle check in Play Mode.");
            var scene = SceneManager.CreateScene("G2 authored player regression");
            var owner = new GameObject("G2 authored player");
            SceneManager.MoveGameObjectToScene(owner, scene);
            try
            {
                var player = owner.AddComponent<PlayerManager>();
                player.enabled = false;
                if (owner.scene != scene) throw new InvalidOperationException("Player moved out of its authored scene before session binding.");
            }
            finally { UnityEngine.Object.Destroy(owner); SceneManager.UnloadSceneAsync(scene); }
        }
    }
}
#endif
