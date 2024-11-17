using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class EventTriggerBossFight : MonoBehaviour
    {
        [SerializeField] int bossID;
        public GameObject Corrupcao;

        private void OnTriggerEnter(Collider other)
        {
            AIBossCharacterManager boss = WorldAIManager.instance.GetBossCharacterByID(bossID);

            if (boss != null)
            {
                boss.WakeBoss();
                if(Corrupcao != null)
                {
                    ChangeCorrupcao();
                    Corrupcao.GetComponent<Corruption>().dissolveActive = false;
                }
            }
            Destroy(gameObject);
        }
        public void ChangeCorrupcao()
        {
            Corrupcao.SetActive(!Corrupcao.activeInHierarchy);
        }
    }
}
