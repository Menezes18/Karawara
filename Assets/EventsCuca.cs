using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class EventsCuca : MonoBehaviour
    {
        public GameObject[] VFXPortal;
        public GameObject[] Fire;
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
        public void TPEvent()
        {
            foreach(GameObject effs in VFXPortal)
            {
                effs.SetActive(true);
            }
            Debug.Log("Entrou");
        }
        public void FireEvent()
        {
            foreach(GameObject effs in Fire)
            {
                effs.SetActive(true);
            }
            Debug.Log("Entrou");
        }
    }
}
