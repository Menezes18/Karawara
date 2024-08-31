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
        private int _numeroDeSkills;
        void Start()
        {
            _numeroDeSkills = 0;
        }
        // Update is called once per frame
        void Update()
        {
            //ApresentacaoInicial();
            if(Keyboard.current.rightArrowKey.wasReleasedThisFrame)
            {
                PercorrerLista();
                Debug.Log(_numeroDeSkills);
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
            if(_skills[2] != null)
            { 
                CreepActions();
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
        void DestroyAll(GameObject skilAtual)
        {
           for(int i = 0; i < _skills.Length; i++)
            {
                if(_skills[i] != skilAtual && _skills[i].activeInHierarchy)
                {
                    Destroy(_skills[i]);
                }
            } 
        }
        void DestroyCameras()
        {
            Camera[] cameras = Camera.allCameras;

            foreach (Camera cam in cameras)
            {
                if (cam.gameObject.activeInHierarchy && cam.enabled)
                {
                    Destroy(cam);
                }
            }
        }
        void PercorrerLista()
        {
            if(!_camera.activeInHierarchy)Instantiate(_camera);
            DestroyAll(_skills[_numeroDeSkills]);
            Instantiate(_skills[_numeroDeSkills]);
            _numeroDeSkills++;
            if(_numeroDeSkills > _skills.Length)
            {
                _numeroDeSkills = 0;
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
