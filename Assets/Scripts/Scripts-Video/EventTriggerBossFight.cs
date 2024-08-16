using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class EventTriggerBossFight : MonoBehaviour
    {
        [SerializeField] int bossID;

        private void OnTriggerEnter(Collider other)
        {
            AIBossCharacterManager boss = WorldAIManager.instance.GetBossCharacterByID(bossID);

            if (boss != null)
            {
                boss.WakeBoss();
            }
        }
    }
}
