using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
namespace RPGKarawara{
    public class BossSpawnerInput : MonoBehaviour{
        [Header("Boss Prefab")] public GameObject bossPrefab; // Prefab do boss que será instanciado
        public Transform spawnPoint; // Ponto onde o boss será instanciado
        private GameObject instantiatedBoss; // Referência para o boss instanciado
        public int id = 4;
        private void Update(){
            if (Keyboard.current.xKey.wasPressedThisFrame){
                SpawnBoss(); // Chama o método para instanciar o boss
            }
        }

        // Método para instanciar o boss
        public void SpawnBoss()
        {
            if (bossPrefab != null && instantiatedBoss == null) // Verifica se o boss já foi instanciado
            {
                // Instancia o boss na posição e rotação do spawnPoint
                instantiatedBoss = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation);
                
                // Certifique-se de que o boss usa o NetworkObject para ser sincronizado na rede
                NetworkObject networkObject = instantiatedBoss.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.Spawn();
                }

                // Definir o ID do boss e adicioná-lo à lista de bosses no WorldAIManager
                var bossCharacterManager = instantiatedBoss.GetComponent<AIBossCharacterManager>();
                if (bossCharacterManager != null)
                {
                    bossCharacterManager.bossID = id;
                    WorldAIManager.instance.AddCharacterToSpawnedCharactersList(bossCharacterManager);

                    // Caso você queira acordar o boss imediatamente
                    bossCharacterManager.WakeBoss();
                }
                else
                {
                    Debug.LogError("AIBossCharacterManager não encontrado no prefab instanciado.");
                }
            }
        }
    }
}