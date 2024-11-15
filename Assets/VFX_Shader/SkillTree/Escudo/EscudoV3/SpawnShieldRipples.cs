using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace RPGKarawara
{
    public class SpawnShieldRipples : MonoBehaviour
    {
        public GameObject shieldRipples;

        private VisualEffect shieldRipplesVFX;

        private void OnCollisionEnter(Collision co)
        {
            Debug.Log(co.gameObject.tag);
            if(co.gameObject.tag == "Inimigo")
            {
                Debug.Log("Colid");
                var ripples = Instantiate(shieldRipples, transform) as GameObject;
                shieldRipplesVFX = ripples.GetComponent<VisualEffect>();
                shieldRipplesVFX.SetVector3("SphereCenter", co.contacts[0].point);

                Destroy(ripples, 2);
            }
        }
    }
}
