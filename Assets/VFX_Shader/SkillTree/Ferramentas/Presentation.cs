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
            if(Keyboard.current.digit1Key.wasReleasedThisFrame) //Pantera
            {
                if(_skills[0] != null)
                {
                    Instantiate(_skills[0]);
                    //_spirit_Summon.Summon();
                    StartCoroutine("Detach");
                }
            }
            if(Keyboard.current.digit2Key.wasReleasedThisFrame) //Folhas
            {
               if(_skills[1] != null)
                { 
                    _skills[1].SetActive(true);
                }
            }
            if(Keyboard.current.digit3Key.wasReleasedThisFrame) //Fury
            {
               if(_skills[2] != null)
                { 
                    _skills[2].SetActive(true);
                }
            }
            if(_skills[2] != null)
            {
                
                CreepActions();
            }
        }
        void CreepActions()
        {
            
            if(Keyboard.current.aKey.wasReleasedThisFrame)
            {
                _skills[2].GetComponentInChildren<Animator>().SetFloat("Action",0.9f);
            }
            if(Keyboard.current.rKey.wasReleasedThisFrame)
            {
                _skills[2].GetComponentInChildren<Animator>().SetFloat("Action",2.1f);
            }
            if(!Keyboard.current.rKey.wasReleasedThisFrame && !Keyboard.current.aKey.wasReleasedThisFrame)
            {
                _skills[2].GetComponentInChildren<Animator>().SetFloat("Action",1.5f);
            }
        }
    }
}
