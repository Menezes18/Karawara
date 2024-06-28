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

        public void Start()
        {

            eroding = false;
        }


        public void getSkinnedK(GameObject obj, int tipo)
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
                if (skinned != null){
                    switch (tipo)
                    {
                        case 0:
                            StartCoroutine(ErodeObjectDecreasing(skinned));
                            break;
                        case 1:
                            StartCoroutine(ErodeObjectIncreasing(skinned));
                            break;
                    }
                }
            }

        }

        public void getSkinned(GameObject obj, int tipo)
        {
            var skinned = obj.GetComponentInChildren<SkinnedMeshRenderer>();
            switch (tipo)
            {
                case 0:
                    StartCoroutine(ErodeObjectDecreasing(skinned));
                    break;
                case 1:
                    StartCoroutine(ErodeObjectIncreasing(skinned));
                    break;
            }
        }

        public IEnumerator ErodeObjectDecreasing(SkinnedMeshRenderer obj)
        {
            float t = 1;
            while (t > 0)
            {
                t -= erodeRate;
                for (int i = 0; i < obj.materials.Length; i++)
                    obj.materials[i].SetFloat("_Erode", t);
                yield return new WaitForSeconds(erodeRefreshRate);
            }
            eroding = false;
        }

        public IEnumerator ErodeObjectIncreasing(SkinnedMeshRenderer obj)
        {
            float t = 0;
            while (t < 1)
            {
                t += erodeRate;
                for (int i = 0; i < obj.materials.Length; i++)
                    obj.materials[i].SetFloat("_Erode", t);
                yield return new WaitForSeconds(erodeRefreshRate);
            }
            eroding = false;
        }
    }
}
