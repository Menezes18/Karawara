using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools.Editor
{
    // Keeps the user's unsaved scene open while a separate playground is exercised.
    [InitializeOnLoad]
    public static class DevValidationSceneSession
    {
        const string Key = "Karawara.DevValidationSceneSession";
        [Serializable] class RootState { public string id; public bool active; }
        [Serializable] class Session
        {
            public string activeScene;
            public bool closePlayground;
            public List<RootState> roots = new List<RootState>();
        }

        static DevValidationSceneSession()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Karawara/Validation/Stage Playground Preserving Open Scenes")]
        public static void Stage()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!string.IsNullOrEmpty(SessionState.GetString(Key, ""))) throw new InvalidOperationException("Restore the previous validation session first.");
            var active = SceneManager.GetActiveScene();
            var session = new Session { activeScene = active.path };
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.path == DevPlaygroundBuilder.ScenePath) continue;
                foreach (var root in scene.GetRootGameObjects())
                    session.roots.Add(new RootState { id = GlobalObjectId.GetGlobalObjectIdSlow(root).ToString(), active = root.activeSelf });
            }
            var playground = SceneManager.GetSceneByPath(DevPlaygroundBuilder.ScenePath);
            session.closePlayground = !playground.IsValid() || !playground.isLoaded;
            SessionState.SetString(Key, JsonUtility.ToJson(session));
            foreach (var state in session.roots) Resolve(state.id)?.SetActive(false);
            if (session.closePlayground) playground = EditorSceneManager.OpenScene(DevPlaygroundBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(playground);
            Debug.Log("Playground staged. Open scenes and their unsaved data are preserved; original root states restore on exiting Play Mode.");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Restore;
        }

        [MenuItem("Karawara/Validation/Restore Open Scene States")]
        public static void Restore()
        {
            string json = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(json) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var session = JsonUtility.FromJson<Session>(json);
            foreach (var root in session.roots) Resolve(root.id)?.SetActive(root.active);
            var original = SceneManager.GetSceneByPath(session.activeScene);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            var playground = SceneManager.GetSceneByPath(DevPlaygroundBuilder.ScenePath);
            if (session.closePlayground && playground.IsValid() && playground.isLoaded) EditorSceneManager.CloseScene(playground, true);
            SessionState.EraseString(Key);
            Debug.Log("Original open scene states restored after playground validation.");
        }

        static GameObject Resolve(string id)
        {
            return GlobalObjectId.TryParse(id, out var globalId) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject : null;
        }
    }
}
