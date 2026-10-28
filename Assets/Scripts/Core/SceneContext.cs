using UnityEngine;
using UnityEngine.SceneManagement;
namespace RPGKarawara
{
    /// <summary>Scene-owned readiness. Register dependencies explicitly before a session binds its player.</summary>
    public sealed class SceneContext : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<CharacterManager> ownedCharacters = new System.Collections.Generic.List<CharacterManager>();
        public bool IsReady { get; private set; }
        public bool IsMenu => gameObject.scene.name == "SceneMenu";
        public PlayerInputManager Input { get; private set; }
        public PlayerUIManager UI { get; private set; }
        public static SceneContext GetOrCreate(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var context = root.GetComponentInChildren<SceneContext>(true);
                if (context != null) return context;
            }
            var owner = new GameObject("Local Scene Context");
            SceneManager.MoveGameObjectToScene(owner, scene);
            return owner.AddComponent<SceneContext>();
        }
        private void Start() => TryPrepare();
        public bool TryPrepare()
        {
            if (IsReady) return true;
            if (!gameObject.scene.isLoaded) return false;
            Input = PlayerInputManager.instance;
            UI = PlayerUIManager.instance;
            if (!IsMenu && (Input == null || UI == null || UI.playerUIHudManager == null || !UI.playerUIHudManager.IsHealthBarReady)) return false;
            var registry = GameBootstrap.EnsureInstance().Registry;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var character in root.GetComponentsInChildren<CharacterManager>(false))
                    if (!(character is PlayerManager))
                    {
                        if (!character.IsInitialized) character.InitializeLocal(character.State, registry);
                        ownedCharacters.Add(character);
                    }
            IsReady = true;
            return true;
        }
        private void OnDestroy()
        {
            IsReady = false;
            foreach (var character in ownedCharacters)
                if (character != null) character.ShutdownLocal();
            ownedCharacters.Clear();
        }
    }
}
