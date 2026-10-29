using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class WorldGameSessionManager : MonoBehaviour
    {
        public static WorldGameSessionManager instance;

        [Header("Active Players In Session")]
        public List<PlayerManager> players = new List<PlayerManager>();

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

        void OnDestroy()
        {
            if (instance == this) instance = null;
            players.Clear();
        }
        public void AddPlayerToActivePlayersList(PlayerManager player)
        {
            players.RemoveAll(value => value == null || !value.IsInitialized);
            if (player != null && !players.Contains(player)) players.Add(player);
        }
        public void RemovePlayerFromActivePlayersList(PlayerManager player)
        {
            players.Remove(player);
            players.RemoveAll(value => value == null || !value.IsInitialized);
        }
    }
}
