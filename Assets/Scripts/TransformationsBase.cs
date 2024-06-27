using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara
{
    public class TransformationsBase : MonoBehaviour
    {
        public Avatar avatar;
        public RuntimeAnimatorController controller;
        public GameObject transformacao;
        public bool ativo = false;
        public float erodeRate = 0.03f;
        public float erodeRefreshRate = 0.01f;
        public float erodeDelay = 1.25f;
        public bool eroding = false;

        public void Start(){

            eroding = false;
        }


        public void getSkinnedK(GameObject obj)
        {
            int childcount = obj.transform.childCount;

            GameObject[] childObjects = new GameObject[childcount];

            for (int i = 0; i < childcount; i++)
            {
                childObjects[i] = obj.transform.GetChild(i).gameObject;
            }

            foreach (GameObject child in childObjects)
            {
                var skinned = child.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null)
                    StartCoroutine(ErodeObject(skinned));
            }

        }

        public void getSkinned(GameObject obj)
        {
            var skinned = obj.GetComponentInChildren<SkinnedMeshRenderer>();
            StartCoroutine(ErodeObject(skinned));
        }

        public IEnumerator ErodeObject(SkinnedMeshRenderer obj)
        {
            // yield return new WaitForSeconds(erodeDelay);
            if (obj.material.GetFloat("_Erode") >= 1f)
            {
                float t = 1;
                while (t > 0)
                {
                    t -= erodeRate;
                    for (int i = 0; i < obj.materials.Length; i++)
                        obj.materials[i].SetFloat("_Erode", t);
                    yield return new WaitForSeconds(erodeRefreshRate);
                }
            }
            else
            {
                float t = 0;
                while (t < 1)
                {
                    t += erodeRate;
                    for (int i = 0; i < obj.materials.Length; i++)
                        obj.materials[i].SetFloat("_Erode", t);
                    yield return new WaitForSeconds(erodeRefreshRate);
                }
            }
            yield return new WaitForSeconds(0.3f);
            eroding = false;
        }
    }
}
