using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class PopUpTutorial : MonoBehaviour
    {
        public GameObject UI;
        public tutorialInfo info;
        public GameObject player;

        void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        public void OnTriggerEnter(Collider other){
            gameObject.GetComponent<BoxCollider>().enabled = false;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            UI.GetComponentInChildren<TMP_Text>().text = info.textoExplicativo;
            UI.GetComponentInChildren<RawImage>().texture = info.video;
            UI.SetActive(true);
        }

        public void close(){
            player.SetActive(true);
            UI.SetActive(false);
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.Locked;
            UI.GetComponentInChildren<TMP_Text>().text = "";
            UI.GetComponentInChildren<RawImage>().texture = null;
        }

    }
}
