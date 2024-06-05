using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

namespace RPGKarawara
{
    public class MenuSettings : MonoBehaviour
    {
        public GameObject objectToActivate;
        public GameObject objectToDeactivate;

       
        public Button toggleButton;

        void Start()
        {
            
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(ToggleObjects);
            }
        }

        void ToggleObjects()
        {
            
            if (objectToActivate != null)
            {
                objectToActivate.SetActive(true);
            }

            if (objectToDeactivate != null)
            {
                objectToDeactivate.SetActive(false);
            }
        }
    }
}
