using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class MudarAvatar : MonoBehaviour
    {
        public Animator animatorMudar;
        public float erodeRate = 0.03f;
        public float erodeRefreshRate = 0.01f;
        public float erodeDelay = 1.25f;
        public static MudarAvatar instancia;

        public TransformationsBase tainara;
        public TransformationsBase pantera;
        public TransformationsBase jabuti;

        public bool change = false;
        public bool eroding = true;


        private void Awake()
        {
            instancia = this;
        }

        public void Ativar(int op)
        {
            switch (op)
            {
                case 0:
                    if (pantera.ativo)
                    {
                        getSkinned(pantera.transformacao);
                        pantera.ativo = false;
                    }
                    else if (jabuti.ativo)
                    {
                        getSkinned(jabuti.transformacao);
                        jabuti.ativo = false;
                    }
                    animatorMudar.runtimeAnimatorController = tainara.controller;
                    animatorMudar.avatar = tainara.avatar;
                    getSkinnedK(tainara.transformacao);
                    tainara.ativo = true;
                    break;
                case 1:
                    if (tainara.ativo)
                    {
                        getSkinnedK(tainara.transformacao);
                        tainara.ativo = false;
                    }
                    else if (jabuti.ativo)
                    {
                        getSkinned(jabuti.transformacao);
                        jabuti.ativo = false;
                    }
                    change = true;
                    animatorMudar.runtimeAnimatorController = pantera.controller;
                    animatorMudar.avatar = pantera.avatar;
                    getSkinned(pantera.transformacao);
                    Invoke("canRun", 0.05f);
                    pantera.ativo = true;
                    break;
                case 2:
                    if (tainara.ativo)
                    {
                        getSkinnedK(tainara.transformacao);
                        tainara.ativo = false;
                    }
                    else if (pantera.ativo)
                    {
                        getSkinned(pantera.transformacao);
                        pantera.ativo = false;
                    }
                    animatorMudar.runtimeAnimatorController = jabuti.controller;
                    animatorMudar.avatar = jabuti.avatar;
                    getSkinned(jabuti.transformacao);
                    jabuti.ativo = true;
                    break;
            }
        }

        public void canRun()
        {
            change = false;
        }

        void Update()
        {
            if (Keyboard.current.pKey.wasPressedThisFrame)
            {
                var player = FindObjectOfType<PlayerManager>();

                player.ReviveCharacter();
            }

        }

        public void getSkinnedK(GameObject obj)
        {

            int childcount = obj.transform.childCount;

            GameObject[] childObjects = new GameObject[childcount];

            for (int i = 0; i < childcount; i++)
            {
                childObjects[i] = obj.transform.GetChild(i).gameObject;
            }

            foreach (GameObject child in childObjects)
            {
                var skinned = child.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null)
                    StartCoroutine(ErodeObject(skinned));
            }

        }

        public void getSkinned(GameObject obj)
        {
            var skinned = obj.GetComponentInChildren<SkinnedMeshRenderer>();
            StartCoroutine(ErodeObject(skinned));
        }

        public IEnumerator ErodeObject(SkinnedMeshRenderer obj)
        {
            // yield return new WaitForSeconds(erodeDelay);
            if (obj.material.GetFloat("_Erode") >= 1f)
            {
                eroding = true;
                float t = 1;
                while (t > 0)
                {
                    t -= erodeRate;
                    for (int i = 0; i < obj.materials.Length; i++)
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
                    for (int i = 0; i < obj.materials.Length; i++)
                        obj.materials[i].SetFloat("_Erode", t);
                    yield return new WaitForSeconds(erodeRefreshRate);
                }
            }
            eroding = false;
        }
    }
}
