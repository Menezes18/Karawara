using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RPGKarawara
{
    /// <summary>Scene composition point; actors and optional content are released with this scene.</summary>
    public sealed class SceneContext : MonoBehaviour
    {
        private readonly List<CharacterManager> ownedCharacters = new List<CharacterManager>();
        public event Action<SceneContext> Released;
        public bool IsReady { get; private set; }
        public bool IsMenu => gameObject.scene.name == "SceneMenu";
        public PlayerInputManager Input { get; private set; }
        public PlayerUIManager UI { get; private set; }
        public PlayerCamera Camera { get; private set; }
        public WorldItemDatabase Catalog { get; private set; }
        public SavePositionOnTrigger Respawn { get; private set; }
        public bool HasSceneAudio { get; private set; }
        private WorldSceneInitializer worldAssets;
        public WorldSceneInitializer WorldAssets => worldAssets != null ? worldAssets : worldAssets = gameObject.AddComponent<WorldSceneInitializer>();
        public static SceneContext GetOrCreate(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var context = root.GetComponentInChildren<SceneContext>(true);
                if (context != null) return context;
            }
            var owner = new GameObject("Local Scene Context"); SceneManager.MoveGameObjectToScene(owner, scene);
            return owner.AddComponent<SceneContext>();
        }
        private void Start() => TryPrepare();
        public bool TryPrepare()
        {
            if (IsReady) return true;
            if (!gameObject.scene.isLoaded) return false;
            Input = PlayerInputManager.instance; UI = PlayerUIManager.instance;
            if (!IsMenu && (Input == null || UI == null || UI.playerUIHudManager == null || !UI.playerUIHudManager.IsHealthBarReady)) return false;
            var bootstrap = GameBootstrap.EnsureInstance(); bootstrap.RefreshApplicationServices();
            UI?.playerUIHudManager?.BindSession(bootstrap.Session);
            UI?.playerUIPopUpManager?.BindOwner(UI);
            Camera = PlayerCamera.instance; Catalog = bootstrap.Catalog != null ? bootstrap.Catalog : WorldItemDatabase.Instance;
            var registry = bootstrap.Registry;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var character in root.GetComponentsInChildren<CharacterManager>(false))
                    if (!(character is PlayerManager))
                    {
                        if (!character.IsInitialized) character.InitializeLocal(character.State, registry);
                        ownedCharacters.Add(character);
                    }
                foreach (var slot in root.GetComponentsInChildren<UI_Character_Save_Slot>(true)) slot.BindServices(WorldSaveGameManager.instance, TitleScreenManager.Instance);
                if (Respawn == null) Respawn = root.GetComponentInChildren<SavePositionOnTrigger>(true);
                foreach (var audio in root.GetComponentsInChildren<AudioListener>(false))
                    if (audio.isActiveAndEnabled && audio.GetComponentInParent<PlayerManager>() == null) HasSceneAudio = true;
            }
            IsReady = true; return true;
        }
        public void ReleaseScene()
        {
            IsReady = false; Released?.Invoke(this); Released = null;
            foreach (var character in ownedCharacters) if (character != null) character.ShutdownLocal();
            ownedCharacters.Clear(); Input = null; UI = null; Camera = null; Catalog = null; Respawn = null; worldAssets = null; HasSceneAudio = false;
        }
        private void OnDestroy() => ReleaseScene();
    }
}
