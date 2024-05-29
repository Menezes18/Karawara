using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class Falling : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            StartCoroutine(faller());
        }

        IEnumerator faller()
        {
            yield return new WaitForSeconds(3f);
            Vector3 newPosition = transform.position;
            newPosition.y -= Time.deltaTime * 3;
            transform.position = newPosition;
            yield return null;
        }
    }
}
