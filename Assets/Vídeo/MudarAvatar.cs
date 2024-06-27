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
                        pantera.getSkinned(pantera.transformacao);
                        pantera.ativo = false;
                    }
                    else if (jabuti.ativo)
                    {
                        jabuti.eroding = true;
                        jabuti.getSkinned(jabuti.transformacao);
                        jabuti.ativo = false;
                    }
                    animatorMudar.runtimeAnimatorController = tainara.controller;
                    animatorMudar.avatar = tainara.avatar;
                    tainara.eroding = true;
                    tainara.getSkinnedK(tainara.transformacao);
                    tainara.ativo = true;
                    break;
                case 1:
                    if (tainara.ativo)
                    {
                        tainara.eroding = true;
                        tainara.getSkinnedK(tainara.transformacao);
                        tainara.ativo = false;
                    }
                    else if (jabuti.ativo)
                    {
                        jabuti.eroding = true;
                        jabuti.getSkinned(jabuti.transformacao);
                        jabuti.ativo = false;
                    }
                    change = true;
                    animatorMudar.runtimeAnimatorController = pantera.controller;
                    animatorMudar.avatar = pantera.avatar;
                    pantera.eroding = true;
                    pantera.getSkinned(pantera.transformacao);
                    Invoke("canRun", 0.05f);
                    pantera.ativo = true;
                    break;
                case 2:
                    if (tainara.ativo)
                    {
                        tainara.eroding = true;
                        tainara.getSkinnedK(tainara.transformacao);
                        tainara.ativo = false;
                    }
                    else if (pantera.ativo)
                    {
                        pantera.eroding = true;
                        pantera.getSkinned(pantera.transformacao);
                        pantera.ativo = false;
                    }
                    animatorMudar.runtimeAnimatorController = jabuti.controller;
                    animatorMudar.avatar = jabuti.avatar;
                    jabuti.eroding = true;
                    jabuti.getSkinned(jabuti.transformacao);
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
    }
}
