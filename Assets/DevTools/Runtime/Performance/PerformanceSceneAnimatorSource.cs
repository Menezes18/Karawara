using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara.DevTools.Performance
{
    // Authored references identify visuals, never grant global population/creation closure.
    [DisallowMultipleComponent]
    public sealed class PerformanceSceneAnimatorSource : MonoBehaviour
    {
        public enum Kind { NpcPresentation, NativeRigidVisual }
        public Kind kind;
        public Animator[] animators = Array.Empty<Animator>();
        public MonoBehaviour[] presentation = Array.Empty<MonoBehaviour>();
        public NPCDialogue dialogue;
        public UIProximity proximity;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Rigidbody body;
        public BoxCollider shape;
        public int missingScriptCount;
        public Component[] nativeParts = Array.Empty<Component>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        internal bool IsValidSource()
        {
            if (animators == null || animators.Length == 0 || animators.Length > 16 || presentation == null || presentation.Length > 128 || nativeParts == null || nativeParts.Length > 128) return false;
            var actual = GetComponentsInChildren<Animator>(true);
            if (actual.Length != animators.Length || animators[0] != GetComponent<Animator>()) return false;
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null || !Within(animators[i]) || Array.IndexOf(actual, animators[i]) < 0) return false;
                for (int j = 0; j < i; j++) if (animators[i] == animators[j]) return false;
            }
            if (kind == Kind.NpcPresentation)
            {
                if (nativeParts.Length != 0 || missingScriptCount != 0 || dialogue == null || !Within(dialogue) || dialogue.gameObject == gameObject ||
                    proximity == null || proximity.gameObject != gameObject || proximity.GetType() != typeof(UIProximity) ||
                    GetComponentInChildren<SkinnedMeshRenderer>(true) == null || meshFilter != null || meshRenderer != null || body != null || shape != null) return false;
                if (proximity.uiElement == null || !Within(proximity.uiElement.transform) || (proximity.uiProximityText != null && !Within(proximity.uiProximityText)) ||
                    !float.IsFinite(proximity.detectionRadius) || proximity.detectionRadius <= 0) return false;
                for (int i = 0; i < presentation.Length; i++)
                {
                    var item = presentation[i];
                    if (item == null || !Within(item) || !PresentationType(item.GetType())) return false;
                    for (int j = 0; j < i; j++) if (presentation[j] == item) return false;
                }
            }
            else if (kind == Kind.NativeRigidVisual)
            {
                if (animators.Length != 1 || presentation.Length != 0 || dialogue != null || proximity != null || missingScriptCount < 0 || missingScriptCount > 3 ||
                    meshFilter == null || meshFilter.gameObject != gameObject || meshFilter.sharedMesh == null ||
                    meshRenderer == null || meshRenderer.gameObject != gameObject || body == null || body.gameObject != gameObject || shape == null || shape.gameObject != gameObject) return false;
            }
            else return false;
            for (int i = 0; i < nativeParts.Length; i++)
            {
                var item = nativeParts[i];
                if (item == null || item.gameObject == gameObject || !Within(item) || !(item is Transform || item is MeshFilter || item is MeshRenderer || item is BoxCollider)) return false;
                for (int j = 0; j < i; j++) if (nativeParts[j] == item) return false;
            }
            int missing = 0;
            foreach (var item in GetComponentsInChildren<Component>(true))
            {
                if (item == null) { missing++; continue; }
                if (item == this || item.GetType() == typeof(PerformanceActorLifetimeWitness)) continue;
                if (item is MonoBehaviour script)
                {
                    if (kind != Kind.NpcPresentation || (script != proximity && script != dialogue && Array.IndexOf(presentation, script) < 0)) return false;
                }
                else if (kind == Kind.NativeRigidVisual)
                {
                    if (item != transform && item != animators[0] && item != meshFilter && item != meshRenderer && item != body && item != shape && Array.IndexOf(nativeParts, item) < 0) return false;
                }
                else if (!(item is Transform || item is Animator || item is SkinnedMeshRenderer || item is MeshFilter || item is MeshRenderer || item is Canvas || item is CanvasRenderer || item is CanvasGroup)) return false;
            }
            return missing == missingScriptCount;
        }
        bool Within(Component item) => item.transform == transform || item.transform.IsChildOf(transform);
        static bool PresentationType(Type type) => type == typeof(CanvasScaler) || type == typeof(GraphicRaycaster) || type == typeof(Image) || type == typeof(Button) || type == typeof(TextMeshProUGUI);
        internal FrozenSource Freeze() => new FrozenSource(this);
        // Only the proximity driver belongs to this visual's lifecycle owner. NPCDialogue
        // is separately bound; UGUI inventory does not pretend to prove UI transitions.
        internal MonoBehaviour[] DriverSubjects() => proximity == null ? Array.Empty<MonoBehaviour>() : new MonoBehaviour[] { proximity };
        void OnEnable() => PerformanceActorLifecycleScope.Notify(this, PerformanceActorLifecycle.Enabled);
        void OnDisable() => PerformanceActorLifecycleScope.Notify(this, PerformanceActorLifecycle.Disabled);
        void OnDestroy() => PerformanceActorLifecycleScope.Notify(this, PerformanceActorLifecycle.Destroyed);

        internal sealed class FrozenSource
        {
            readonly PerformanceSceneAnimatorSource source;
            readonly Kind kind;
            readonly Animator[] animators;
            readonly Component[] nativeParts;
            readonly FrozenBox[] boxes;
            readonly MonoBehaviour[] presentation;
            readonly NPCDialogue dialogue;
            readonly UIProximity proximity;
            readonly MeshFilter filter;
            readonly MeshRenderer renderer;
            readonly Rigidbody body;
            readonly BoxCollider shape;
            readonly Mesh mesh;
            readonly int missing;
            readonly VisualSurface[] surfaces;
            readonly Component[] components;
            readonly Transform[] parents;
            readonly List<Component> observed;
            readonly List<Material> materialScratch = new List<Material>(16);
            readonly Material[] materials;
            readonly Texture[] textures;
            readonly Shader[] shaders;
            readonly bool rendererEnabled, colliderEnabled, trigger, kinematic, gravity, receiveShadows;
            readonly UnityEngine.Rendering.ShadowCastingMode shadows;
            readonly RigidbodyConstraints constraints;
            readonly float mass, detection;
            readonly Vector3 center, size;
            readonly GameObject ui;
            readonly TextMeshProUGUI text;
            readonly string message;
            internal FrozenSource(PerformanceSceneAnimatorSource source)
            {
                if (!source.IsValidSource()) throw new ArgumentException("Malformed authored scene Animator source");
                this.source = source; kind = source.kind; missing = source.missingScriptCount;
                animators = (Animator[])source.animators.Clone(); nativeParts = (Component[])source.nativeParts.Clone(); presentation = (MonoBehaviour[])source.presentation.Clone();
                // Imported serialized empty Component fields can be Editor fake-null wrappers.
                // Normalize absent optional fields now; retain real objects so destruction still fails.
                dialogue = source.dialogue == null ? null : source.dialogue; proximity = source.proximity == null ? null : source.proximity;
                filter = source.meshFilter == null ? null : source.meshFilter; renderer = source.meshRenderer == null ? null : source.meshRenderer;
                body = source.body == null ? null : source.body; shape = source.shape == null ? null : source.shape;
                var initial = new List<Component>(); source.GetComponentsInChildren(true, initial);
                initial.RemoveAll(c => c != null && c.GetType() == typeof(PerformanceActorLifetimeWitness));
                if (initial.Count > 512) throw new ArgumentException("Scene visual component bound exceeded");
                for (int i = 0; i < initial.Count; i++) if (initial[i] == null) initial[i] = null;
                components = initial.ToArray(); parents = new Transform[components.Length];
                for (int i = 0; i < components.Length; i++) if (components[i] != null) parents[i] = components[i].transform.parent;
                observed = new List<Component>(components.Length + 64);
                var renderers = source.GetComponentsInChildren<Renderer>(true);
                surfaces = new VisualSurface[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) surfaces[i] = new VisualSurface(renderers[i]);
                var colliders = source.GetComponentsInChildren<BoxCollider>(true); boxes = new FrozenBox[colliders.Length];
                for (int i = 0; i < colliders.Length; i++) boxes[i] = new FrozenBox(colliders[i]);
                if (proximity != null) { ui = proximity.uiElement; text = proximity.uiProximityText == null ? null : proximity.uiProximityText; detection = proximity.detectionRadius; message = proximity.textGUI; }
                if (renderer != null)
                {
                    mesh = filter.sharedMesh; rendererEnabled = renderer.enabled; receiveShadows = renderer.receiveShadows; shadows = renderer.shadowCastingMode;
                    colliderEnabled = shape.enabled; trigger = shape.isTrigger; center = shape.center; size = shape.size;
                    kinematic = body.isKinematic; gravity = body.useGravity; constraints = body.constraints; mass = body.mass;
                    renderer.GetSharedMaterials(materialScratch); materials = materialScratch.ToArray(); textures = new Texture[materials.Length]; shaders = new Shader[materials.Length];
                    for (int i = 0; i < materials.Length; i++) if (materials[i] != null) { textures[i] = materials[i].mainTexture; shaders[i] = materials[i].shader; }
                }
            }
            internal IEnumerable<Component> Subjects()
            { for (int i = 0; i < surfaces.Length; i++) yield return surfaces[i].Subject; for (int i = 0; i < boxes.Length; i++) yield return boxes[i].Subject; }
            internal bool Owns(Component subject)
            { for (int i = 0; i < surfaces.Length; i++) if (ReferenceEquals(surfaces[i].Subject, subject)) return true; for (int i = 0; i < boxes.Length; i++) if (ReferenceEquals(boxes[i].Subject, subject)) return true; return false; }
            internal bool Matches()
            {
                if (source == null || source.kind != kind || source.missingScriptCount != missing || !Same(source.animators, animators) || !Same(source.presentation, presentation) || !Same(source.nativeParts, nativeParts) ||
                    source.dialogue != dialogue || source.proximity != proximity || source.meshFilter != filter || source.meshRenderer != renderer || source.body != body || source.shape != shape) return false;
                source.GetComponentsInChildren(true, observed);
                int offset = 0;
                for (int i = 0; i < observed.Count; i++)
                {
                    var current = observed[i]; if (current == null) current = null;
                    if (current != null && current.GetType() == typeof(PerformanceActorLifetimeWitness)) continue;
                    if (offset >= components.Length || !ReferenceEquals(current, components[offset])) return false;
                    if (current != null && current.transform.parent != parents[offset]) return false;
                    // A Unity-destroyed object must not compare equal to an authored null script.
                    if (!ReferenceEquals(current, null) && current == null) return false;
                    offset++;
                }
                if (offset != components.Length) return false;
                for (int i = 0; i < surfaces.Length; i++) if (!surfaces[i].Matches()) return false;
                for (int i = 0; i < boxes.Length; i++) if (!boxes[i].Matches()) return false;
                if (!ReferenceEquals(proximity, null) && (proximity == null || dialogue == null || proximity.uiElement != ui || ui == null || proximity.uiProximityText != text || (!ReferenceEquals(text, null) && text == null) || proximity.detectionRadius != detection || proximity.textGUI != message)) return false;
                if (!ReferenceEquals(renderer, null))
                {
                    if (renderer == null || filter == null || body == null || shape == null || mesh == null || filter.sharedMesh != mesh || renderer.enabled != rendererEnabled || renderer.receiveShadows != receiveShadows || renderer.shadowCastingMode != shadows ||
                        shape.enabled != colliderEnabled || shape.isTrigger != trigger || shape.center != center || shape.size != size || body.isKinematic != kinematic || body.useGravity != gravity || body.constraints != constraints || body.mass != mass) return false;
                    renderer.GetSharedMaterials(materialScratch);
                    if (materialScratch.Count != materials.Length) return false;
                    for (int i = 0; i < materials.Length; i++) if (materialScratch[i] != materials[i] || (!ReferenceEquals(materials[i], null) && (materials[i] == null || materials[i].mainTexture != textures[i] || materials[i].shader != shaders[i]))) return false;
                }
                return true;
            }
            sealed class FrozenBox
            {
                readonly BoxCollider shape;
                readonly bool enabled, trigger, activeSelf, activeHierarchy;
                readonly Vector3 center, size;
                internal BoxCollider Subject => shape;
                internal FrozenBox(BoxCollider shape) { this.shape = shape; enabled = shape.enabled; trigger = shape.isTrigger; activeSelf = shape.gameObject.activeSelf; activeHierarchy = shape.gameObject.activeInHierarchy; center = shape.center; size = shape.size; }
                internal bool Matches() => shape != null && shape.enabled == enabled && shape.isTrigger == trigger && shape.gameObject.activeSelf == activeSelf && shape.gameObject.activeInHierarchy == activeHierarchy && shape.center == center && shape.size == size;
            }
            sealed class VisualSurface
            {
                readonly Renderer renderer;
                readonly SkinnedMeshRenderer skinned;
                readonly MeshFilter filter;
                readonly Mesh mesh;
                readonly Material[] materials;
                readonly Shader[] shaders;
                readonly Texture[] textures;
                readonly bool[] hasTexture;
                readonly bool enabled, receive, activeSelf, activeHierarchy;
                internal Renderer Subject => renderer;
                readonly UnityEngine.Rendering.ShadowCastingMode shadows;
                readonly List<Material> scratch;
                internal VisualSurface(Renderer renderer)
                {
                    this.renderer = renderer; skinned = renderer as SkinnedMeshRenderer; var actualFilter = renderer.GetComponent<MeshFilter>(); filter = actualFilter == null ? null : actualFilter;
                    mesh = skinned != null ? skinned.sharedMesh : filter == null ? null : filter.sharedMesh;
                    enabled = renderer.enabled; activeSelf = renderer.gameObject.activeSelf; activeHierarchy = renderer.gameObject.activeInHierarchy; receive = renderer.receiveShadows; shadows = renderer.shadowCastingMode;
                    scratch = new List<Material>(16); renderer.GetSharedMaterials(scratch); materials = scratch.ToArray();
                    shaders = new Shader[materials.Length]; textures = new Texture[materials.Length]; hasTexture = new bool[materials.Length];
                    for (int i = 0; i < materials.Length; i++) if (materials[i] != null)
                    { shaders[i] = materials[i].shader; hasTexture[i] = materials[i].HasProperty("_MainTex") || materials[i].HasProperty("_BaseMap"); if (hasTexture[i]) textures[i] = materials[i].mainTexture; }
                }
                internal bool Matches()
                {
                    if (renderer == null || renderer.gameObject.activeSelf != activeSelf || renderer.gameObject.activeInHierarchy != activeHierarchy || renderer.enabled != enabled || renderer.receiveShadows != receive || renderer.shadowCastingMode != shadows ||
                        (!ReferenceEquals(skinned, null) && (skinned == null || skinned.sharedMesh != mesh)) ||
                        (!ReferenceEquals(filter, null) && (filter == null || filter.sharedMesh != mesh)) || (!ReferenceEquals(mesh, null) && mesh == null)) return false;
                    renderer.GetSharedMaterials(scratch); if (scratch.Count != materials.Length) return false;
                    for (int i = 0; i < materials.Length; i++) if (scratch[i] != materials[i] ||
                        (!ReferenceEquals(materials[i], null) && (materials[i] == null || materials[i].shader != shaders[i] || (hasTexture[i] && materials[i].mainTexture != textures[i])))) return false;
                    return true;
                }
            }
            static bool Same<T>(T[] current, T[] frozen) where T : UnityEngine.Object
            {
                if (current == null || current.Length != frozen.Length) return false;
                for (int i = 0; i < current.Length; i++) if (!ReferenceEquals(current[i], frozen[i]) || current[i] == null) return false;
                return true;
            }
        }
#endif
    }
}
