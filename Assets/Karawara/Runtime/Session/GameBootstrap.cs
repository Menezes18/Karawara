using System.IO;
using Karawara.Core.Characters;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RPGKarawara
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }
        [SerializeField] private PlayerManager playerPrefab;
        [SerializeField] private string menuScenePath = "Assets/Scenes/SceneMenu.unity";
        public PlayerManager PlayerPrefab => playerPrefab;
        public CharacterRegistry Registry { get; private set; }
        public GameSessionController Session { get; private set; }
        public WorldItemDatabase Catalog { get; private set; }
        public WorldCharacterEffectsManager Effects { get; private set; }
        public WorldUtilityManager Utility { get; private set; }
        public static GameBootstrap EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<GameBootstrap>();
            return existing != null ? existing : new GameObject("Game Bootstrap").AddComponent<GameBootstrap>();
        }
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A direct/empty entry scene may have created the application shell
                // before the authored menu supplies its serialized player definition.
                if (Instance.playerPrefab == null && playerPrefab != null) Instance.ConfigurePlayerPrefab(playerPrefab);
                Destroy(this); return;
            }
            Instance = this;
            Registry = new CharacterRegistry();
            Session = GetComponent<GameSessionController>() ?? gameObject.AddComponent<GameSessionController>();
            Session.Configure(this, new SceneTransitionService(), menuScenePath);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeEntryScene()
        {
            var bootstrap = EnsureInstance();
            bootstrap.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshApplicationServices();
            SceneContext.GetOrCreate(scene);
            if (!Session.IsBusy && scene.name != "SceneMenu" && scene.name != "DevPlayground" && !Session.HasActiveSession)
                Session.StartDirectQaSession(scene);
            else if (!Session.IsBusy && scene.name == "SceneMenu") Session.DisposeSession();
        }
        public void RefreshApplicationServices()
        {
            // These authored definitions/settings belong to the application, not the
            // menu scene that first supplied them. A scene player never owns them.
            if (Catalog == null && WorldItemDatabase.Instance != null)
            { Catalog = WorldItemDatabase.Instance; Catalog.transform.SetParent(transform, true); }
            if (Effects == null && WorldCharacterEffectsManager.instance != null)
            { Effects = WorldCharacterEffectsManager.instance; Effects.transform.SetParent(transform, true); }
            if (Utility == null && WorldUtilityManager.Instance != null)
            { Utility = WorldUtilityManager.Instance; Utility.transform.SetParent(transform, true); }
        }
        public void ConfigurePlayerPrefab(PlayerManager prefab) { playerPrefab = prefab; RefreshApplicationServices(); }
        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Session.CancelTransition();
            Session.DisposeSession();
            Registry.Clear();
            Instance = null;
        }
    }
}
