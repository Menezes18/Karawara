using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetachGameObjects : MonoBehaviour
{
    public List<GameObject> trails;

    public void Detach ()
    {
        for(int i=0; i<trails.Count; i++)
        {
            trails[i].transform.parent = null;
        }
    }

    public IEnumerator Detach (float delay)
    {
        yield return new WaitForSeconds (delay);
        
        for(int i=0; i<trails.Count; i++)
        {
            trails[i].transform.parent = null;
        }
    }
}
