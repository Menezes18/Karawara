using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class Teleport : MonoBehaviour
    {
        public PlayerManager player;
        public int esteTP;
        public int proximoTp;
        public int[] QualTp = {0,1,2,3,4};
        void Start()
        {
            player = FindObjectOfType<PlayerManager>();
            proximoTp = 0;
        }

        // Update is called once per frame
        void Update()
        {
            if (PlayerInputManager.instance.playerControls.PlayerActions.Teleport.WasPressedThisFrame())
            {
                proximoTp++;
                if (QualTp[proximoTp] == esteTP)
                    player.transform.position = transform.position;
                
            }
        }
    }
}
