using System;
using UnityEngine;

namespace RPGKarawara.DevTools
{
    [Serializable]
    public class DevPlaygroundAsset
    {
        public string label;
        public string path;
        public GameObject prefab;
    }

    [CreateAssetMenu(menuName = "Karawara/Development/Playground Catalog")]
    public class DevPlaygroundCatalog : ScriptableObject
    {
        public GameObject[] managerPrefabs = Array.Empty<GameObject>();
        public GameObject networkManagerPrefab;
        public GameObject playerPrefab;
        public GameObject cameraPrefab;
        public GameObject inputPrefab;
        public GameObject hudPrefab;
        public DevPlaygroundAsset[] enemies = Array.Empty<DevPlaygroundAsset>();
        public DevPlaygroundAsset[] showcase = Array.Empty<DevPlaygroundAsset>();
        public DevPlaygroundAsset[] effects = Array.Empty<DevPlaygroundAsset>();
        public DevPlaygroundAsset[] interactables = Array.Empty<DevPlaygroundAsset>();
        public Skill[] skills = Array.Empty<Skill>();
        public WeaponItem[] weapons = Array.Empty<WeaponItem>();
        public AnimationClip[] animations = Array.Empty<AnimationClip>();
        public Material[] materials = Array.Empty<Material>();
    }
}
