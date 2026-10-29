using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class GateActivator : MonoBehaviour
    {
        private System.IDisposable cutsceneLease;
        private void OnDisable() { cutsceneLease?.Dispose(); cutsceneLease = null; }
        public Lever[] levers; // Array of levers to interact with
        public GameObject gate; // Reference to the gate GameObject
        public bool PortaoAtivo = false;
        public bool trueorfalse;
        public GameObject cameraAtivar;

        public CutScenePortao cutScenePortao;
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
                gate.SetActive(trueorfalse);
                
                PortaoAtivo = true;
                Debug.Log("Gate activated!");
            }
        }
        
        void StartCutscene()
        {
            if (cutsceneLease == null && PlayerInputManager.instance?.Pause != null)
                cutsceneLease = PlayerInputManager.instance.Pause.Acquire(Karawara.Core.Input.InputBlockReason.Cutscene);

            cameraAtivar.SetActive(true);
            
        }

        void EndCutscene()
        {
            cutsceneLease?.Dispose(); cutsceneLease = null;
            cameraAtivar.SetActive(false);

            
        }
    }
}
