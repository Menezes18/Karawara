using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Karawara.Core.Characters;
using Karawara.Core.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RPGKarawara
{
    public sealed class GameSessionController : MonoBehaviour
    {
        private GameBootstrap bootstrap;
        private ISceneTransitionService transitions;
        private string menuScenePath;
        private CancellationTokenSource transitionCancellation;
        private bool ownsQaIsolation;
        private bool suspendedPlayerBinding, suspendedInputEnabled;
        private string previousSessionSaveRoot;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        private bool previousDevelopmentIsolation;
#endif
        public bool IsBusy { get; private set; }
        public bool HasActiveSession => Player != null;
        public bool IsActive => HasActiveSession;
        public bool IsQaSession { get; private set; }
        public PlayerManager Player { get; private set; }
        public string LastError { get; private set; }
        public void Configure(GameBootstrap owner, ISceneTransitionService service, string menuPath)
        {
            bootstrap = owner; transitions = service; menuScenePath = menuPath;
        }
        public Task<SessionResult> StartNewGameAsync(int slotId) => RunSlotAsync(slotId, true);
        public Task<SessionResult> LoadGameAsync(int slotId) => RunSlotAsync(slotId, false);
        private async Task<SessionResult> RunSlotAsync(int slotId, bool create)
        {
            if (IsBusy) return SessionResult.Busy;
            if (slotId < 1 || slotId > 3) return Fail("Select a character slot from 1 to 3.");
            IsBusy = true;
            transitionCancellation = new CancellationTokenSource();
            bool sceneRequested = false;
            try
            {
                var saves = WorldSaveGameManager.instance;
                if (saves == null) return Fail("Save service is unavailable.");
                // IO and validation happen before input, scene, player or active save context changes.
                var slot = (CharacterSlot)(slotId - 1);
                var snapshot = create ? saves.PrepareNewGame(slot) : saves.TryLoadSelectedSlot(slot);
                if (!snapshot.Success) return Fail("Save " + snapshot.Error + ": " + snapshot.Message);
                string path = SceneUtility.GetScenePathByBuildIndex(snapshot.Value.sceneIndex);
                if (string.IsNullOrEmpty(path)) return Fail("The saved scene is not enabled in build settings.");
                SetInput(false);
                if (saves.loadscene != null) saves.loadscene.LoadGame();
                sceneRequested = true;
                DisposeSession();
                // Scene-owned bosses/checkpoints read the validated progress during initialization.
                saves.AcceptSessionLoad(slot, snapshot.Value, null);
                var result = await transitions.LoadAsync(path, transitionCancellation.Token);
                if (result != SessionResult.Success) return Fail("Scene loading did not finish. Return to menu to retry.");
                transitionCancellation.Token.ThrowIfCancellationRequested();
                await saves.PrepareWorldAssetsAsync(transitionCancellation.Token);
                var context = SceneContext.GetOrCreate(SceneManager.GetActiveScene());
                BindPlayer(context);
                saves.player = Player;
                if (!saves.ApplyPendingSaveWhenReady()) return Fail("Player or HUD could not restore the save. Return to menu to retry.");
                LastError = null;
                Time.timeScale = 1f;
                SetInput(true);
                return SessionResult.Success;
            }
            catch (Exception exception) { return Fail(exception.Message); }
            finally
            {
                if (sceneRequested && !string.IsNullOrEmpty(LastError)) { SetInput(false); DisposeSession(); }
                if (WorldSaveGameManager.instance != null && WorldSaveGameManager.instance.loadscene != null)
                    WorldSaveGameManager.instance.loadscene.desativado();
                transitionCancellation.Dispose(); transitionCancellation = null; IsBusy = false;
            }
        }
        private void BindPlayer(SceneContext context)
        {
            if (!context.IsReady) throw new InvalidOperationException("Scene dependencies are not ready.");
            if (Player != null) throw new InvalidOperationException("Session already owns a player.");
            // A direct QA scene may contain one authored player. Production uses its configured prefab.
            foreach (var root in context.gameObject.scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<PlayerManager>(true))
                {
                    if (Player != null) throw new InvalidOperationException("More than one player was authored in the scene.");
                    Player = candidate;
                }
            if (Player == null)
            {
                if (bootstrap.PlayerPrefab == null) throw new InvalidOperationException("GameBootstrap requires its player prefab reference.");
                Player = Instantiate(bootstrap.PlayerPrefab);
            }
            DontDestroyOnLoad(Player.gameObject);
            if (!Player.IsInitialized) Player.InitializeLocal(Player.State, bootstrap.Registry);
            if (context.Input != null) context.Input.BindSessionPlayer(Player);
            if (WorldSaveGameManager.instance != null) WorldSaveGameManager.instance.player = Player;
        }
        public async Task<SessionResult> ReturnToMenuAsync()
        {
            if (IsBusy) return SessionResult.Busy;
            IsBusy = true; transitionCancellation = new CancellationTokenSource();
            try
            {
                SetInput(false); DisposeSession();
                var result = await transitions.LoadAsync(menuScenePath, transitionCancellation.Token);
                if (result != SessionResult.Success) return Fail("Could not return to menu. Retry the return command.");
                LastError = null; Time.timeScale = 1f;
                return SessionResult.Success;
            }
            catch (Exception exception) { return Fail(exception.Message); }
            finally { transitionCancellation.Dispose(); transitionCancellation = null; IsBusy = false; }
        }
        public async void StartDirectQaSession(Scene scene)
        {
            if (IsBusy || HasActiveSession) return;
            IsBusy = true; IsQaSession = true;
            transitionCancellation = new CancellationTokenSource();
            try
            {
                BeginQaIsolation();
                SetInput(false);
                var context = SceneContext.GetOrCreate(scene);
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!context.TryPrepare())
                {
                    transitionCancellation.Token.ThrowIfCancellationRequested();
                    if (Time.realtimeSinceStartup >= deadline) throw new InvalidOperationException("Direct QA scene dependencies did not become ready.");
                    await Task.Yield();
                }
                BindPlayer(context); LastError = null; SetInput(true);
            }
            catch (Exception exception) { Fail(exception.Message); DisposeSession(); }
            finally { transitionCancellation.Dispose(); transitionCancellation = null; IsBusy = false; }
        }
        public void BeginQaSession(PlayerManager suppliedPlayer)
        {
            if (suppliedPlayer == null) throw new ArgumentNullException(nameof(suppliedPlayer));
            if (IsBusy) throw new InvalidOperationException("A scene transition is already running.");
            if (Player != null && Player != suppliedPlayer) throw new InvalidOperationException("Session already owns another player.");
            Player = suppliedPlayer; IsQaSession = true;
            BeginQaIsolation();
            if (!Player.IsInitialized) Player.InitializeLocal(Player.State, bootstrap.Registry);
            DontDestroyOnLoad(Player.gameObject);
            if (WorldSaveGameManager.instance != null) WorldSaveGameManager.instance.player = Player;
            if (PlayerInputManager.instance != null) PlayerInputManager.instance.BindSessionPlayer(Player);
            Player.RefreshLocalHUD(); SetInput(true);
        }
        public PlayerManager SpawnQaPlayer(PlayerManager prefab, Vector3 position, Quaternion rotation)
        {
            if (HasActiveSession || IsBusy) throw new InvalidOperationException("Session already owns a player or transition.");
            var supplied = Instantiate(prefab, position, rotation);
            try { BeginQaSession(supplied); return supplied; }
            catch { Destroy(supplied.gameObject); throw; }
        }
        public void CancelTransition() => transitionCancellation?.Cancel();
        public void SuspendPlayerBinding(PlayerManager player)
        {
            if (Player != player || IsBusy || PlayerInputManager.instance == null) return;
            suspendedPlayerBinding = PlayerInputManager.instance.player == player;
            suspendedInputEnabled = PlayerInputManager.instance.SessionInputEnabled;
        }
        public void ResumePlayerBinding(PlayerManager player)
        {
            if (!suspendedPlayerBinding || Player != player || IsBusy || !player.IsInitialized || PlayerInputManager.instance == null) return;
            PlayerInputManager.instance.BindSessionPlayer(player);
            // Dead-player commands are gated by input handling. Preserve the session's
            // intent so a later revive does not permanently lose its controls.
            PlayerInputManager.instance.SetSessionInputEnabled(suspendedInputEnabled);
            suspendedPlayerBinding = false;
        }
        public void DisposeSession()
        {
            SetInput(false);
            if (PlayerInputManager.instance != null) PlayerInputManager.instance.BindSessionPlayer(null);
            if (Player != null) { Player.ShutdownLocal(); Destroy(Player.gameObject); Player = null; }
            if (WorldSaveGameManager.instance != null) WorldSaveGameManager.instance.ClearSession();
            if (ownsQaIsolation && WorldSaveGameManager.instance != null)
            {
                WorldSaveGameManager.instance.SessionSaveRootOverride = previousSessionSaveRoot;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                WorldSaveGameManager.DevPlaygroundIsolationActive = previousDevelopmentIsolation;
#endif
            }
            ownsQaIsolation = false;
            suspendedPlayerBinding = false;
            IsQaSession = false;
        }
        private void BeginQaIsolation()
        {
            var saves = WorldSaveGameManager.instance;
            if (saves == null) throw new InvalidOperationException("Direct QA requires a save context.");
            if (!ownsQaIsolation)
            {
                previousSessionSaveRoot = saves.SessionSaveRootOverride;
                saves.SessionSaveRootOverride = Path.Combine(Path.GetTempPath(), "Karawara-DirectQA-" + Guid.NewGuid().ToString("N"));
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                previousDevelopmentIsolation = WorldSaveGameManager.DevPlaygroundIsolationActive;
                WorldSaveGameManager.DevPlaygroundIsolationActive = true;
#endif
                ownsQaIsolation = true;
            }
            if (saves.currentCharacterData == null)
                saves.currentCharacterData = new CharacterSaveData { sceneIndex = SceneManager.GetActiveScene().buildIndex,
                    vitality = 1, endurance = 1, currentHealth = Player == null ? 400 : Player.State.Health };
        }
        private static void SetInput(bool enabled)
        {
            if (PlayerInputManager.instance != null) PlayerInputManager.instance.SetSessionInputEnabled(enabled);
        }
        private SessionResult Fail(string message) { LastError = message; Debug.LogWarning("Local session: " + message); return SessionResult.Failed; }
        private void OnGUI()
        {
            if (string.IsNullOrEmpty(LastError) || IsBusy) return;
            GUI.Box(new Rect(12, 12, 520, 90), LastError);
            if (GUI.Button(new Rect(24, 60, 200, 30), "Return to menu / Retry")) _ = ReturnToMenuAsync();
        }
    }
}
