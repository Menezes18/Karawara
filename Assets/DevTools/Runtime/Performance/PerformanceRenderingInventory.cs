using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace RPGKarawara.DevTools.Performance
{
    [Serializable] public sealed class RenderingInspectionConfig { public int schemaVersion, holdSeconds; public string releaseToken; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Explicit standalone diagnostic only. All discovery/serialization occurs outside timing.
    public sealed class PerformanceRenderingInventory : IDisposable
    {
        const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly MethodInfo EntityMethod = typeof(Object).GetMethod("GetEntityId", Instance, null, Type.EmptyTypes, null);
        static readonly FieldInfo EntityRaw = EntityMethod?.ReturnType.GetField("m_Data", Instance);
        readonly PlayerManager player;
        readonly int sceneHandle;
        readonly WorldSaveGameManager world;
        readonly Unity.Netcode.NetworkManager network;
        readonly string saveOverride;
        readonly Camera frozenCamera;
        Vector3 frozenCameraPosition;
        Quaternion frozenCameraRotation;
        Vector3 frozenPosition;
        readonly float priorTimeScale;
        readonly double started;
        readonly int maximumSeconds;
        readonly string token;
        bool ownsHold;
        public JObject Evidence { get; }
        public bool Released { get; private set; }
        PerformanceRenderingInventory(PlayerManager player, RenderingInspectionConfig config)
        {
            world=WorldSaveGameManager.instance;network=Unity.Netcode.NetworkManager.Singleton;saveOverride=world==null?null:world.SaveRootOverride;
            frozenCamera=PlayerCamera.instance==null?null:PlayerCamera.instance.cameraObject;frozenCameraPosition=frozenCamera==null?Vector3.zero:frozenCamera.transform.position;frozenCameraRotation=frozenCamera==null?Quaternion.identity:frozenCamera.transform.rotation;
            this.player=player;sceneHandle=SceneManager.GetActiveScene().handle;frozenPosition=player.transform.position;
            priorTimeScale=Time.timeScale;started=Time.realtimeSinceStartupAsDouble;maximumSeconds=config.holdSeconds;token=config.releaseToken;
            Evidence=new JObject{["schemaVersion"]=1,["timingEligible"]=false,["populationVerified"]=false,["g1bAccepted"]=false,
                ["holdStartedRealtime"]=started,["holdStartedFrame"]=Time.frameCount,["priorTimeScale"]=priorTimeScale,["released"]=false,["restored"]=false,
                ["boundary"]="Explicit paused standalone diagnostic only; inventory ownership is Player-local. No per-renderer draw counts, GPU timing, transient population closure or G1B acceptance inferred."};
        }
        public static void ValidateRequest(PerformanceProcessConfig config,string inspectionJson)
        {
            if(config==null||config.rehearsal==null||(config.mode!="tutorial-path-inspection"&&config.mode!="cuca-path-inspection")||
                (config.measurementArm!=null&&config.measurementArm!="full")||config.capture!=null||config.profilerAttribution!=null||config.retention!=null||config.skillRetention!=null||config.cucaProjectileRetention!=null||
                !string.IsNullOrEmpty(config.certifiedManifestJson)||config.rehearsal.durationSeconds!=20||config.rehearsal.auditAiSpawnerReset||config.rehearsal.auditMemorySnapshots||config.rehearsal.aiSpawnerResetCycles!=1)
                throw new ArgumentException("Rendering inspection requires only explicit20-second Tutorial/Cuca path inspection without timing, light, retention or reset diagnostics.");
            var json=JObject.Parse(inspectionJson,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            string[] fields={"schemaVersion","holdSeconds","releaseToken"};
            if(json.Properties().Any(p=>!fields.Contains(p.Name))||fields.Any(n=>json[n]==null||json[n].Type==JTokenType.Null)||
                json["schemaVersion"].Type!=JTokenType.Integer||(int)json["schemaVersion"]!=1||json["holdSeconds"].Type!=JTokenType.Integer||(int)json["holdSeconds"]!=120||json["releaseToken"].Type!=JTokenType.String||!Guid.TryParseExact((string)json["releaseToken"],"N",out _))
                throw new ArgumentException("Exact rendering inspection schema1, bounded120-second hold and32-hex release token required.");
        }
        public static bool ReleaseMatches(string expected,string actual)=>!string.IsNullOrEmpty(expected)&&string.Equals(expected,actual,StringComparison.Ordinal);
        public static void ValidateCount(int count,int maximum){if(count<0||maximum<1||count>maximum)throw new ArgumentException("Rendering inventory capacity exceeded; no partial/truncated export permitted.");}
        public static JObject Identity(Object value)
        {
            if(value==null)return new JObject{["available"]=false,["instanceId"]=JValue.CreateNull(),["entityIdAvailable"]=false,["entityId"]=JValue.CreateNull()};
            bool available=EntityMethod!=null&&EntityRaw?.FieldType==typeof(int);
            return new JObject{["available"]=true,["instanceId"]=value.GetInstanceID(),["entityIdAvailable"]=available,["entityId"]=available?new JValue((int)EntityRaw.GetValue(EntityMethod.Invoke(value,null))):JValue.CreateNull(),["name"]=Text(value.name),["type"]=value.GetType().FullName};
        }
        static string Text(string text){if(text!=null&&text.Length>4096)throw new ArgumentException("Rendering inventory text capacity exceeded.");return text;}
        static float[] Position(Vector3 value)=>new[]{Finite(value.x),Finite(value.y),Finite(value.z)};
        static float[] Rotation(Quaternion value)=>new[]{Finite(value.x),Finite(value.y),Finite(value.z),Finite(value.w)};
        static float Finite(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Nonfinite rendering inventory value.");return value;}
        static string TransformPath(Transform item){string path=item.name;for(var parent=item.parent;parent!=null;parent=parent.parent){path=parent.name+"/"+path;Text(path);}return path;}
        static Mesh SharedMesh(Renderer renderer)=>renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
        static Mesh[] SharedMeshes(Renderer renderer)
        {
            if(renderer is ParticleSystemRenderer particles){ValidateCount(particles.meshCount,64);var meshes=new Mesh[particles.meshCount];if(meshes.Length>0)particles.GetMeshes(meshes);return meshes;}
            var mesh=SharedMesh(renderer);return mesh==null?Array.Empty<Mesh>():new[]{mesh};
        }
        public static JObject DescribeRenderer(Renderer renderer)
        {
            if(renderer==null)throw new ArgumentException("Live renderer required.");
            var slots=new List<Material>();renderer.GetSharedMaterials(slots);ValidateCount(slots.Count,64);
            var mesh=SharedMesh(renderer);var sharedMeshes=SharedMeshes(renderer);var bounds=renderer.bounds;
            var materials=new JArray();for(int i=0;i<slots.Count;i++)materials.Add(new JObject{["slot"]=i,["material"]=Identity(slots[i]),["shader"]=Identity(slots[i]==null?null:slots[i].shader)});
            return new JObject{["renderer"]=Identity(renderer),["gameObject"]=Identity(renderer.gameObject),["scene"]=renderer.gameObject.scene.name,["sceneHandle"]=(int)renderer.gameObject.scene.handle,["path"]=TransformPath(renderer.transform),
                ["activeSelf"]=renderer.gameObject.activeSelf,["activeInHierarchy"]=renderer.gameObject.activeInHierarchy,["enabled"]=renderer.enabled,["layer"]=renderer.gameObject.layer,["renderingLayerMask"]=renderer.renderingLayerMask,
                ["shadowCastingMode"]=renderer.shadowCastingMode.ToString(),["receiveShadows"]=renderer.receiveShadows,["staticBatch"]=renderer.isPartOfStaticBatch,["hasPropertyBlock"]=renderer.HasPropertyBlock(),["staticBatchInfo"]=Properties(renderer,"subMeshStartIndex","staticBatchIndex"),
                ["boundsCenter"]=JArray.FromObject(Position(bounds.center)),["boundsExtents"]=JArray.FromObject(Position(bounds.extents)),["mesh"]=Identity(mesh),["sharedMeshes"]=new JArray(sharedMeshes.Select(Identity)),["meshSubmeshes"]=mesh==null?JValue.CreateNull():new JValue(mesh.subMeshCount),["meshUnavailableReason"]=mesh==null?"No single MeshFilter or SkinnedMeshRenderer sharedMesh; particle mesh lists are in sharedMeshes and procedural geometry is not inferred.":null,["materials"]=materials};
        }
        static JObject DescribeMaterial(Material material)
        {
            ValidateCount(material.passCount,128);var passes=new JArray();for(int i=0;i<material.passCount;i++){string name=material.GetPassName(i);passes.Add(new JObject{["index"]=i,["name"]=Text(name),["enabled"]=material.GetShaderPassEnabled(name)});}
            var keywords=material.shaderKeywords;ValidateCount(keywords.Length,256);foreach(string keyword in keywords)Text(keyword);
            return new JObject{["material"]=Identity(material),["shader"]=Identity(material.shader),["renderQueue"]=material.renderQueue,["enableInstancing"]=material.enableInstancing,["doubleSidedGI"]=material.doubleSidedGI,["passes"]=passes,["keywords"]=JArray.FromObject(keywords)};
        }
        static JObject Properties(object value,params string[] names)
        {
            var row=new JObject();foreach(string name in names){var property=value?.GetType().GetProperty(name,Instance);if(property==null){row[name]=new JObject{["available"]=false};continue;}object actual=property.GetValue(value);row[name]=actual==null?JValue.CreateNull():actual.GetType().IsEnum?new JValue(actual.ToString()):JToken.FromObject(actual);}return row;
        }
        static JObject CameraRow(Camera camera)
        {
            camera.TryGetComponent<UniversalAdditionalCameraData>(out var urp);
            return new JObject{["camera"]=Identity(camera),["gameObject"]=Identity(camera.gameObject),["path"]=TransformPath(camera.transform),["scene"]=camera.gameObject.scene.name,["enabled"]=camera.enabled,["active"]=camera.gameObject.activeInHierarchy,
                ["position"]=JArray.FromObject(Position(camera.transform.position)),["rotation"]=JArray.FromObject(Rotation(camera.transform.rotation)),["cullingMask"]=camera.cullingMask,["targetDisplay"]=camera.targetDisplay,["depth"]=Finite(camera.depth),["fieldOfView"]=Finite(camera.fieldOfView),["orthographic"]=camera.orthographic,["orthographicSize"]=Finite(camera.orthographicSize),["targetTexture"]=Identity(camera.targetTexture),
                ["urpAvailable"]=urp!=null,["urp"]=Properties(urp,"renderShadows","renderPostProcessing","renderType","requiresDepthTexture","requiresColorTexture")};
        }
        static JObject LightRow(Light light)
        {
            light.TryGetComponent<UniversalAdditionalLightData>(out var urp);
            return new JObject{["light"]=Identity(light),["gameObject"]=Identity(light.gameObject),["path"]=TransformPath(light.transform),["scene"]=light.gameObject.scene.name,["enabled"]=light.enabled,["active"]=light.gameObject.activeInHierarchy,
                ["type"]=light.type.ToString(),["intensity"]=Finite(light.intensity),["range"]=Finite(light.range),["shadows"]=light.shadows.ToString(),["shadowStrength"]=Finite(light.shadowStrength),["cullingMask"]=light.cullingMask,["renderingLayerMask"]=light.renderingLayerMask,
                ["urpAvailable"]=urp!=null,["urp"]=Properties(urp,"shadowRenderingLayers","renderingLayers","usePipelineSettings","additionalLightsShadowResolutionTier","softShadowQuality")};
        }
        JObject Capture()
        {
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.gameObject.scene.IsValid()).ToArray();ValidateCount(renderers.Length,20000);
            var cameras=Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.gameObject.scene.IsValid()).ToArray();ValidateCount(cameras.Length,64);
            var lights=Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(l=>l.gameObject.scene.IsValid()).ToArray();ValidateCount(lights.Length,4096);
            var materials=new HashSet<Material>();var meshes=new HashSet<Mesh>();var rows=new JArray();var scratch=new List<Material>(64);
            long slotCount=0;foreach(var renderer in renderers){rows.Add(DescribeRenderer(renderer));renderer.GetSharedMaterials(scratch);slotCount+=scratch.Count;if(slotCount>200000)throw new ArgumentException("Total rendering slot capacity exceeded.");foreach(var material in scratch)if(material!=null)materials.Add(material);foreach(var mesh in SharedMeshes(renderer))if(mesh!=null)meshes.Add(mesh);}ValidateCount(materials.Count,20000);ValidateCount(meshes.Count,20000);
            var hud=PlayerUIManager.instance?.playerUIHudManager;var pause=hud==null?null:typeof(PlayerUIHudManager).GetField("pauseMenu",Instance)?.GetValue(hud) as GameObject;
            var pipeline=GraphicsSettings.currentRenderPipeline;
            string assemblyPath=typeof(PlayerManager).Assembly.Location;string assemblyHash=null;
            if(!string.IsNullOrEmpty(assemblyPath)&&File.Exists(assemblyPath))using(var sha=SHA256.Create())using(var assemblyStream=File.OpenRead(assemblyPath))assemblyHash=BitConverter.ToString(sha.ComputeHash(assemblyStream)).Replace("-","");
            return new JObject{["schemaVersion"]=1,["timingEligible"]=false,["populationVerified"]=false,["g1bAccepted"]=false,["utc"]=DateTime.UtcNow.ToString("O"),["processId"]=System.Diagnostics.Process.GetCurrentProcess().Id,["playerBuildGuid"]=Application.buildGUID,["assemblyPath"]=assemblyPath,["assemblySha256"]=assemblyHash,["unityVersion"]=Application.unityVersion,["development"]=Debug.isDebugBuild,
                ["scene"]=SceneManager.GetActiveScene().name,["sceneHandle"]=sceneHandle,["frame"]=Time.frameCount,["realtime"]=Time.realtimeSinceStartupAsDouble,["qpc"]=System.Diagnostics.Stopwatch.GetTimestamp(),["qpcFrequency"]=System.Diagnostics.Stopwatch.Frequency,["timeScale"]=Time.timeScale,["applicationFocused"]=Application.isFocused,
                ["player"]=Identity(player),["playerGameObject"]=Identity(player.gameObject),["playerPosition"]=JArray.FromObject(Position(player.transform.position)),["playerRotation"]=JArray.FromObject(Rotation(player.transform.rotation)),["playerHealth"]=player.playerNetworkManager.currentHealth.Value,["playerDead"]=player.isDead.Value,
                ["ui"]=new JObject{["hud"]=Identity(hud),["pausePanel"]=Identity(pause),["pauseActive"]=pause!=null&&pause.activeInHierarchy,["skillPanel"]=Identity(hud==null?null:hud.pauseSkillMenu),["skillActive"]=hud!=null&&hud.pauseSkillMenu!=null&&hud.pauseSkillMenu.activeInHierarchy},
                ["renderers"]=rows,["materials"]=new JArray(materials.Select(DescribeMaterial)),["meshes"]=new JArray(meshes.Select(m=>new JObject{["mesh"]=Identity(m),["subMeshCount"]=m.subMeshCount,["vertexCount"]=m.vertexCount})),["cameras"]=new JArray(cameras.Select(CameraRow)),["lights"]=new JArray(lights.Select(LightRow)),
                ["pipeline"]=Identity(pipeline),["urpSettings"]=Properties(pipeline,"supportsMainLightShadows","supportsAdditionalLightShadows","mainLightShadowmapResolution","additionalLightsShadowmapResolution","shadowDistance","shadowCascadeCount","supportsSoftShadows"),
                ["quality"]=new JObject{["name"]=QualitySettings.names[QualitySettings.GetQualityLevel()],["shadows"]=QualitySettings.shadows.ToString(),["shadowDistance"]=QualitySettings.shadowDistance,["shadowResolution"]=QualitySettings.shadowResolution.ToString(),["shadowCascades"]=QualitySettings.shadowCascades,["shadowProjection"]=QualitySettings.shadowProjection.ToString()},
                ["boundary"]=Evidence["boundary"].DeepClone()};
        }
        public static PerformanceRenderingInventory Begin(PlayerManager player,RenderingInspectionConfig config,string outputRoot,double routeElapsed)
        {
            if(!Application.isPlaying||Application.isEditor||player==null||!player.IsSpawned||!player.IsOwner||player.isDead.Value||player.playerNetworkManager.currentHealth.Value<=0||Time.timeScale!=1||config==null||config.schemaVersion!=1||config.holdSeconds!=120||!Guid.TryParseExact(config.releaseToken,"N",out _)||!Path.IsPathRooted(outputRoot)||!Directory.Exists(outputRoot))throw new InvalidOperationException("Explicit healthy owned standalone20-second completed route required for rendering hold.");
            var hold=new PerformanceRenderingInventory(player,config);
            try
            {
                string id=Guid.NewGuid().ToString("N");string inventory=Path.Combine(outputRoot,"rendering-inventory-"+id+".json"),ready=Path.Combine(outputRoot,"rendering-hold-ready-"+id+".json"),release=Path.Combine(outputRoot,"rendering-hold-release-"+id+".token");
                if(File.Exists(release)||File.Exists(inventory)||File.Exists(ready))throw new IOException("Rendering inspection artifacts must be exclusive.");
                hold.Evidence["releasePath"]=release;hold.Evidence["releaseToken"]=config.releaseToken;hold.Evidence["inventoryPath"]=inventory;hold.Evidence["readyPath"]=ready;hold.Evidence["routeElapsed"]=routeElapsed;
                Time.timeScale=0;hold.ownsHold=true;hold.Evidence["ownedHold"]=true;hold.Evidence["heldTimeScale"]=0;
                // Wait a complete frame after pausing before reading poses/surfaces. Camera LateUpdate
                // may finish the already-started frame using its previous deltaTime.
                return hold;
            }
            catch(Exception error){hold.Dispose();hold.Evidence["error"]=error.ToString();try{WriteNew(Path.Combine(outputRoot,"rendering-hold-failed-"+Guid.NewGuid().ToString("N")+".json"),hold.Evidence);}catch(Exception outputError){Debug.LogError("Rendering hold failure evidence unavailable: "+outputError.Message);}throw;}
        }
        static void WriteNew(string path,JObject data){string json=data.ToString(Formatting.Indented);if(json.Length>64*1024*1024)throw new ArgumentException("Rendering diagnostic JSON exceeds64MiB character capacity; refuse partial export.");using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))using(var writer=new StreamWriter(stream))writer.Write(json);}
        public bool Poll()
        {
            if(Released)return true;
            if(!ownsHold||player==null||Time.timeScale!=0||world==null||world!=WorldSaveGameManager.instance||world.player!=player||world.SaveRootOverride!=saveOverride||network==null||network!=Unity.Netcode.NetworkManager.Singleton||!network.IsHost||!network.IsListening||network.LocalClient?.PlayerObject!=player.NetworkObject||frozenCamera==null||PlayerCamera.instance==null||PlayerCamera.instance.cameraObject!=frozenCamera||!player.IsSpawned||!player.IsOwner||player.isDead.Value||player.playerNetworkManager.currentHealth.Value<=0||SceneManager.GetActiveScene().handle!=sceneHandle)throw new InvalidOperationException("Rendering hold world/player/clock ownership changed.");
            if(Time.realtimeSinceStartupAsDouble-started>maximumSeconds){Evidence["timedOut"]=true;throw new TimeoutException("Rendering inspection hold exceeded120 seconds.");}
            if(Evidence["readyRealtime"]==null)
            {
                if(Time.frameCount<=(int)Evidence["holdStartedFrame"])return false;
                frozenPosition=player.transform.position;frozenCameraPosition=frozenCamera.transform.position;frozenCameraRotation=frozenCamera.transform.rotation;
                var data=Capture();string inventory=(string)Evidence["inventoryPath"];WriteNew(inventory,data);
                using(var sha=SHA256.Create())using(var stream=File.OpenRead(inventory))Evidence["inventorySha256"]=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
                Evidence["inventoryFrame"]=data["frame"].DeepClone();Evidence["readyRealtime"]=Time.realtimeSinceStartupAsDouble;
                if(Time.realtimeSinceStartupAsDouble-started>=maximumSeconds)throw new TimeoutException("Rendering inventory exhausted its hold before readiness.");
                WriteNew((string)Evidence["readyPath"],Evidence);return false;
            }
            if(Vector3.Distance(player.transform.position,frozenPosition)>.001f||Vector3.Distance(frozenCamera.transform.position,frozenCameraPosition)>.001f||Quaternion.Angle(frozenCamera.transform.rotation,frozenCameraRotation)>.01f)throw new InvalidOperationException("Rendering paused Player/camera pose changed after inventory readiness.");
            string release=(string)Evidence["releasePath"];if(!File.Exists(release))return false;
            var info=new FileInfo(release);if(info.Length>128||!ReleaseMatches(token,File.ReadAllText(release)))throw new InvalidOperationException("Rendering hold release token mismatch.");
            Released=true;Evidence["released"]=true;Dispose();return true;
        }
        public void Dispose()
        {
            if(!ownsHold)return;Evidence["observedReleaseTimeScale"]=Time.timeScale;
            bool stillOwned=Time.timeScale==0;Evidence["ownershipLost"]=!stillOwned;
            if(stillOwned)Time.timeScale=priorTimeScale;ownsHold=false;Evidence["ownedHold"]=false;
            Evidence["restored"]=stillOwned&&Time.timeScale==priorTimeScale;Evidence["holdEndedRealtime"]=Time.realtimeSinceStartupAsDouble;Evidence["holdEndedFrame"]=Time.frameCount;
        }
    }
#endif
}









