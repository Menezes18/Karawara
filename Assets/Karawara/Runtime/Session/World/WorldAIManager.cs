using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Linq;

namespace RPGKarawara
{
    public class WorldAIManager : MonoBehaviour
    {
        public static WorldAIManager instance;
        public static WorldAIManager GetOrCreateInstance(UnityEngine.SceneManagement.Scene scene)
        {
            if (instance != null) return instance;
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<WorldAIManager>(true);
                if (existing != null) { instance = existing; return existing; }
            }
            var owner = new GameObject("Scene World AI");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner, scene);
            return owner.AddComponent<WorldAIManager>();
        }

        [Header("Characters")]
        [SerializeField] List<AICharacterSpawner> aiCharacterSpawners = new List<AICharacterSpawner>();
        [SerializeField] List<AICharacterManager> spawnedInCharacters = new List<AICharacterManager>();

        [Header("Bosses")]
        [SerializeField] List<AIBossCharacterManager> spawnedInBosses = new List<AIBossCharacterManager>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }


        public void SpawnCharacter(AICharacterSpawner aiCharacterSpawner)
        {
            if (aiCharacterSpawner != null)
            {
                if (!aiCharacterSpawners.Contains(aiCharacterSpawner)) aiCharacterSpawners.Add(aiCharacterSpawner);
                aiCharacterSpawner.AttemptToSpawnCharacter();
            }
        }

        public void AddCharacterToSpawnedCharactersList(AICharacterManager character)
        {
            if (character == null || !character.IsInitialized || !character.State.IsActive || character.State.IsDead) return;
            if (spawnedInCharacters.Contains(character))
                return;

            spawnedInCharacters.Add(character);

            AIBossCharacterManager bossCharacter = character as AIBossCharacterManager;

            if (bossCharacter != null)
            {
                //if (spawnedInBosses.Contains(bossCharacter))
                  //  return;

                if (!spawnedInBosses.Contains(bossCharacter)) spawnedInBosses.Add(bossCharacter);
            }
        }

        public AIBossCharacterManager GetBossCharacterByID(int ID)
        {
            return spawnedInBosses.FirstOrDefault(boss => boss != null && boss.IsInitialized && !boss.State.IsDead && boss.bossID == ID);
        }

        public void ResetAllCharacters()
        {
            DespawnAllCharacters();

            foreach (var spawner in aiCharacterSpawners)
            {
                if (spawner != null) spawner.ResetSpawnedCharacter();
            }
        }

        public void DespawnAllCharacters()
        {
            foreach (var character in spawnedInCharacters.ToArray())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                long qaAttempt = RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.StartDespawn(character);
                bool qaDespawned = false;
                try
                {
#endif
                if (character == null) continue;
                character.ShutdownLocal();
                character.gameObject.SetActive(false);
                Destroy(character.gameObject);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                qaDespawned = character == null || !character.IsInitialized;
                }
                finally { RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.FinishDespawn(qaAttempt, qaDespawned); }
#endif
            }

            spawnedInCharacters.Clear();
            spawnedInBosses.Clear();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        public bool QaIsRegistered(AICharacterManager actor) => actor != null && spawnedInCharacters != null && spawnedInCharacters.Contains(actor);
        public int QaBossReferences => spawnedInBosses == null ? 0 : spawnedInBosses.Count;
        public int QaLiveBossReferences => spawnedInBosses == null ? 0 : spawnedInBosses.Count(b => b != null && b.IsInitialized);
#endif

        public void RemoveCharacter(AICharacterManager character)
        {
            spawnedInCharacters.Remove(character);
            if (character is AIBossCharacterManager boss) spawnedInBosses.Remove(boss);
        }
        void OnDestroy()
        {
            if (instance == this) instance = null;
            spawnedInCharacters.Clear(); spawnedInBosses.Clear(); aiCharacterSpawners.Clear();
        }
    }
}
