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
        private System.IDisposable pauseLease;
        public bool IsShowing => pauseLease != null && UI != null && UI.activeInHierarchy;
        private void OnDisable() { pauseLease?.Dispose(); pauseLease = null; }

        void Update(){
            if(passou){
                Destroy(gameObject);
            }

            if(UI.activeSelf == true && pauseLease != null){
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        public void OnTriggerEnter(Collider other){
            if(other.tag == "Player")
            {
                gameObject.GetComponent<BoxCollider>().enabled = false;
                if (pauseLease == null && PlayerInputManager.instance?.Pause != null)
                    pauseLease = PlayerInputManager.instance.Pause.Acquire(Karawara.Core.Input.InputBlockReason.Pause);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                UI.GetComponentInChildren<TMP_Text>().text = info.textoExplicativo;
                UI.GetComponentInChildren<RawImage>().texture = info.texture;
                UI.GetComponentInChildren<Button>().onClick.AddListener(close);
                if (videoPlayer != null)
                {
                    videoPlayer.clip = info.vClip;
                    videoPlayer.targetTexture = info.texture;
                    videoPlayer.Play();
                }
                UI.SetActive(true);
            }
        }

        public void close(){
            UI.SetActive(false);
            pauseLease?.Dispose(); pauseLease = null;
            PlayerInputManager.instance?.RefreshCursorPolicy();
            UI.GetComponentInChildren<TMP_Text>().text = "";
            UI.GetComponentInChildren<RawImage>().texture = null;
            if (videoPlayer != null)
            {
                videoPlayer.clip = null;
                videoPlayer.targetTexture = null;
            }
            passou = true;
        }

    }
}
