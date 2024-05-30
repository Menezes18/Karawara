using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class GateActivator : MonoBehaviour
    {
        public Lever[] levers; // Array of levers to interact with
        public GameObject gate; // Reference to the gate GameObject
        public bool PortaoAtivo = false;
        private void Start()
        {
            foreach (Lever lever in levers)
            {
                lever.onActivate += CheckLevers; // Subscribe to the onActivate event
            }
        }

        private void CheckLevers()
        {
            foreach (Lever lever in levers)
            {
                if (!lever.isActive)
                {
                    return; // If any lever is not active, do nothing
                }
            }

            // If all levers are active, activate the gate
            ActivateGate();
        }

        private void ActivateGate()
        {
            if (gate != null)
            {
                gate.SetActive(true); // Assuming the gate is initially inactive
                PortaoAtivo = true;
                Debug.Log("Gate activated!");
            }
        }
    }
}
