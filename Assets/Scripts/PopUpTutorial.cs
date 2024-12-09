using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RPGKarawara
{
    public class PopUpTutorial : MonoBehaviour
    {
        public GameObject UI;
        public tutorialInfo info;
        public VideoPlayer videoPlayer;
        public bool passou = false;

        void Update(){
            if(passou){
                Destroy(gameObject);
            }

            if(UI.activeSelf == true){
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                if(Time.timeScale > 0){
                    Time.timeScale = 0;
                }
            }
        }

        public void OnTriggerEnter(Collider other){
            gameObject.GetComponent<BoxCollider>().enabled = false;
            Time.timeScale = 0;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            UI.GetComponentInChildren<TMP_Text>().text = info.textoExplicativo;
            UI.GetComponentInChildren<RawImage>().texture = info.texture;
            UI.GetComponentInChildren<Button>().onClick.AddListener(close);
            videoPlayer.clip = info.vClip;
            videoPlayer.targetTexture = info.texture;
            videoPlayer.Play();
            UI.SetActive(true);
        }

        public void close(){
            UI.SetActive(false);
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.Locked;
            UI.GetComponentInChildren<TMP_Text>().text = "";
            UI.GetComponentInChildren<RawImage>().texture = null;
            videoPlayer.clip = null;
            videoPlayer.targetTexture = null;
            passou = true;
        }

    }
}
