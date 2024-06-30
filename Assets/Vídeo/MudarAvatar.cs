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
        public static MudarAvatar instancia;

        public TransformationsBase tainara;
        public TransformationsBase pantera;
        public TransformationsBase jabuti;
        public GameObject lança;
        
        public float t;

        public bool change = false;

        private void Awake()
        {
            instancia = this;
        }

        public void Ativar(int op)
        {
            if(pantera.eroding || jabuti.eroding || tainara.eroding)return;
            switch (op)
            {
                case 0:
                    if (pantera.ativo)
                    {
                        pantera.eroding = true;
                        pantera.getSkinned(pantera.transformacao,1);
                        pantera.ativo = false;
                    }
                    else if (jabuti.ativo)
                    {
                        jabuti.eroding = true;
                        jabuti.getSkinned(jabuti.transformacao,1);
                        jabuti.ativo = false;
                        t = 0;
                        PlayerInputManager.instance.player.playerLocomotionManager.canDodge = true;
                    }
                    tainara.suporte.enabled = false;
                    animatorMudar.runtimeAnimatorController = tainara.controller;
                    animatorMudar.avatar = tainara.avatar;
                    tainara.eroding = true;
                    tainara.getSkinned(tainara.transformacao,0);
                    lança.SetActive(true);
                    tainara.ativo = true;
                    break;
                case 1:
                    if (tainara.ativo)
                    {
                        tainara.eroding = true;
                        tainara.suporte.enabled = true;
                        tainara.getSkinned(tainara.transformacao,1);
                        lança.SetActive(false);
                        tainara.ativo = false;
                    }
                    else if (jabuti.ativo)
                    {
                        jabuti.eroding = true;
                        jabuti.getSkinned(jabuti.transformacao,1);
                        jabuti.ativo = false;
                        t = 0;
                    }
                    change = true;
                    pantera.eroding = true;
                    pantera.getSkinned(pantera.transformacao,0);
                    animatorMudar.runtimeAnimatorController = pantera.controller;
                    animatorMudar.avatar = pantera.avatar;
                    Invoke("canRun", 0.05f);
                    pantera.ativo = true;
                    break;
                case 2:
                    if (tainara.ativo)
                    {
                        tainara.eroding = true;
                        tainara.suporte.enabled = true;
                        tainara.getSkinned(tainara.transformacao,1);
                        lança.SetActive(false);
                        tainara.ativo = false;
                    }
                    else if (pantera.ativo)
                    {
                        pantera.eroding = true;
                        pantera.getSkinned(pantera.transformacao,1);
                        pantera.ativo = false;
                    }
                    animatorMudar.runtimeAnimatorController = jabuti.controller;
                    animatorMudar.avatar = jabuti.avatar;
                    jabuti.eroding = true;
                    jabuti.getSkinned(jabuti.transformacao,0);
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

            if(jabuti.ativo){
                t+=Time.deltaTime;
                if(t >= 1.3f){
                    Ativar(0);
                    PlayerInputManager.instance.player.playerLocomotionManager.canDodge = true;
                }
            }

        }
    }
}
