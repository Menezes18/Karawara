using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public GameObject impactVFX;
    public List<AudioClip> impactSFX;

    private bool collided;
    
    void OnCollisionEnter (Collision co) 
    {
        if (co.gameObject.tag != "Bullet" && co.gameObject.tag != "Player" && !collided)
        {
            collided = true;

            var trail = GetComponent<DetachGameObjects>();
            if(trail != null)
                trail.Detach();

            if (impactVFX != null)
            {
                ContactPoint contact = co.contacts[0];
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, contact.normal);
                Vector3 pos = contact.point;

                var hitVFX = Instantiate(impactVFX, pos, rot) as GameObject;
                var num = Random.Range (0, impactSFX.Count);

                if (impactSFX.Count >0)
                {
                    var audioSource = hitVFX.GetComponent<AudioSource>();

                    if(audioSource != null)
                        audioSource.PlayOneShot(impactSFX[num]);
                    else
                        Debug.Log("No Audio Source attached to the Hit/Impact");
                }

                Destroy (hitVFX, 2);
            }

            Destroy(gameObject);
        }
    }

    void OnTriggerExit (Collider co)
    {
        if(co.gameObject.tag == "Boundaries")
        {
            Destroy(gameObject);
        }
    }
}
