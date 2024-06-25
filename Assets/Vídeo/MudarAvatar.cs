using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class MudarAvatar : MonoBehaviour{
         public float erodeRate = 0.03f;
        public float erodeRefreshRate = 0.01f;
        public float erodeDelay = 1.25f;
        public static MudarAvatar instancia;

        public Animator animatorMudar;
        
        public Avatar avatar1;
        public RuntimeAnimatorController animator1;
        
        public Avatar avatar2; 
        public RuntimeAnimatorController animator2;

        public Avatar jabutiAvatar;
        public RuntimeAnimatorController jabutiAnimator;

        public GameObject persona;
        public GameObject animal;
        public GameObject jabutiGameobject;

        public bool change = false;
        public bool eroding = true;

        public GameObject parentObject;

        private void Awake(){
            instancia = this;
        }

        
        public void TrocarBear(){
            change = true;
            getSkinned(animal);
            animatorMudar.runtimeAnimatorController = animator2;
            animatorMudar.avatar = avatar2;
            Invoke("canRun", 0.05f);
        }

        public void canRun(){
            change = false;
        }

        public void TrocarPlayer(){
            getSkinnedK(persona);
            animatorMudar.runtimeAnimatorController = animator1;
          
            animatorMudar.avatar = avatar1;
        }

        public void TrocarJabuti(){
            change = true;
            getSkinned(jabutiGameobject);
            animatorMudar.runtimeAnimatorController = jabutiAnimator;
          
            animatorMudar.avatar = jabutiAvatar;
        } 
        void Update()
        {
            if (Keyboard.current.pKey.wasPressedThisFrame){
                var player = FindObjectOfType<PlayerManager>();
                
                player.ReviveCharacter();
            }
            
        }

        public void getSkinnedK(GameObject obj){
            
                int childcount = obj.transform.childCount;

                GameObject[] childObjects = new GameObject[childcount];

                for (int i = 0; i < childcount; i++){
                    childObjects[i] = obj.transform.GetChild(i).gameObject;
                }

                foreach (GameObject child in childObjects){
                    var skinned = child.GetComponent<SkinnedMeshRenderer>();
                    if(skinned != null)
                        StartCoroutine(ErodeObject(skinned));
                }
            
        }

        public void getSkinned(GameObject obj){
            var skinned = obj.GetComponentInChildren<SkinnedMeshRenderer>();
            StartCoroutine(ErodeObject(skinned));
        }

        public IEnumerator ErodeObject(SkinnedMeshRenderer obj)
        {
            // yield return new WaitForSeconds(erodeDelay);
            if(obj.material.GetFloat("_Erode") >= 1f){
                eroding = true;
                float t = 1;
                while (t > 0)
                {
                    t -= erodeRate;
                    for(int i = 0; i < obj.materials.Length; i++)
                    obj.materials[i].SetFloat("_Erode", t);
                    yield return new WaitForSeconds(erodeRefreshRate);
                }
            }
            else 
            {
                eroding = true;
                float t = 0;
                while (t < 1)
                {
                    t += erodeRate;
                    for(int i = 0; i < obj.materials.Length; i++)
                    obj.materials[i].SetFloat("_Erode", t);
                    yield return new WaitForSeconds(erodeRefreshRate);
                }
            }
            eroding = false;
        }
    }
}
