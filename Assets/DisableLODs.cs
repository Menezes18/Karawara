using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class DisableLODs : MonoBehaviour
    {
        void Start()
        {
            // Obtemos todos os LODGroups na cena
            LODGroup[] lodGroups = FindObjectsOfType<LODGroup>();
        
            // Para cada LODGroup, definimos o primeiro LOD como ativo
            foreach (LODGroup lodGroup in lodGroups)
            {
                LOD[] lods = lodGroup.GetLODs();
                if (lods.Length > 0)
                {
                    // Define o primeiro LOD como sempre visível
                    lodGroup.SetLODs(new LOD[] { lods[0] });
                }
            }
        }
    }
}
