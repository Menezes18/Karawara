using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class Lever : MonoBehaviour
    {
        public bool isActive;
        public System.Action onActivate; // Action to notify when lever is activated

        public void Activate()
        {
            isActive = true;
            Debug.Log("Lever activated: " + isActive);
            onActivate?.Invoke(); // Notify that the lever has been activated
        }
    }
}
