using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager manager;

        void Awake()
        {
            if(!manager)
            {
                manager = this;
            }
        }

        public PlayerBaseDate GetPlayer()
        {
            PlayerBaseDate data = new PlayerBaseDate(Player.instancia.Level,Player.instancia.transform.position);
            return data;
        }

        public void SetPlayerData(PlayerBaseDate data)
        {
            Player.instancia.Level = data.Level;
            Player.instancia.transform.position = data.pos;
        }
    }
}
