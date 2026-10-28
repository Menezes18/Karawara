using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;
using UnityEngine;
namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceRenderingInventoryValidation
    {
        static Type Contract => typeof(PerformanceProcessHarness).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceRenderingInventory") ?? throw new InvalidOperationException("Rendering inventory contract absent");
        static object Call(string method, params object[] args) { var entry=Contract.GetMethod(method,BindingFlags.Static|BindingFlags.Public); if(entry==null)throw new InvalidOperationException("Missing rendering inventory policy: "+method);return entry.Invoke(null,args); }
        static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static string Inspection()=>new JObject{["schemaVersion"]=1,["holdSeconds"]=120,["releaseToken"]=new string('a',32)}.ToString();
        static PerformanceProcessConfig Config(string mode="cuca-path-inspection")=>new PerformanceProcessConfig{mode=mode,rehearsal=new TutorialRehearsalConfig{durationSeconds=20}};
        static void Policy(PerformanceProcessConfig config,string json=null)=>Call("ValidateRequest",config,json??Inspection());
        static void Reject(Action body){try{body();}catch(TargetInvocationException e)when(e.InnerException is ArgumentException){return;}throw new InvalidOperationException("Invalid inspection accepted");}
        static void WithSyntheticHold(float current,Action<object> body)
        {
            float previous=Time.timeScale;try{
                var hold=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(Contract);var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
                Contract.GetField("ownsHold",flags).SetValue(hold,true);Contract.GetField("priorTimeScale",flags).SetValue(hold,1f);Contract.GetField("<Evidence>k__BackingField",flags).SetValue(hold,new JObject());Time.timeScale=current;body(hold);
            }finally{Time.timeScale=previous;}
        }
        public static string Run()
        {
            var rows=new List<object>();Action<string,Action> test=(name,body)=>{try{body();rows.Add(new{name,passed=true,error=""});}catch(Exception error){rows.Add(new{name,passed=false,error=error.ToString()});}};
            foreach(string mode in new[]{"cuca-path-inspection","tutorial-path-inspection"})test("Accept explicit20s "+mode,()=>Policy(Config(mode)));
            foreach(string mode in new[]{"tutorial-certified-route","tutorial-ui-certified-route","cuca-certified-route","video1-path-inspection","world-retention","skill-retention"})test("Reject timing/unreviewed mode "+mode,()=>Reject(()=>Policy(Config(mode))));
            foreach(string mutation in new[]{"light","capture","retention","skill-retention","profiler","reset-audit","memory-audit","wrong-duration","wrong-schema","hold-zero","hold-long","bad-token","extra-field"})test("Reject mixed/invalid "+mutation,()=>{
                var config=Config();var json=JObject.Parse(Inspection());switch(mutation){
                    case "light":config.measurementArm="light-control";break;case "capture":config.capture=new CaptureConfig();break;
                    case "retention":config.retention=new WorldRetentionConfig();break;case "skill-retention":config.skillRetention=new SkillRetentionConfig();break;
                    case "profiler":config.profilerAttribution=new ProfilerAttributionConfig();break;case "reset-audit":config.rehearsal.auditAiSpawnerReset=true;break;case "memory-audit":config.rehearsal.auditMemorySnapshots=true;break;
                    case "wrong-duration":config.rehearsal.durationSeconds=150;break;case "wrong-schema":json["schemaVersion"]=2;break;case "hold-zero":json["holdSeconds"]=0;break;case "hold-long":json["holdSeconds"]=121;break;case "bad-token":json["releaseToken"]="bad";break;case "extra-field":json["enabled"]=true;break;
                }Reject(()=>Policy(config,json.ToString()));});
            test("Shared renderer surfaces include Player-local EntityIds without cloning",()=>{
                var go=new GameObject("Rendering inventory fixture");Material material=null;Mesh mesh=null;
                try{
                    var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");Need(shader!=null,"Fixture shader absent");material=new Material(shader);mesh=new Mesh{name="fixture mesh"};mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.triangles=new[]{0,1,2};
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{material,material};
                    var before=renderer.sharedMaterials;var row=(JObject)Call("DescribeRenderer",renderer);
                    Need((int)row["renderer"]["instanceId"]==renderer.GetInstanceID()&&(int)row["renderer"]["entityId"]!=0,"Renderer identity missing");
                    Need(row["materials"].Count()==2&&(int)row["materials"][0]["slot"]==0&&(int)row["materials"][1]["slot"]==1,"Ordered material slots lost");
                    Need((int)row["materials"][0]["material"]["instanceId"]==material.GetInstanceID()&&(int)row["materials"][1]["material"]["instanceId"]==material.GetInstanceID(),"Shared material cloned or aliased incorrectly");
                    Need((int)row["mesh"]["instanceId"]==mesh.GetInstanceID()&&renderer.sharedMaterials.SequenceEqual(before),"Mesh identity or shared references changed");
                }finally{UnityEngine.Object.DestroyImmediate(go);if(material!=null)UnityEngine.Object.DestroyImmediate(material);if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);}
            });
            test("Hold requires owned frozen world and exact release token",()=>{Need((bool)Call("ReleaseMatches","abc","abc")&&!(bool)Call("ReleaseMatches","abc","wrong")&&!(bool)Call("ReleaseMatches","abc","abc\n"),"Release token ownership weakened");});
            foreach(int count in new[]{-1,20001})test("Reject renderer bound "+count,()=>Reject(()=>Call("ValidateCount",count,20000)));
            test("Accept inclusive renderer capacity",()=>{Call("ValidateCount",0,20000);Call("ValidateCount",20000,20000);});
            test("Owned held clock restores once",()=>WithSyntheticHold(0,hold=>{Contract.GetMethod("Dispose").Invoke(hold,null);var evidence=(JObject)Contract.GetProperty("Evidence").GetValue(hold);Need(Time.timeScale==1&&(bool)evidence["restored"]&&evidence["ownershipLost"]?.Value<bool>()!=true,"Owned clock restoration failed");}));
            foreach(float foreign in new[]{.5f,1f})test("Foreign clock preserved "+foreign,()=>WithSyntheticHold(foreign,hold=>{Contract.GetMethod("Dispose").Invoke(hold,null);var evidence=(JObject)Contract.GetProperty("Evidence").GetValue(hold);Need(Time.timeScale==foreign&&(bool)evidence["ownershipLost"]&&!(bool)evidence["restored"],"Foreign clock overwritten or falsely claimed restored");}));
            string path=Path.GetFullPath("Docs/Validation/TOBE/Execution/rendering-inventory-fixture-"+Guid.NewGuid().ToString("N")+".json");File.WriteAllText(path,JsonConvert.SerializeObject(new{passed=rows.All(r=>(bool)r.GetType().GetProperty("passed").GetValue(r)),cases=rows,boundary="EditMode policy and synthetic shared-surface descriptor only; no actual Player/frame attribution acceptance"},Formatting.Indented));return path;
        }
    }
}

