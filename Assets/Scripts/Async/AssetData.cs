using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "NewAssetData", menuName = "Async/Asset Data")]
    public class AssetData : ScriptableObject
    {
        public string texturePath = "Assets/Data/Robson exportando coisas/ImagensTexturas/Capivara_Cor.png";
        public GameObject prefab; // Referência para o objeto
        public Vector3 position;
    }

}
