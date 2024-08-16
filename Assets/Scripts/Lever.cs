using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara
{
    public class Lever : MonoBehaviour
    {
        public bool isActive;
        public int num;
        public System.Action onActivate; // Action to notify when lever is activated
        public CutScenePortao cutScenePortao;
        private void Start(){
            cutScenePortao = FindObjectOfType<CutScenePortao>();
        }

        public void Activate()
        {
            isActive = !isActive;
            cutScenePortao.StartSequence(num, isActive);
            Debug.Log("Lever activated: " + isActive);
            onActivate?.Invoke(); // Notify that the lever has been activated
        }
    }
}
