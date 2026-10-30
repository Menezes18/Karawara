using UnityEngine;
using System.Collections;

namespace RPGKarawara.SkillTree
{
    public class DurationSkill : MonoBehaviour{
        public bool skillRoot = false;
        public bool showFields = true; 
        public float erodeRate = 0.03f;
        public float erodeRefreshRate = 0.01f;
        public float erodeDelay = 1.25f;
        public SkinnedMeshRenderer erodeObject;

        public void Init(float duration)
        {
            Invoke(nameof(DestroyCircle), duration);
        }

        private void DestroyCircle()
        {
            if (erodeObject != null && showFields)
            {
                StartCoroutine(ErodeObject());
            }
            else if (skillRoot){
                SkillRootDestroyed();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SkillRootDestroyed(){
            RootSkill root = GetComponent<RootSkill>();
            root.DestroyAllPrefabsAfterDelay();
            Destroy(gameObject);
        }
        IEnumerator ErodeObject()
        {
            float t = erodeObject.material.GetFloat("_Erode");
            while (t < 1.2)
            {
                t += erodeRate;
                erodeObject.material.SetFloat("_Erode", t);
                yield return new WaitForSeconds(erodeRefreshRate);
            }
            Destroy(gameObject);
        }
    }
}