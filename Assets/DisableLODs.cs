using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class DisableLODs : MonoBehaviour
    {
        void Start()
            {
               
                LODGroup[] lodGroups = FindObjectsOfType<LODGroup>();
                
                
                foreach (LODGroup lodGroup in lodGroups)
                {
                    LOD[] lods = lodGroup.GetLODs();
                    if (lods.Length > 0)
                    {
                       
                        lodGroup.SetLODs(new LOD[] { lods[0] });
                    }
                }
            }
    }
}
