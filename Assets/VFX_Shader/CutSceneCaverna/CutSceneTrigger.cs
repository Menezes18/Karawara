using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class CutSceneTrigger : MonoBehaviour
    {
        private Transform _oldPos;
        public GameObject _player,_cutScene;
        //public BossSpawnerInput Input;
        //private bool _active;
        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player");
        }
        void OnTriggerEnter(Collider col)
        {

            if(col.tag == "Player") //&& !_active)
            {
                //Input.SpawnBoss();
                //_active = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this);
#endif
                _cutScene.SetActive(true);
                _player.SetActive(false);
            }
        }
        public void FimCutScene()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this);
#endif

            _cutScene.SetActive(false);
            _player.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
