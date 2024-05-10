using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera instance;
        public Camera cameraObj;

        private void Awake()
        {
            if(instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(instance);
            }
        }

        
    }
}
