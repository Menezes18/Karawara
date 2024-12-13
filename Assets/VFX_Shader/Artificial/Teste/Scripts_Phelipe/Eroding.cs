using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Eroding : MonoBehaviour
{
    public float erodeRate = 0.03f;
    public float erodeRefreshRate = 0.01f;
    public float erodeDelay = 1.25f;
    public SkinnedMeshRenderer[] erodeObjects;
    public Animator _animTainara;
    public GameObject Lanca;

    void Start()
    {
        Lanca = GameObject.FindGameObjectWithTag("Lanca");
    }
    void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            
            foreach (SkinnedMeshRenderer erodeObject in erodeObjects)
            {
                if (!erodeObject.gameObject.activeInHierarchy)
                {
                    erodeObject.gameObject.SetActive(true);
                }
                StartCoroutine(ErodeObject(erodeObject));
                Debug.Log(erodeObject.material.GetFloat("_Erode"));
            }
        }
    }

    IEnumerator ErodeObject(SkinnedMeshRenderer erodeObject)
    {
        yield return new WaitForSeconds(erodeDelay);
        _animTainara.enabled = true;
        if (erodeObject.material.GetFloat("_Erode") >= 1f)
        {
            float t = 1f;
            while (t > 0)
            {
                t -= erodeRate;
                for (int i = 0; i < erodeObject.materials.Length; i++)
                {
                    erodeObject.materials[i].SetFloat("_Erode", t);
                    Lanca.GetComponent<MeshRenderer>().material.SetFloat("_Erode", t);
                }
                yield return new WaitForSeconds(erodeRefreshRate);
            }
        }
        else
        {
            float t = 0f;
            while (t < 1f)
            {
                t += erodeRate;
                for (int i = 0; i < erodeObject.materials.Length; i++)
                {
                    erodeObject.materials[i].SetFloat("_Erode", t);
                    //Lanca.GetComponent<MeshRenderer>().material.SetFloat("_Erode", t);
                }
                yield return new WaitForSeconds(erodeRefreshRate);
            }
        }
    }
}
