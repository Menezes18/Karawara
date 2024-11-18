using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace RPGKarawara
{
    public class NPCATIVAR : MonoBehaviour
    {
        public float interactionRange = 3f; // Distância de interação
        public Transform player; // Referência ao transform do playe

        private void Start(){
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }

        private void Update()
        {
            // Verifica se o player está dentro do alcance
            if (Vector3.Distance(transform.position, player.position) <= interactionRange)
            {
                // Verifica se a tecla E foi pressionada
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    Interact();
                }
            }
        }

        // Método de interação que será chamado quando a tecla E for pressionada
        private void Interact()
        {
            // Lógica de interação, exemplo:
            Debug.Log("Interagiu com o objeto!");
            // Você pode adicionar aqui o que deseja fazer durante a interação, como abrir um menu, pegar um item, etc.
        }
    }
}
