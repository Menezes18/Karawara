using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class Presentation : MonoBehaviour
    {
        public GameObject[] _skills;
        public List<GameObject> _objectsToDetach = new List<GameObject>();
        public Spirit_Summon _spirit_Summon;
        void Start()
        {
            
        }
        IEnumerator Detach()
        {
            yield return new WaitForSeconds(1f);

            for (int i=0; i < _objectsToDetach.Count; i++)
            {   
                _objectsToDetach[i].transform.parent = null;
                Destroy(_objectsToDetach[i], 1f);
            }
        
        }
        // Update is called once per frame
        void Update()
        {
            if(Keyboard.current.digit1Key.wasReleasedThisFrame)
            {
                if(_skills[0] != null)
                {
                    Instantiate(_skills[0]);
                    //_spirit_Summon.Summon();
                    StartCoroutine("Detach");
                }
            }
        }
    }
}
