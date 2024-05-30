using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class Lever : MonoBehaviour
    {
        public bool isActive;

        public void Activate()
        {
            isActive = !isActive;
            Debug.Log("Lever activated: " + isActive);
        }
    }
}
