using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Frozen provenance only. This never grants route or population acceptance.
    internal sealed class PerformanceLoadedVisualProof
    {
        internal WorldSaveGameManager Owner { get; }
        readonly AssetData asset;
        readonly GameObject prefab, root;
        readonly Texture2D texture;
        readonly string path;
        readonly Vector3 position;
        readonly bool ownerEnabled, rootActive;
        readonly int scene;
        readonly Component[] components;
        readonly Transform[] parents;
        readonly List<Component> scratch;
        readonly Renderer[] renderers;
        readonly Material[] materials;
        readonly bool[] rendererEnabled;
        readonly MeshFilter[] filters;
        readonly Mesh[] meshes;
        readonly SkinnedMeshRenderer[] skins;
        readonly Mesh[] skinMeshes;

        internal static WorldSaveGameManager FindOwner(GameObject root)
        {
            WorldSaveGameManager found = null;
            foreach (var owner in UnityEngine.Object.FindObjectsByType<WorldSaveGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (owner.QaLoadedWorldObject == root)
                {
                    if (found != null) throw new ArgumentException("Ambiguous loaded visual owner");
                    found = owner;
                }
            return found;
        }
        internal static bool CanBind(WorldSaveGameManager owner)
        {
            if (owner == null || !owner.QaWorldTextureApplied || owner.QaLoadedWorldObject == null || owner.QaLoadedWorldTexture == null ||
                owner.QaLoadedWorldAsset == null || owner.QaLoadedWorldPrefab == null || string.IsNullOrEmpty(owner.QaLoadedWorldTexturePath)) return false;
            var asset = owner.QaCurrentWorldAsset;
            if (asset != owner.QaLoadedWorldAsset || asset.prefab != owner.QaLoadedWorldPrefab || asset.texturePath != owner.QaLoadedWorldTexturePath || asset.position != owner.QaLoadedWorldPosition) return false;
            var root = owner.QaLoadedWorldObject;
            if (!root.scene.IsValid() || root.GetComponent<Animator>() == null) return false;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component == null || (component is MonoBehaviour && !(component is PerformanceActorLifetimeWitness))) return false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || renderers.Length > 256) return false;
            foreach (var renderer in renderers)
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture != owner.QaLoadedWorldTexture) return false;
            return true;
        }
        internal static PerformanceLoadedVisualProof Freeze(WorldSaveGameManager owner)
        {
            if (!CanBind(owner)) throw new ArgumentException("Loaded visual request/application/source incomplete");
            return new PerformanceLoadedVisualProof(owner);
        }
        PerformanceLoadedVisualProof(WorldSaveGameManager owner)
        {
            Owner = owner; asset = owner.QaLoadedWorldAsset; prefab = owner.QaLoadedWorldPrefab;
            root = owner.QaLoadedWorldObject; texture = owner.QaLoadedWorldTexture;
            path = owner.QaLoadedWorldTexturePath; position = owner.QaLoadedWorldPosition;
            ownerEnabled = owner.enabled; rootActive = root.activeInHierarchy; scene = root.scene.handle;
            var initial = new List<Component>(); root.GetComponentsInChildren(true, initial);
            initial.RemoveAll(c => c is PerformanceActorLifetimeWitness);
            if (initial.Count > 512) throw new ArgumentException("Loaded visual component bound exceeded");
            components = initial.ToArray(); parents = new Transform[components.Length];
            for (int i = 0; i < components.Length; i++) parents[i] = components[i].transform.parent;
            scratch = new List<Component>(components.Length + 16);
            renderers = root.GetComponentsInChildren<Renderer>(true);
            materials = new Material[renderers.Length]; rendererEnabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) { materials[i] = renderers[i].sharedMaterial; rendererEnabled[i] = renderers[i].enabled; }
            filters = root.GetComponentsInChildren<MeshFilter>(true); meshes = new Mesh[filters.Length];
            for (int i = 0; i < filters.Length; i++) meshes[i] = filters[i].sharedMesh;
            skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true); skinMeshes = new Mesh[skins.Length];
            for (int i = 0; i < skins.Length; i++) skinMeshes[i] = skins[i].sharedMesh;
        }
        internal bool Matches()
        {
            if (Owner == null || root == null || asset == null || prefab == null || texture == null || !Owner.QaWorldTextureApplied ||
                Owner.enabled != ownerEnabled || root.activeInHierarchy != rootActive || root.scene.handle != scene ||
                Owner.QaLoadedWorldObject != root || Owner.QaCurrentWorldAsset != asset || Owner.QaLoadedWorldAsset != asset || Owner.QaLoadedWorldPrefab != prefab ||
                Owner.QaLoadedWorldTexture != texture || Owner.QaLoadedWorldTexturePath != path || Owner.QaLoadedWorldPosition != position ||
                asset.prefab != prefab || asset.texturePath != path || asset.position != position) return false;
            scratch.Clear(); root.GetComponentsInChildren(true, scratch);
            int index = 0;
            for (int i = 0; i < scratch.Count; i++)
            {
                var item = scratch[i]; if (item is PerformanceActorLifetimeWitness) continue;
                if (item == null || index >= components.Length || components[index] != item || item.transform.parent != parents[index]) return false;
                index++;
            }
            if (index != components.Length) return false;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == null || materials[i] == null || renderers[i].enabled != rendererEnabled[i] || renderers[i].sharedMaterial != materials[i] || materials[i].mainTexture != texture) return false;
            for (int i = 0; i < filters.Length; i++) if (filters[i] == null || filters[i].sharedMesh != meshes[i]) return false;
            for (int i = 0; i < skins.Length; i++) if (skins[i] == null || skins[i].sharedMesh != skinMeshes[i]) return false;
            return true;
        }
    }
#endif
}
