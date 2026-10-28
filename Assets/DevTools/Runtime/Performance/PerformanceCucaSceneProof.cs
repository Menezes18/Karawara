using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Copies precede timing. Matches also traverses component inventories; native scan cost requires separate qualification.
    internal sealed class PerformanceCucaSceneProof : IDisposable
    {
        const int MaximumComponents = 2048;
        readonly PerformanceCucaSceneSource source;
        readonly PerformanceCucaSceneSource.Kind kind;
        readonly Animator[] animators;
        readonly Camera[] cameras;
        readonly cutSceneFight[] cutscenes;
        readonly DestroyAfterTime[] lifetimes;
        readonly Component[] components, subjects, targetInventory;
        readonly Node[] nodes;
        readonly AnimatorState[] animation;
        readonly Surface[] surfaces;
        readonly Box[] boxes;
        readonly CameraState[] cameraStates;
        readonly CutsceneState[] cutsceneStates;
        readonly MeshShape[] meshShapes;
        readonly ParticleState[] particleStates;
        readonly LightState[] lightStates;
        readonly float[] destroyDelays;
        readonly List<Component> scratch = new List<Component>(MaximumComponents * 2);
        readonly BoxCollider shape, targetShape;
        readonly DamageCollider damage;
        readonly AnimationTrigger lever;
        readonly Camera camera;
        readonly UniversalAdditionalCameraData cameraData;
        readonly AudioListener listener;
        readonly AudioSource audio;
        readonly CutSceneTrigger trigger;
        readonly GameObject target, triggerPlayer;
        readonly float physical, magic, fire, holy;
        readonly bool pet;
        readonly Node targetNode, triggerNode;
        readonly AudioState audioState;
        readonly long revision;
        bool failed, disposed;

        internal static bool CanBind(PerformanceCucaSceneSource source)
        { try { Validate(source); return true; } catch (Exception) { return false; } }
        internal static Component[] Validate(PerformanceCucaSceneSource s)
        {
            Need(s != null && s.enabled && s.gameObject.scene.IsValid(), "Enabled authored Cuca source in a loaded scene required");
            Need(s.animators != null && s.animators.Length > 0 && s.animators.Length <= 32 && s.cameras != null && s.cutscenes != null && s.lifetimes != null, "Bounded explicit Cuca source arrays required");
            var actualAnimators = s.GetComponentsInChildren<Animator>(true);
            Need(SameSet(s.animators, actualAnimators), "Every actual Animator must have one exact declared owner");
            foreach (var animator in s.animators) Need(animator.runtimeAnimatorController != null, "Actual Animator controller required");
            bool dormant = s.kind == PerformanceCucaSceneSource.Kind.DormantCutscene;
            bool geometry = s.kind == PerformanceCucaSceneSource.Kind.NativeGeometry || s.kind == PerformanceCucaSceneSource.Kind.DamageGeometry;
            if (geometry)
            {
                Need(s.animators.Length == 1 && s.animators[0].gameObject == s.gameObject && s.shape != null && s.shape.gameObject == s.gameObject &&
                    s.targetShape == null && s.lever == null && s.camera == null && s.cameraData == null && s.listener == null && s.audio == null && s.trigger == null && EmptyOtherArrays(s), "Exact native geometry root required");
                if (s.kind == PerformanceCucaSceneSource.Kind.DamageGeometry)
                    Need(s.damage != null && s.damage.GetType() == typeof(DamageCollider) && s.damage.gameObject == s.gameObject && s.damage.QaDamageCollider == s.shape &&
                        Nonnegative(s.damage.physicalDamage) && Nonnegative(s.damage.magicDamage) && Nonnegative(s.damage.fireDamage) && Nonnegative(s.damage.holyDamage), "Exact local base DamageCollider and finite damage required");
                else Need(s.damage == null, "Plain geometry cannot own a damage driver");
                Need(s.GetComponentInChildren<MeshRenderer>(true) != null, "Native geometry mesh surface required");
            }
            else if (s.kind == PerformanceCucaSceneSource.Kind.LeverCamera)
            {
                Need(s.animators.Length == 1 && s.animators[0].gameObject == s.gameObject && s.shape == null && s.damage == null && s.audio == null && s.trigger == null && EmptyOtherArrays(s) &&
                    s.lever != null && s.lever.GetType() == typeof(AnimationTrigger) && s.lever.gameObject == s.gameObject && s.lever.animator == s.animators[0] &&
                    s.camera != null && s.camera.gameObject == s.gameObject && s.lever.cam == s.camera && s.cameraData != null && s.cameraData.gameObject == s.gameObject &&
                    (s.listener == null || s.listener.gameObject == s.gameObject), "Exact initialized lever camera/Animator/driver required");
                Need(s.lever.target != null && s.lever.target != s.gameObject && s.lever.target.scene == s.gameObject.scene && s.targetShape != null && s.targetShape.gameObject == s.lever.target, "Exact external lever target collider required");
                foreach (var component in s.lever.target.GetComponents<Component>())
                    Need(component != null && (component.GetType() == typeof(Transform) || component.GetType() == typeof(PerformanceActorLifetimeWitness) || component == s.targetShape), "Lever target contains undeclared behavior/native components");
            }
            else if (dormant)
            {
                Need(!s.gameObject.activeSelf && !s.gameObject.activeInHierarchy && s.shape == null && s.targetShape == null && s.damage == null && s.lever == null && s.camera == null && s.cameraData == null &&
                    s.cameras.Length > 0 && s.cameras.Length <= 16 && s.cutscenes.Length > 0 && s.cutscenes.Length <= 8 && s.lifetimes.Length <= 4 &&
                    s.audio != null && s.audio.gameObject == s.gameObject && !s.audio.isPlaying && s.listener != null && s.listener.gameObject == s.gameObject &&
                    s.trigger != null && s.trigger.GetType() == typeof(CutSceneTrigger) && s.trigger._cutScene == s.gameObject && s.trigger._player != null &&
                    !Within(s, s.trigger) && !Within(s, s.trigger._player.transform) && s.trigger.gameObject.scene == s.gameObject.scene, "Exact dormant cutscene owner/audio/trigger graph required");
                Need(SameSet(s.cameras, s.GetComponentsInChildren<Camera>(true)) && SameSet(s.cutscenes, s.GetComponentsInChildren<cutSceneFight>(true)) && SameSet(s.lifetimes, s.GetComponentsInChildren<DestroyAfterTime>(true)), "Incomplete or duplicate cutscene camera/driver/lifetime inventory");
                foreach (var c in s.cameras) Need(c.GetComponent<Animator>() != null && c.GetComponent<UniversalAdditionalCameraData>() != null && !c.isActiveAndEnabled, "Exact dormant Animator camera required");
                foreach (var lifetime in s.lifetimes) Need(Finite(lifetime.QaDestroyDelay) && lifetime.QaDestroyDelay > 0, "Finite dormant lifetime delay required");
                foreach (var driver in s.cutscenes)
                {
                    Need(driver.GetType() == typeof(cutSceneFight) && driver.GetComponent<Animator>() != null && driver.audio == s.audio && driver.trigger == s.trigger &&
                        driver.cam != null && driver.cam.Length > 0 && driver.cam.Length <= 16 && Unique(driver.cam) &&
                        driver.materialErode != null && driver.materialCUCA != null && driver.materialErode.Length <= 128 && driver.materialCUCA.Length <= 128 &&
                        (driver.clip == null || driver.clip.Length <= 64) && Within(s, driver.CucaDeFato) && Within(s, driver.TainaraStop) && Within(s, driver.TainaraMove) && driver.TainaraStop != driver.TainaraMove, "Malformed dormant cutscene driver references");
                    foreach (var c in driver.cam) Need(Array.IndexOf(s.cameras, c) >= 0, "Cutscene driver references undeclared camera");
                    foreach (var renderer in driver.materialErode) Need(Within(s, renderer), "Cutscene erosion references external/null renderer");
                    foreach (var renderer in driver.materialCUCA) Need(Within(s, renderer), "Cutscene Cuca references external/null renderer");
                }
                foreach (var c in s.cameras)
                {
                    int count = 0; foreach (var driver in s.cutscenes) foreach (var declared in driver.cam) if (declared == c) count++;
                    Need(count == 1, "Each cutscene camera must have one exact production driver");
                }
            }
            else throw new ArgumentException("Unknown Cuca scene source kind");
            var all = s.GetComponentsInChildren<Component>(true);
            int nativeCount = 0;
            foreach (var component in all)
            {
                Need(component != null && Allowed(s, component, geometry, dormant), "Missing script or unexpected Cuca subtree component");
                if (component.GetType() != typeof(PerformanceActorLifetimeWitness)) nativeCount++;
                if (component is MeshFilter filter) Need(filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null, "Native mesh/filter pair incomplete");
                if (component is Renderer renderer)
                {
                    Need(renderer is SkinnedMeshRenderer skin ? skin.sharedMesh != null : renderer is ParticleSystemRenderer ? renderer.GetComponent<ParticleSystem>() != null : renderer.GetComponent<MeshFilter>() != null, "Renderer mesh ownership incomplete");
                    var materials = renderer.sharedMaterials;
                    Need(materials.Length > 0 && materials.Length <= 32, "Bounded material surfaces required");
                    foreach (var material in materials) Need(material != null && material.shader != null, "Missing material/shader surface");
                }
                if (component is BoxCollider box) Need(Finite(box.center) && Finite(box.size) && box.size.x > 0 && box.size.y > 0 && box.size.z > 0, "Finite positive collider geometry required");
                if (component is MeshCollider meshShape)
                {
                    Need(meshShape.sharedMesh != null, "Exact authored collision mesh required");
                    if (s.kind == PerformanceCucaSceneSource.Kind.NativeGeometry)
                    {
                        var physicalFilter = meshShape.GetComponent<MeshFilter>();
                        var physicalRenderer = meshShape.GetComponent<MeshRenderer>();
                        // Unity replaces the rendered mesh during static batching; physics
                        // retains the original collision mesh. Freeze both identities separately.
                        Need(physicalFilter != null && physicalFilter.sharedMesh != null && physicalRenderer != null &&
                            (physicalFilter.sharedMesh == meshShape.sharedMesh || physicalRenderer.isPartOfStaticBatch), "Native collider requires its local rendered mesh or an actual Unity static batch");
                    }
                }
                if (component is ParticleSystem particles) Need(!particles.isPlaying && particles.particleCount == 0 && particles.time == 0 && particles.GetComponent<ParticleSystemRenderer>() != null, "Dormant particle source must be idle with exact renderer");
                if (component is Light light) Need(light.GetComponent<UniversalAdditionalLightData>() != null && Nonnegative(light.intensity) && Nonnegative(light.range), "Exact finite dormant URP light required");
            }
            Need(nativeCount <= MaximumComponents, "Cuca component inventory exceeds reviewed bound");
            return all;
        }
        static bool Allowed(PerformanceCucaSceneSource s, Component component, bool geometry, bool dormant)
        {
            var type = component.GetType();
            if (component == s || type == typeof(PerformanceActorLifetimeWitness) || type == typeof(Transform)) return true;
            if (type == typeof(Animator)) return Array.IndexOf(s.animators, component) >= 0;
            if (geometry) return type == typeof(MeshFilter) || type == typeof(MeshRenderer) || type == typeof(BoxCollider) || component == s.damage ||
                s.kind == PerformanceCucaSceneSource.Kind.NativeGeometry && type == typeof(MeshCollider);
            if (dormant)
                return type == typeof(MeshFilter) || type == typeof(MeshRenderer) || type == typeof(SkinnedMeshRenderer) || type == typeof(BoxCollider) || type == typeof(MeshCollider) ||
                    type == typeof(ParticleSystem) || type == typeof(ParticleSystemRenderer) && component.GetComponent<ParticleSystem>() != null ||
                    type == typeof(Light) || type == typeof(UniversalAdditionalLightData) && component.GetComponent<Light>() != null ||
                    type == typeof(Camera) && Array.IndexOf(s.cameras, component) >= 0 ||
                    type == typeof(UniversalAdditionalCameraData) && Array.IndexOf(s.cameras, component.GetComponent<Camera>()) >= 0 ||
                    component == s.audio || component == s.listener ||
                    type == typeof(cutSceneFight) && Array.IndexOf(s.cutscenes, component) >= 0 ||
                    type == typeof(DestroyAfterTime) && Array.IndexOf(s.lifetimes, component) >= 0;
            return component == s.lever || component == s.camera || component == s.cameraData || component == s.listener;
        }
        internal PerformanceCucaSceneProof(PerformanceCucaSceneSource source, bool bindCallbacks = true)
        {
            var all = Validate(source); this.source = source; kind = source.kind; revision = source.MutationRevision;
            animators = (Animator[])source.animators.Clone(); cameras = (Camera[])source.cameras.Clone(); cutscenes = (cutSceneFight[])source.cutscenes.Clone(); lifetimes = (DestroyAfterTime[])source.lifetimes.Clone();
            shape = Live(source.shape); targetShape = Live(source.targetShape); damage = Live(source.damage); lever = Live(source.lever); camera = Live(source.camera); cameraData = Live(source.cameraData); listener = Live(source.listener); audio = Live(source.audio); trigger = Live(source.trigger);
            var componentList = new List<Component>(all.Length); foreach (var component in all) if (component.GetType() != typeof(PerformanceActorLifetimeWitness)) componentList.Add(component);
            components = componentList.ToArray(); nodes = new Node[components.Length];
            var subjectList = new List<Component>(components.Length);
            foreach (var component in components)
                if (!(component is Transform) && !(component is MeshFilter)) subjectList.Add(component);
            for (int i = 0; i < components.Length; i++) nodes[i] = new Node(components[i]);
            if (damage != null) { physical = damage.physicalDamage; magic = damage.magicDamage; fire = damage.fireDamage; holy = damage.holyDamage; pet = damage.isPet; }
            if (lever != null)
            {
                target = lever.target; targetNode = new Node(target.transform);
                var targetParts = new List<Component>();
                foreach (var component in target.GetComponents<Component>()) if (component.GetType() != typeof(PerformanceActorLifetimeWitness)) targetParts.Add(component);
                targetInventory = targetParts.ToArray();
                subjectList.Add(target.transform); subjectList.Add(targetShape);
            }
            if (trigger != null) { triggerPlayer = trigger._player; triggerNode = new Node(trigger); subjectList.Add(trigger); }
            if (audio != null) audioState = new AudioState(audio);
            animation = new AnimatorState[animators.Length]; for (int i = 0; i < animators.Length; i++) animation[i] = new AnimatorState(animators[i]);
            var renderers = source.GetComponentsInChildren<Renderer>(true); surfaces = new Surface[renderers.Length]; for (int i = 0; i < renderers.Length; i++) surfaces[i] = new Surface(renderers[i]);
            var shapes = source.GetComponentsInChildren<BoxCollider>(true); boxes = new Box[shapes.Length + (targetShape == null ? 0 : 1)];
            for (int i = 0; i < shapes.Length; i++) boxes[i] = new Box(shapes[i]); if (targetShape != null) boxes[boxes.Length - 1] = new Box(targetShape);
            var actualCameras = source.GetComponentsInChildren<Camera>(true); cameraStates = new CameraState[actualCameras.Length]; for (int i = 0; i < actualCameras.Length; i++) cameraStates[i] = new CameraState(actualCameras[i]);
            cutsceneStates = new CutsceneState[cutscenes.Length]; for (int i = 0; i < cutscenes.Length; i++) cutsceneStates[i] = new CutsceneState(cutscenes[i]);
            var meshes = source.GetComponentsInChildren<MeshCollider>(true); meshShapes = new MeshShape[meshes.Length]; for (int i = 0; i < meshes.Length; i++) meshShapes[i] = new MeshShape(meshes[i]);
            var particles = source.GetComponentsInChildren<ParticleSystem>(true); particleStates = new ParticleState[particles.Length]; for (int i = 0; i < particles.Length; i++) particleStates[i] = new ParticleState(particles[i]);
            var lights = source.GetComponentsInChildren<Light>(true); lightStates = new LightState[lights.Length]; for (int i = 0; i < lights.Length; i++) lightStates[i] = new LightState(lights[i]);
            destroyDelays = new float[lifetimes.Length]; for (int i = 0; i < lifetimes.Length; i++) destroyDelays[i] = lifetimes[i].QaDestroyDelay;
            subjects = subjectList.ToArray();
            // Endpoint-only light controls retain structural values without installing
            // source callbacks or competing with the full observer being handed off.
            if (bindCallbacks) source.Bind(this, subjects);
        }
        internal IEnumerable<Component> Subjects() => subjects;
        internal bool Owns(Component subject) { for (int i = 0; i < subjects.Length; i++) if (ReferenceEquals(subjects[i], subject)) return true; return false; }
        internal bool Matches()
        {
            if (disposed || failed) return false;
            // Latched: restoring a changed value does not rescue a sampled route.
            try { failed = !Check(); }
            catch (Exception) { failed = true; }
            return !failed;
        }
        bool Check()
        {
            if (source == null || source.MutationRevision != revision || source.kind != kind || !Same(source.animators, animators) || !Same(source.cameras, cameras) || !Same(source.cutscenes, cutscenes) || !Same(source.lifetimes, lifetimes) ||
                !Ref(source.shape, shape) || !Ref(source.targetShape, targetShape) || !Ref(source.damage, damage) || !Ref(source.lever, lever) || !Ref(source.camera, camera) || !Ref(source.cameraData, cameraData) || !Ref(source.listener, listener) || !Ref(source.audio, audio) || !Ref(source.trigger, trigger)) return false;
            if (kind == PerformanceCucaSceneSource.Kind.DormantCutscene && (source.gameObject.activeSelf || source.gameObject.activeInHierarchy)) return false;
            scratch.Clear(); source.GetComponentsInChildren(true, scratch);
            int index = 0;
            for (int i = 0; i < scratch.Count; i++)
            {
                var item = scratch[i]; if (item != null && item.GetType() == typeof(PerformanceActorLifetimeWitness)) continue;
                if (item == null || index >= components.Length || !ReferenceEquals(item, components[index])) return false;
                index++;
            }
            if (index != components.Length) return false;
            for (int i = 0; i < nodes.Length; i++) if (!nodes[i].Matches()) return false;
            for (int i = 0; i < animation.Length; i++) if (!animation[i].Matches()) return false;
            for (int i = 0; i < surfaces.Length; i++) if (!surfaces[i].Matches()) return false;
            for (int i = 0; i < boxes.Length; i++) if (!boxes[i].Matches()) return false;
            for (int i = 0; i < cameraStates.Length; i++) if (!cameraStates[i].Matches()) return false;
            for (int i = 0; i < cutsceneStates.Length; i++) if (!cutsceneStates[i].Matches()) return false;
            for (int i = 0; i < meshShapes.Length; i++) if (!meshShapes[i].Matches()) return false;
            for (int i = 0; i < particleStates.Length; i++) if (!particleStates[i].Matches()) return false;
            for (int i = 0; i < lightStates.Length; i++) if (!lightStates[i].Matches()) return false;
            for (int i = 0; i < lifetimes.Length; i++) if (lifetimes[i] == null || lifetimes[i].QaDestroyDelay != destroyDelays[i]) return false;
            if (!ReferenceEquals(damage, null) && (damage == null || damage.QaDamageCollider != shape || damage.physicalDamage != physical || damage.magicDamage != magic || damage.fireDamage != fire || damage.holyDamage != holy || damage.isPet != pet)) return false;
            if (!ReferenceEquals(lever, null) && (lever == null || lever.animator != animators[0] || lever.cam != camera || lever.target != target || !targetNode.Matches() || !TargetInventoryMatches())) return false;
            if (!ReferenceEquals(trigger, null) && (trigger == null || !triggerNode.Matches() || trigger._cutScene != source.gameObject || triggerPlayer == null || trigger._player != triggerPlayer)) return false;
            return audioState == null || audioState.Matches();
        }
        bool TargetInventoryMatches()
        {
            if (target == null) return false;
            scratch.Clear(); target.GetComponents(scratch);
            int index = 0;
            for (int i = 0; i < scratch.Count; i++)
            {
                var component = scratch[i];
                if (component != null && component.GetType() == typeof(PerformanceActorLifetimeWitness)) continue;
                if (component == null || index >= targetInventory.Length || !ReferenceEquals(component, targetInventory[index])) return false;
                index++;
            }
            return index == targetInventory.Length;
        }
        public void Dispose() { if (disposed) return; disposed = true; if (!ReferenceEquals(source, null)) source.Unbind(this); }
        sealed class Node
        {
            readonly Component value;
            readonly Transform parent;
            readonly int scene;
            readonly bool self, hierarchy, enabled;
            readonly Behaviour behaviour;
            internal Node(Component value) { this.value = value; parent = value.transform.parent; scene = value.gameObject.scene.handle; self = value.gameObject.activeSelf; hierarchy = value.gameObject.activeInHierarchy; behaviour = value as Behaviour; enabled = behaviour != null && behaviour.enabled; }
            internal bool Matches() => value != null && value.transform.parent == parent && value.gameObject.scene.handle == scene && value.gameObject.activeSelf == self && value.gameObject.activeInHierarchy == hierarchy && (ReferenceEquals(behaviour, null) || behaviour != null && behaviour.enabled == enabled);
        }
        sealed class AnimatorState
        {
            readonly Animator value;
            readonly RuntimeAnimatorController controller;
            readonly Avatar avatar;
            readonly AnimatorUpdateMode mode;
            readonly AnimatorCullingMode culling;
            readonly bool rootMotion;
            readonly float speed;
            internal AnimatorState(Animator value) { this.value = value; controller = value.runtimeAnimatorController; avatar = Live(value.avatar); mode = value.updateMode; culling = value.cullingMode; rootMotion = value.applyRootMotion; speed = value.speed; }
            internal bool Matches() => value != null && controller != null && value.runtimeAnimatorController == controller && Ref(value.avatar, avatar) && value.updateMode == mode && value.cullingMode == culling && value.applyRootMotion == rootMotion && value.speed == speed;
        }
        sealed class Box
        {
            readonly BoxCollider value;
            readonly bool enabled, trigger;
            readonly Vector3 center, size;
            internal Box(BoxCollider value) { this.value = value; enabled = value.enabled; trigger = value.isTrigger; center = value.center; size = value.size; }
            internal bool Matches() => value != null && value.enabled == enabled && value.isTrigger == trigger && value.center == center && value.size == size;
        }
        sealed class Surface
        {
            readonly Renderer value;
            readonly MeshFilter filter;
            readonly SkinnedMeshRenderer skin;
            readonly Mesh mesh;
            readonly Transform bone;
            readonly bool enabled, receive;
            readonly ShadowCastingMode shadows;
            readonly Material[] materials;
            readonly Shader[] shaders;
            readonly Texture[] textures;
            readonly bool[] textured;
            readonly List<Material> scratch = new List<Material>(32);
            internal Surface(Renderer value)
            {
                this.value = value; filter = Live(value.GetComponent<MeshFilter>()); skin = value as SkinnedMeshRenderer;
                mesh = skin != null ? skin.sharedMesh : filter == null ? null : filter.sharedMesh; bone = skin == null ? null : Live(skin.rootBone);
                enabled = value.enabled; receive = value.receiveShadows; shadows = value.shadowCastingMode;
                value.GetSharedMaterials(scratch); materials = scratch.ToArray(); shaders = new Shader[materials.Length]; textures = new Texture[materials.Length]; textured = new bool[materials.Length];
                for (int i = 0; i < materials.Length; i++) { shaders[i] = materials[i].shader; textured[i] = materials[i].HasProperty("_MainTex") || materials[i].HasProperty("_BaseMap"); textures[i] = textured[i] ? Live(materials[i].mainTexture) : null; }
            }
            internal bool Matches()
            {
                if (value == null || ((!ReferenceEquals(filter, null) || !ReferenceEquals(skin, null)) && mesh == null) || value.enabled != enabled || value.receiveShadows != receive || value.shadowCastingMode != shadows ||
                    (!ReferenceEquals(filter, null) && (filter == null || filter.sharedMesh != mesh)) || (!ReferenceEquals(skin, null) && (skin == null || skin.sharedMesh != mesh || !Ref(skin.rootBone, bone)))) return false;
                value.GetSharedMaterials(scratch); if (scratch.Count != materials.Length) return false;
                for (int i = 0; i < materials.Length; i++) if (materials[i] == null || scratch[i] != materials[i] || shaders[i] == null || materials[i].shader != shaders[i] || textured[i] && !Ref(materials[i].mainTexture, textures[i])) return false;
                return true;
            }
        }
        sealed class CameraState
        {
            readonly Camera value;
            readonly UniversalAdditionalCameraData data;
            readonly RenderTexture texture;
            readonly Rect viewport;
            readonly float fov, size, depth, near, far;
            readonly int display, mask, clear, antialiasing, quality, volumeMask;
            readonly bool orthographic, hdr, msaa, dynamicResolution, shadows, post, stopNaN, dithering;
            readonly Color background;
            readonly CameraRenderType type;
            readonly Transform volumeTrigger;
            readonly Camera[] stack;
            internal CameraState(Camera value)
            {
                this.value = value; data = value.GetComponent<UniversalAdditionalCameraData>(); texture = Live(value.targetTexture); viewport = value.rect;
                fov = value.fieldOfView; size = value.orthographicSize; depth = value.depth; near = value.nearClipPlane; far = value.farClipPlane; display = value.targetDisplay; mask = value.cullingMask; clear = (int)value.clearFlags;
                orthographic = value.orthographic; hdr = value.allowHDR; msaa = value.allowMSAA; dynamicResolution = value.allowDynamicResolution; background = value.backgroundColor;
                type = data.renderType; shadows = data.renderShadows; post = data.renderPostProcessing; stopNaN = data.stopNaN; dithering = data.dithering; antialiasing = (int)data.antialiasing; quality = (int)data.antialiasingQuality; volumeMask = data.volumeLayerMask.value; volumeTrigger = Live(data.volumeTrigger);
                stack = type == CameraRenderType.Base && data.cameraStack != null ? data.cameraStack.ToArray() : Array.Empty<Camera>();
            }
            internal bool Matches()
            {
                if (value == null || data == null || !Ref(value.targetTexture, texture) || value.rect != viewport || value.fieldOfView != fov || value.orthographicSize != size || value.depth != depth || value.nearClipPlane != near || value.farClipPlane != far || value.targetDisplay != display || value.cullingMask != mask || (int)value.clearFlags != clear || value.orthographic != orthographic || value.allowHDR != hdr || value.allowMSAA != msaa || value.allowDynamicResolution != dynamicResolution || value.backgroundColor != background ||
                    data.renderType != type || data.renderShadows != shadows || data.renderPostProcessing != post || data.stopNaN != stopNaN || data.dithering != dithering || (int)data.antialiasing != antialiasing || (int)data.antialiasingQuality != quality || data.volumeLayerMask.value != volumeMask || !Ref(data.volumeTrigger, volumeTrigger)) return false;
                if (type != CameraRenderType.Base) return stack.Length == 0;
                var current = data.cameraStack; if (current == null) return stack.Length == 0;
                if (current.Count != stack.Length) return false;
                for (int i = 0; i < stack.Length; i++) if (!Ref(current[i], stack[i])) return false;
                return true;
            }
        }
        sealed class MeshShape
        {
            readonly MeshCollider value;
            readonly Mesh mesh;
            readonly PhysicsMaterial material;
            readonly bool enabled, trigger, convex;
            readonly MeshColliderCookingOptions cooking;
            internal MeshShape(MeshCollider value) { this.value = value; mesh = value.sharedMesh; material = Live(value.sharedMaterial); enabled = value.enabled; trigger = value.isTrigger; convex = value.convex; cooking = value.cookingOptions; }
            internal bool Matches() => value != null && mesh != null && value.sharedMesh == mesh && Ref(value.sharedMaterial, material) && value.enabled == enabled && value.isTrigger == trigger && value.convex == convex && value.cookingOptions == cooking;
        }
        sealed class ParticleState
        {
            readonly ParticleSystem value;
            readonly ParticleSystemRenderer renderer;
            readonly bool paused, stopped, emission, loop, prewarm, playOnAwake, unscaled;
            readonly float duration, speed, time, rateTimeMultiplier, rateDistanceMultiplier;
            readonly int maximum, space, scaling, stop, culling, mode, sort, alignment;
            readonly Mesh[] meshes, scratch;
            readonly CurveState rateTime, rateDistance, lifetime, startSpeed, startSize;
            internal ParticleState(ParticleSystem value)
            {
                this.value = value; renderer = value.GetComponent<ParticleSystemRenderer>();
                var main = value.main; var emit = value.emission;
                paused = value.isPaused; stopped = value.isStopped; time = value.time; emission = emit.enabled;
                loop = main.loop; prewarm = main.prewarm; playOnAwake = main.playOnAwake; unscaled = main.useUnscaledTime; duration = main.duration; speed = main.simulationSpeed;
                maximum = main.maxParticles; space = (int)main.simulationSpace; scaling = (int)main.scalingMode; stop = (int)main.stopAction; culling = (int)main.cullingMode;
                rateTimeMultiplier = emit.rateOverTimeMultiplier; rateDistanceMultiplier = emit.rateOverDistanceMultiplier;
                rateTime = new CurveState(emit.rateOverTime); rateDistance = new CurveState(emit.rateOverDistance); lifetime = new CurveState(main.startLifetime); startSpeed = new CurveState(main.startSpeed); startSize = new CurveState(main.startSize);
                mode = (int)renderer.renderMode; sort = (int)renderer.sortMode; alignment = (int)renderer.alignment;
                meshes = new Mesh[renderer.meshCount]; scratch = new Mesh[meshes.Length]; if (meshes.Length > 0) renderer.GetMeshes(meshes);
            }
            internal bool Matches()
            {
                if (value == null || renderer == null || value.isPlaying || value.particleCount != 0 || value.isPaused != paused || value.isStopped != stopped || value.time != time) return false;
                var main = value.main; var emit = value.emission;
                if (emit.enabled != emission || main.loop != loop || main.prewarm != prewarm || main.playOnAwake != playOnAwake || main.useUnscaledTime != unscaled || main.duration != duration || main.simulationSpeed != speed || main.maxParticles != maximum || (int)main.simulationSpace != space || (int)main.scalingMode != scaling || (int)main.stopAction != stop || (int)main.cullingMode != culling ||
                    emit.rateOverTimeMultiplier != rateTimeMultiplier || emit.rateOverDistanceMultiplier != rateDistanceMultiplier || !rateTime.Matches(emit.rateOverTime) || !rateDistance.Matches(emit.rateOverDistance) || !lifetime.Matches(main.startLifetime) || !startSpeed.Matches(main.startSpeed) || !startSize.Matches(main.startSize) ||
                    (int)renderer.renderMode != mode || (int)renderer.sortMode != sort || (int)renderer.alignment != alignment || renderer.meshCount != meshes.Length) return false;
                if (meshes.Length > 0) { renderer.GetMeshes(scratch); if (!Same(scratch, meshes)) return false; }
                return true;
            }
        }
        sealed class CurveState
        {
            readonly ParticleSystemCurveMode mode;
            readonly float multiplier, min, max;
            readonly AnimationCurve lower, upper;
            readonly Keyframe[] lowerKeys, upperKeys;
            readonly WrapMode lowerPre, lowerPost, upperPre, upperPost;
            internal CurveState(ParticleSystem.MinMaxCurve value)
            {
                mode = value.mode; multiplier = value.curveMultiplier; min = value.constantMin; max = value.constantMax;
                lower = value.curveMin; upper = value.curveMax;
                if (lower != null) { lowerKeys = lower.keys; lowerPre = lower.preWrapMode; lowerPost = lower.postWrapMode; }
                if (upper != null) { upperKeys = upper.keys; upperPre = upper.preWrapMode; upperPost = upper.postWrapMode; }
            }
            internal bool Matches(ParticleSystem.MinMaxCurve value) => value.mode == mode && value.curveMultiplier == multiplier && value.constantMin == min && value.constantMax == max &&
                CurveMatches(value.curveMin, lowerKeys, lowerPre, lowerPost) && CurveMatches(value.curveMax, upperKeys, upperPre, upperPost);
            static bool CurveMatches(AnimationCurve value, Keyframe[] keys, WrapMode pre, WrapMode post)
            {
                if (value == null) return keys == null;
                if (value.length != keys.Length || value.preWrapMode != pre || value.postWrapMode != post) return false;
                for (int i = 0; i < keys.Length; i++)
                {
                    var a = value[i]; var b = keys[i];
                    if (a.time != b.time || a.value != b.value || a.inTangent != b.inTangent || a.outTangent != b.outTangent || a.inWeight != b.inWeight || a.outWeight != b.outWeight || a.weightedMode != b.weightedMode) return false;
                }
                return true;
            }
        }
        sealed class LightState
        {
            readonly Light value;
            readonly UniversalAdditionalLightData data;
            readonly Texture cookie;
            readonly Color color;
            readonly float intensity, range, spot, inner, bounce, temperature, shadowStrength, shadowBias, shadowNormalBias, shadowNear;
            readonly int type, shadows, mask, render, shadowTier, softQuality;
            readonly uint layers, shadowLayers;
            readonly bool useTemperature, pipeline, custom;
            readonly Vector2 cookieSize, cookieOffset;
            internal LightState(Light value)
            {
                this.value = value; data = value.GetComponent<UniversalAdditionalLightData>(); cookie = Live(value.cookie); color = value.color;
                intensity = value.intensity; range = value.range; spot = value.spotAngle; inner = value.innerSpotAngle; bounce = value.bounceIntensity; temperature = value.colorTemperature; useTemperature = value.useColorTemperature;
                shadowStrength = value.shadowStrength; shadowBias = value.shadowBias; shadowNormalBias = value.shadowNormalBias; shadowNear = value.shadowNearPlane;
                type = (int)value.type; shadows = (int)value.shadows; mask = value.cullingMask; render = (int)value.renderMode;
                pipeline = data.usePipelineSettings; custom = data.customShadowLayers; shadowTier = data.additionalLightsShadowResolutionTier; softQuality = (int)data.softShadowQuality; layers = data.renderingLayers; shadowLayers = data.shadowRenderingLayers; cookieSize = data.lightCookieSize; cookieOffset = data.lightCookieOffset;
            }
            internal bool Matches() => value != null && data != null && Ref(value.cookie, cookie) && value.color == color && value.intensity == intensity && value.range == range && value.spotAngle == spot && value.innerSpotAngle == inner && value.bounceIntensity == bounce && value.colorTemperature == temperature && value.useColorTemperature == useTemperature &&
                value.shadowStrength == shadowStrength && value.shadowBias == shadowBias && value.shadowNormalBias == shadowNormalBias && value.shadowNearPlane == shadowNear && (int)value.type == type && (int)value.shadows == shadows && value.cullingMask == mask && (int)value.renderMode == render &&
                data.usePipelineSettings == pipeline && data.customShadowLayers == custom && data.additionalLightsShadowResolutionTier == shadowTier && (int)data.softShadowQuality == softQuality && (uint)data.renderingLayers == layers && (uint)data.shadowRenderingLayers == shadowLayers && data.lightCookieSize == cookieSize && data.lightCookieOffset == cookieOffset;
        }
        sealed class CutsceneState
        {
            readonly cutSceneFight value;
            readonly Camera[] cameras;
            readonly AudioClip[] clips;
            readonly SkinnedMeshRenderer[] erode, cuca;
            readonly AudioSource audio;
            readonly GameObject actual, stopped, moving;
            readonly CutSceneTrigger trigger;
            internal CutsceneState(cutSceneFight value) { this.value = value; cameras = (Camera[])value.cam.Clone(); clips = value.clip == null ? null : (AudioClip[])value.clip.Clone(); erode = (SkinnedMeshRenderer[])value.materialErode.Clone(); cuca = (SkinnedMeshRenderer[])value.materialCUCA.Clone(); audio = value.audio; actual = value.CucaDeFato; stopped = value.TainaraStop; moving = value.TainaraMove; trigger = value.trigger; }
            internal bool Matches() => value != null && Same(value.cam, cameras) && Same(value.clip, clips) && Same(value.materialErode, erode) && Same(value.materialCUCA, cuca) && audio != null && value.audio == audio && actual != null && value.CucaDeFato == actual && stopped != null && value.TainaraStop == stopped && moving != null && value.TainaraMove == moving && trigger != null && value.trigger == trigger;
        }
        sealed class AudioState
        {
            readonly AudioSource value;
            readonly AudioClip clip;
            readonly UnityEngine.Audio.AudioMixerGroup output;
            readonly bool loop, playOnAwake, mute;
            readonly float volume, pitch, spatial;
            internal AudioState(AudioSource value) { this.value = value; clip = Live(value.clip); output = Live(value.outputAudioMixerGroup); loop = value.loop; playOnAwake = value.playOnAwake; mute = value.mute; volume = value.volume; pitch = value.pitch; spatial = value.spatialBlend; }
            internal bool Matches() => value != null && !value.isPlaying && Ref(value.clip, clip) && Ref(value.outputAudioMixerGroup, output) && value.loop == loop && value.playOnAwake == playOnAwake && value.mute == mute && value.volume == volume && value.pitch == pitch && value.spatialBlend == spatial;
        }
        static bool EmptyOtherArrays(PerformanceCucaSceneSource s) => s.cameras.Length == 0 && s.cutscenes.Length == 0 && s.lifetimes.Length == 0;
        static T Live<T>(T value) where T : UnityEngine.Object => value == null ? null : value;
        static bool Ref(UnityEngine.Object actual, UnityEngine.Object frozen) => ReferenceEquals(frozen, null) ? actual == null : frozen != null && ReferenceEquals(actual, frozen);
        static bool Same<T>(T[] actual, T[] frozen) where T : UnityEngine.Object
        { if (actual == null || frozen == null) return actual == null && frozen == null; if (actual.Length != frozen.Length) return false; for (int i = 0; i < actual.Length; i++) if (!Ref(actual[i], frozen[i])) return false; return true; }
        static bool Unique<T>(T[] values) where T : UnityEngine.Object
        { for (int i = 0; i < values.Length; i++) { if (values[i] == null) return false; for (int j = 0; j < i; j++) if (values[j] == values[i]) return false; } return true; }
        static bool SameSet<T>(T[] declared, T[] actual) where T : UnityEngine.Object => declared.Length == actual.Length && Unique(declared) && EachPresent(declared, actual);
        static bool EachPresent<T>(T[] declared, T[] actual) where T : UnityEngine.Object { foreach (var value in declared) if (Array.IndexOf(actual, value) < 0) return false; return true; }
        static bool Within(PerformanceCucaSceneSource s, Component component) => component != null && (component.transform == s.transform || component.transform.IsChildOf(s.transform));
        static bool Within(PerformanceCucaSceneSource s, GameObject value) => value != null && Within(s, value.transform);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Nonnegative(float value) => Finite(value) && value >= 0;
        static void Need(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
    }
#endif
}
