using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class GameManager : MonoBehaviour
    {
       /* public static GameManager manager;
        public PlayerData player;
        public Transform respawnPoint;
        private float vida;
        private float stamina;
        void Awake()
        {
            if(!manager)
            {
                manager = this;
            }

            vida = player.vida;
            
        }

        private void Update(){
            if (Keyboard.current.mKey.wasPressedThisFrame){
                TakeDamage(10);
            }
            if(player.vida <= 0)
            {
                Respawn();
            }
        }

        public void TakeDamage(int damageAmount)
        {
            player.vida -= damageAmount;
            if(player.vida <= 0)
            {
                Respawn();
                player.vida = player.maxVida;
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
        
        public void Respawn()
        {
            Player.instancia.transform.position = respawnPoint.position;
            
           
        }*/
    } 
}
