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
        public static GameBootstrap EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<GameBootstrap>();
            return existing != null ? existing : new GameObject("Game Bootstrap").AddComponent<GameBootstrap>();
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
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
            SceneContext.GetOrCreate(scene);
            if (!Session.IsBusy && scene.name != "SceneMenu" && scene.name != "DevPlayground" && !Session.HasActiveSession)
                Session.StartDirectQaSession(scene);
            else if (!Session.IsBusy && scene.name == "SceneMenu") Session.DisposeSession();
        }
        public void ConfigurePlayerPrefab(PlayerManager prefab) { playerPrefab = prefab; }
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
