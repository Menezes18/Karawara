using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace RPGKarawara
{
    public class WorldObjectManager : MonoBehaviour
    {
        public static WorldObjectManager instance;

        [Header("Network Objects")]
        [SerializeField] List<NetworkObjectSpawner> networkObjectSpawners = new List<NetworkObjectSpawner>();
        [SerializeField] List<GameObject> spawnedInObjects = new List<GameObject>();

        [Header("Fog Walls")]
        public List<FogWallInteractable> fogWalls = new List<FogWallInteractable>();

        public static WorldObjectManager GetOrCreateInstance()
        {
            if (instance != null) return instance;
            var existing = FindFirstObjectByType<WorldObjectManager>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }
            // Some gameplay scenes contain object spawners without a manager prefab.
            // Install the real manager so fog walls and spawn registration still work.
            return new GameObject("World Object Manager").AddComponent<WorldObjectManager>();
        }

        // 4. When the fog walls are spawned, add them to the world fog wall list
        // 5. Grab the correct fogwall from the list on the boss manager when the boss is being initialized

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void SpawnObject(NetworkObjectSpawner networkObjectSpawner)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                networkObjectSpawners.RemoveAll(spawner => spawner == null);
                networkObjectSpawners.Add(networkObjectSpawner);
                networkObjectSpawner.AttemptToSpawnObject();
            }
        }

        public void AddFogWallToList(FogWallInteractable fogWall)
        {
            if (!fogWalls.Contains(fogWall))
            {
                fogWalls.Add(fogWall);
            }
        }

        public void RemoveFogWallFromList(FogWallInteractable fogWall)
        {
            if (fogWalls.Contains(fogWall))
            {
                fogWalls.Remove(fogWall);
            }
        }
    }
}
