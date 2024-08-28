using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class Presentation : MonoBehaviour
    {
        public GameObject[] _skills;
        public GameObject _camera;
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
            ApresentacaoInicial();
            if(Keyboard.current.rightArrowKey.wasReleasedThisFrame)
            {
                PercorrerLista();
            }
        }
        void ApresentacaoInicial()
        {
             if(Keyboard.current.digit1Key.wasReleasedThisFrame) //Pantera
            {
                if(_skills[0] != null)
                {
                    DesactivateAll(_skills[0]);
                    Instantiate(_skills[0]);
                    //_spirit_Summon.Summon();
                    StartCoroutine("Detach");
                }
            }
            if(Keyboard.current.digit2Key.wasReleasedThisFrame) //Folhas
            {
               if(_skills[1] != null)
                { 
                    DesactivateAll(_skills[1]);
                    _skills[1].SetActive(true);
                }
            }
            if(Keyboard.current.digit3Key.wasReleasedThisFrame) //Fury
            {
               if(_skills[2] != null)
                { 
                    DesactivateAll(_skills[2]);
                    _skills[2].SetActive(true);
                    CreepActions();
                }
            }
            
        }
        void DesactivateAll(GameObject skilAtual)
        {
            for(int i = 0; i < _skills.Length; i++)
            {
                if(_skills[i] != skilAtual)
                {
                    _skills[i].SetActive(false);
                }
            }
        }
        void PercorrerLista()
        {
            Instantiate(_camera);
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
