using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace System.MiniMap
{
    public class DamageBoxDemo : MonoBehaviour
    {

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                SystemMiniMap.ActiveMiniMap.DoHitEffect();
            }
        }
    }
}