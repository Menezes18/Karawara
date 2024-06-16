using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class NPC : MonoBehaviour
    {
        public GameObject TelaDialogo;
        public Sprite avatar;
        public string[] Dialogo;
        public GameObject CallbackObject = null;
        public string Metodo;
        private Animator corpo_fsm;
        private int index = 0;
        void Start()
        {
            corpo_fsm = transform.GetComponent<Animator>();
        }

        // Update is called once per frame
        void OnTriggerEnter(Collider col)
        {
            if(col.tag == "Player"){
                ActivateCursor();
                index = 0;
                corpo_fsm.SetBool("Falando", true);
                TelaDialogo.SetActive(true);  
                MontarDialogo(); 
                
            }
        }
        void OnTriggerExit(Collider col)
        {
            if(col.tag == "Player"){
                DeactivateCursor();
                corpo_fsm.SetBool("Falando", true);
                TelaDialogo.SetActive(false);   
            }
        }
        void MontarDialogo()
        {
            TelaDialogo.transform.GetChild(1).GetComponent<Text>().text = Dialogo[index];
            TelaDialogo.transform.GetChild(2).GetComponent<Image>().sprite = avatar;
            TelaDialogo.transform.GetChild(3).GetComponent<Button>().onClick.AddListener(btProximo);

        }
        void btProximo(){
            if(index < Dialogo.Length -1)
            {
                index++;
                TelaDialogo.transform.GetChild(1).GetComponent<Text>().text = Dialogo[index];
            }
        }
        // Método para ativar o cursor
        public void ActivateCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // Método para desativar o cursor
        public void DeactivateCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}
