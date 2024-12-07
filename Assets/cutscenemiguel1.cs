using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class cutscenemiguel1 : MonoBehaviour
    {
        public GameObject CameraPlayer, CameraNPC;
        public Transform player; 
        public float interactionRange = 3f;
        public AudioSource audio;
       private void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
            //CameraPlayer = GameObject.FindGameObjectWithTag("PlayerCamera (Left & Right)");
            //dialogBox.SetActive(false); // Esconde a caixa de diálogo no início
        }

        // Update is called once per frame
        void Update()
        {
            if (Vector3.Distance(transform.position, player.position) <= interactionRange)
            {
                // Verifica se a tecla E foi pressionada
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    CameraPlayer.SetActive(false);
                    CameraNPC.SetActive(true);
                    GetComponent<Animator>().SetBool("Cutscene",true);
                    audio.Play();
                }   
            }
        }
    }
}
