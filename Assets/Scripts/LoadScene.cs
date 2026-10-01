using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class LoadScene : MonoBehaviour{
        
        
        public Button _button;
        public GameObject image;

        


        public void LoadSceneGame(){
            image.SetActive(true);
            
            _button.enabled = false;
            
        }
        
        public void LoadGame(){
            image.SetActive(true);

        }

        public void desativado(){
            image.SetActive(false);
        }
    }
}
