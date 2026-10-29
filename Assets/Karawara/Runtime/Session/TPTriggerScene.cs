using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara
{
    public class TPTriggerScene : MonoBehaviour{
        public string scenename;
        public GameObject scene;
        private void OnTriggerEnter(Collider other){
            if (other.CompareTag("Player")){
                scene.SetActive(true);
                SceneManager.LoadScene(scenename);
            }
        }
    }
}
