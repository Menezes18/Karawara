using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem;
namespace RPGKarawara{
    public class BossSpawnerInput : MonoBehaviour{
        [Header("Boss Prefab")] public GameObject bossPrefab; // Prefab do boss que será instanciado
        public Transform spawnPoint; // Ponto onde o boss será instanciado
        private GameObject instantiatedBoss; // Referência para o boss instanciado
        public int id = 4;
        private void Update(){
            if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame){
                SpawnBoss(); // Chama o método para instanciar o boss
            }
        }

        // Método para instanciar o boss
        public void SpawnBoss()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            // A request against the already-owned boss creates no new lifetime.
            long qaAttempt = instantiatedBoss != null ? 0 : DevTools.Performance.PerformanceAiCreationHistory.StartAttempt(this);
            try
            {
#endif
            if (bossPrefab != null && instantiatedBoss == null) // Verifica se o boss já foi instanciado
            {
                // Instancia o boss na posição e rotação do spawnPoint
                instantiatedBoss = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instantiatedBoss, gameObject.scene);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                DevTools.Performance.PerformanceAiCreationHistory.Observe(qaAttempt, this, instantiatedBoss, DevTools.Performance.PerformanceAiCreationHistory.Stage.Instantiated);
#endif

                // Definir o ID do boss e adicioná-lo à lista de bosses no WorldAIManager
                var bossCharacterManager = instantiatedBoss.GetComponent<AIBossCharacterManager>();
                if (bossCharacterManager != null)
                {
                    bossCharacterManager.bossID = id;
                    bossCharacterManager.InitializeLocal(bossCharacterManager.State, GameBootstrap.EnsureInstance().Registry);
                    WorldAIManager.instance.AddCharacterToSpawnedCharactersList(bossCharacterManager);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                    DevTools.Performance.PerformanceAiCreationHistory.Observe(qaAttempt, this, instantiatedBoss, DevTools.Performance.PerformanceAiCreationHistory.Stage.Registered);
#endif

                    // Caso você queira acordar o boss imediatamente
                    bossCharacterManager.WakeBoss();
                }
                else
                {
                    Debug.LogError("AIBossCharacterManager não encontrado no prefab instanciado.");
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            }
            finally { DevTools.Performance.PerformanceAiCreationHistory.FinishAttempt(qaAttempt); }
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        public GameObject QaSpawned => instantiatedBoss;
#endif
    }
}
