using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara
{
    public class TriggerFallCheck : MonoBehaviour
    {
        public float minY = 19f;
        public SavePositionOnTrigger savePositionScript;

        void Start()
        {
            // Verifica se a cena ativa é "Video1"
            if (SceneManager.GetActiveScene().name == "Video1")
            {
                // Obtém a referência do script SavePositionOnTrigger
                savePositionScript = FindObjectOfType<SavePositionOnTrigger>();
            }
        }

        void Update()
        {
            // Verifica se a cena ativa é "Video1"
            if (SceneManager.GetActiveScene().name != "Video1")
                return;
            

            // Checa se a posição Y do player está abaixo da altura mínima
            if (transform.position.y <= minY)
            {
                // Chama o método Reviver() se o player cair
                if (savePositionScript != null)
                {
                    savePositionScript.Reviver();
                }
                else
                {
                    Debug.LogWarning("Script SavePositionOnTrigger não encontrado!");
                }
            }
        }
    }
}