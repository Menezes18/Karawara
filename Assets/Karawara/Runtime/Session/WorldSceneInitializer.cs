using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace RPGKarawara
{
    /// <summary>Optional scene content preparation; persistence only exposes its legacy definition.</summary>
    public sealed class WorldSceneInitializer : MonoBehaviour
    {
        private AssetData definition;
        public void Configure(AssetData value) => definition = value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        public GameObject QaLoadedWorldObject { get; private set; }
        public AssetData QaLoadedWorldAsset { get; private set; }
        public GameObject QaLoadedWorldPrefab { get; private set; }
        public Texture2D QaLoadedWorldTexture { get; private set; }
        public string QaLoadedWorldTexturePath { get; private set; }
        public Vector3 QaLoadedWorldPosition { get; private set; }
        public bool QaWorldTextureApplied { get; private set; }
#endif
        public async Task PrepareAsync(AssetData value, CancellationToken cancellationToken)
        {
            definition = value;
            if (definition == null || string.IsNullOrEmpty(definition.texturePath)) return;
            bool finished = false;
            var operation = StartCoroutine(PrepareAndSignal(() => finished = true));
            float deadline = Time.realtimeSinceStartup + 30f;
            try
            {
                while (!finished)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("World asset preparation timed out.");
                    await Task.Yield();
                }
            }
            finally { if (!finished && operation != null) StopCoroutine(operation); }
        }
        private IEnumerator PrepareAndSignal(Action complete) { yield return Prepare(); complete(); }
        public IEnumerator Prepare()
        {
            if (definition != null && !string.IsNullOrEmpty(definition.texturePath))
                yield return InstantiatePrefabWithTextureAsync(definition.prefab, definition.texturePath);
        }
        public IEnumerator InstantiatePrefabWithTextureAsync(GameObject prefab, string texturePath)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            QaWorldTextureApplied = false;
            QaLoadedWorldTexture = null;
            QaLoadedWorldAsset = definition;
            QaLoadedWorldPrefab = prefab;
            QaLoadedWorldTexturePath = texturePath;
            QaLoadedWorldPosition = definition == null ? Vector3.zero : definition.position;
#endif
            if (prefab == null)
            {
                Debug.LogWarning("Prefab nÃ£o encontrado.");
                yield break;
            }

            GameObject instantiatedObject = Instantiate(prefab, definition.position, Quaternion.identity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            QaLoadedWorldObject = instantiatedObject;
            DevTools.Performance.PerformanceActorLifecycleScope.Notify(instantiatedObject.transform, DevTools.Performance.PerformanceActorLifecycle.Enabled);
#endif
            Debug.Log("Prefab instanciado: " + instantiatedObject.name);
            ResourceRequest resourceRequest = Resources.LoadAsync<Texture2D>(texturePath);
            yield return resourceRequest;
            if (resourceRequest.asset == null)
            {
                Debug.LogError($"Falha ao carregar o recurso: {texturePath}. Verifique se o caminho estÃ¡ correto e o arquivo estÃ¡ na pasta Resources.");
                yield break;
            }

            yield return StartCoroutine(ApplyTextureToObject(instantiatedObject, resourceRequest.asset as Texture2D));
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (QaLoadedWorldObject == instantiatedObject && QaLoadedWorldAsset == definition && QaLoadedWorldPrefab == prefab && QaLoadedWorldTexturePath == texturePath)
            {
                QaLoadedWorldTexture = resourceRequest.asset as Texture2D;
                QaWorldTextureApplied = instantiatedObject != null && QaLoadedWorldTexture != null;
            }
#endif

        }

// MÃ©todo para aplicar a textura ao objeto instanciado
        private IEnumerator ApplyTextureToObject(GameObject instantiatedObject, Texture2D texture)
        {
            if (instantiatedObject == null || texture == null)
            {
                Debug.Log("Objeto instanciado ou textura nÃ£o encontrados.");
                yield break;
            }

            // Encontrar todos os Renderers nos filhos do objeto instanciado
            Renderer[] renderers = instantiatedObject.GetComponentsInChildren<Renderer>();
            SkinnedMeshRenderer[] skinnedMeshRenderers = instantiatedObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            // Aplicar textura aos Renderers comuns
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Material material = renderer.sharedMaterial;

                    if (material != null)
                    {
                        material.mainTexture = texture;
                        Debug.Log($"Textura aplicada ao material do Renderer: {renderer.gameObject.name}");
                    }
                    else
                    {
                        Debug.Log($"Material nÃ£o encontrado no Renderer: {renderer.gameObject.name}");
                    }
                }
                else
                {
                    Debug.Log("Renderer nÃ£o encontrado em um dos filhos do prefab.");
                }
            }

            // Aplicar textura aos SkinnedMeshRenderers
            foreach (SkinnedMeshRenderer skinnedRenderer in skinnedMeshRenderers)
            {
                if (skinnedRenderer != null)
                {
                    Material material = skinnedRenderer.sharedMaterial;

                    if (material != null)
                    {
                        material.mainTexture = texture;
                        Debug.Log($"Textura aplicada ao material do SkinnedMeshRenderer: {skinnedRenderer.gameObject.name}");
                    }
                    else
                    {
                        Debug.Log($"Material nÃ£o encontrado no SkinnedMeshRenderer: {skinnedRenderer.gameObject.name}");
                    }
                }
                else
                {
                    Debug.Log("SkinnedMeshRenderer nÃ£o encontrado em um dos filhos do prefab.");
                }
            }
        }

        




    }
}
