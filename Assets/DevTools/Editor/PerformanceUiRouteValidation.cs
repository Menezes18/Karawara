using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceUiRouteValidation
    {
        static Type Contract => typeof(Performance.PerformanceProcessHarness).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceUiRouteProcess") ?? throw new InvalidOperationException("UI route coordinator missing");
        static int Phase(double seconds) => (int)Contract.GetMethod("ExpectedPhase").Invoke(null,new object[] {seconds});
        public static void Run()
        {
            var rows = new List<object>();
            Action<string,Action> test = (name,body) => {try {body();rows.Add(new{name,passed=true,error=""});}catch(Exception error){rows.Add(new{name,passed=false,error=error.ToString()});}};
            foreach(var sample in new[]{new[]{0d,0},new[]{30d,0},new[]{59.999,0},new[]{60d,1},new[]{89.999,1},new[]{90d,2},new[]{119.999,2},new[]{120d,3},new[]{150d,3}})
                test("Real-time UI phase at "+sample[0],()=>{if(Phase(sample[0])!=(int)sample[1])throw new InvalidOperationException("Incorrect planned UI phase");});
            test("Unplanned/invalid UI clock rejected",()=>{foreach(double seconds in new[]{double.NaN,double.PositiveInfinity,-1d}) {bool rejected=false;try{Phase(seconds);}catch(TargetInvocationException e) when(e.InnerException is ArgumentException){rejected=true;}if(!rejected)throw new InvalidOperationException("Invalid clock accepted");}});
            test("Exact production toggle sequence",()=>{var method=Contract.GetMethod("ActionForTransition");if(method==null)throw new InvalidOperationException("Typed transition contract missing");for(int i=0;i<3;i++)if((int)method.Invoke(null,new object[]{i})!=(i==0?1:2))throw new InvalidOperationException("Wrong authored toggle");});
            test("Invalid production toggle index rejected",()=>{foreach(int i in new[]{-1,3}) Reject(()=>Contract.GetMethod("ActionForTransition").Invoke(null,new object[]{i}));});
            test("UI manifest accepts stationary neutral route",()=>Parse(Manifest()));
            test("UI manifest freezes owned copies",()=>{var source=Manifest();var frozen=Parse(source);source["miniMapPath"]="changed";var first=frozen.GetType().GetProperty("Snapshot").GetValue(frozen);first.GetType().GetField("miniMapPath").SetValue(first,"mutated");var second=frozen.GetType().GetProperty("Snapshot").GetValue(frozen);if((string)second.GetType().GetField("miniMapPath").GetValue(second)!="MiniMap")throw new InvalidOperationException("Caller mutated frozen manifest");});
            test("UI manifest rejects identities and widened tolerances",()=>{foreach(var pair in new[]{new[]{"routeId","tutorial-exploration-v1"},new[]{"scene","Video1"},new[]{"sourceRevision","bad"},new[]{"buildAssemblySha256",new string('b',64)},new[]{"playerBuildGuid",new string('2',32)},new[]{"distributionSha256",new string('b',64)}}){var m=Manifest();m[pair[0]]=pair[1];Reject(()=>Parse(m));}foreach(string field in new[]{"positionTolerance","cameraAngleTolerance","maxLatenessSeconds","renderScale"}){var m=Manifest();m[field]=2;Reject(()=>Parse(m));}});
            test("UI manifest rejects nonneutral timeline",()=>{string timeline="{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[\"W\"]},{\"timeSeconds\":149,\"keys\":[]}]}";var manifest=Manifest();manifest["timelineSha256"]=Hash(timeline);Reject(()=>Parse(manifest,timeline));});
            test("UI manifest rejects missing fields and invalid pose",()=>{var missing=Manifest();missing.Remove("pausePanelPath");Reject(()=>Parse(missing));var pose=Manifest();pose["initialCameraRotation"]=new JArray(0,0,0,0);Reject(()=>Parse(pose));});
            test("UI manifest rejects duplicate paths and counters",()=>{var m=Manifest();m["cameraPaths"]=new JArray("Camera","Camera");Reject(()=>Parse(m));m=Manifest();m["counterNames"]=new JArray("Main Thread","Main Thread");Reject(()=>Parse(m));});
            test("UI mode never reuses Tutorial movement certification",()=>{var method=typeof(Performance.PerformanceProcessHarness).GetMethod("IsUiRouteMode",BindingFlags.Static|BindingFlags.NonPublic);if(method==null||!(bool)method.Invoke(null,new object[]{"tutorial-ui-certified-route"}))throw new InvalidOperationException("Explicit UI mode missing");});
            test("Actual request parser accepts isolated UI route without runtime acceptance",()=>ProcessRequest("valid"));
            test("Actual request parser rejects wrong UI scene",()=>ProcessRequest("wrong-scene"));
            test("Actual request parser rejects wrong UI seed scene",()=>ProcessRequest("wrong-seed"));
            test("Actual request parser rejects UI capture mixed with movement certification",()=>ProcessRequest("mixed-mode"));
            test("Actual request parser rejects UI timing with attribution",()=>ProcessRequest("attribution"));
            test("Actual request parser rejects UI diagnostic audits",()=>ProcessRequest("audit"));
            test("UI completion excludes discovery screenshot requirement",()=>{var method=typeof(Performance.PerformanceTutorialProcess).GetMethod("RequiresDiscoveryScreenshots",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);if(method==null)throw new InvalidOperationException("Screenshot policy missing");foreach(string mode in new[]{"tutorial-ui-certified-route","tutorial-certified-route","tutorial-rehearsal"})if((bool)method.Invoke(null,new object[]{mode}))throw new InvalidOperationException("Timed route incorrectly requires discovery screenshots");if(!(bool)method.Invoke(null,new object[]{"cuca-path-inspection"}))throw new InvalidOperationException("Discovery screenshot requirement lost");});
            test("Stationary camera rejects translation with unchanged rotation",()=>{var method=Contract.GetMethod("StationaryCameraPositionMatches");if(method==null)throw new InvalidOperationException("Camera position guard missing");if(!(bool)method.Invoke(null,new object[]{Vector3.zero,new Vector3(.2f,0,0),.25d})||(bool)method.Invoke(null,new object[]{Vector3.zero,new Vector3(1,0,0),.25d}))throw new InvalidOperationException("Camera translation tolerance not enforced");});
            test("Minimap accepts only player or owned tagged camera pivot",()=>{var method=Contract.GetMethod("IsOwnedMinimapTarget");if(method==null)throw new InvalidOperationException("Minimap target ownership contract missing");var player=new GameObject("player");var owner=new GameObject("cameraOwner");var pivot=new GameObject("pivot");var camera=new GameObject("camera");var foreign=new GameObject("foreign");try{pivot.transform.parent=owner.transform;camera.transform.parent=pivot.transform;pivot.tag="TargetPlayerMiniMap";foreign.tag="TargetPlayerMiniMap";Func<GameObject,bool> accepts=target=>(bool)method.Invoke(null,new object[]{target,player,owner.transform,camera.transform});if(!accepts(player)||!accepts(pivot)||accepts(foreign))throw new InvalidOperationException("Minimap owner selection incorrect");pivot.tag="Untagged";if(accepts(pivot))throw new InvalidOperationException("Untagged camera target accepted");}finally{UnityEngine.Object.DestroyImmediate(player);UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(foreign);}});
            test("UI light request is explicitly supported",()=>ProcessRequest("light-valid"));
            foreach(string scenario in new[]{"empty","null-count","wrong-count","fault","orphan","unexpected-attempt","unsupported-actor"})
                test("UI warm AI history validates exact census: "+scenario,()=>
                {
                    var census=JObject.FromObject(new {frame=10,activeScene="Tutorial_Final",spawnerAssociations=new object[0],actors=new[]{new{objectId=1,scene="Tutorial_Final",sceneHandle=1,primaryType=typeof(Animator).FullName,role="world-visual",supportedBinding=true,active=true,enabled=true,spawned=false,components=new[]{typeof(Animator).FullName}}}}).ToObject<Performance.PerformanceActorCensus.Report>();
                    var history=new Performance.PerformanceAiCreationHistory.Report{startedFrame=1,currentAiCount=0,matchedAiCount=0,preexistingAiIds=Array.Empty<int>(),unmatchedAiIds=Array.Empty<int>(),orphanLiveAttempts=Array.Empty<long>(),attempts=Array.Empty<Performance.PerformanceAiCreationHistory.Attempt>(),events=Array.Empty<Performance.PerformanceAiCreationHistory.Event>()};
                    if(scenario=="null-count")history.currentAiCount=null;
                    if(scenario=="wrong-count")history.currentAiCount=1;
                    if(scenario=="fault")history.sourceFaulted=true;
                    if(scenario=="orphan")history.orphanLiveAttempts=new[]{1L};
                    if(scenario=="unexpected-attempt")history.attempts=new[]{new Performance.PerformanceAiCreationHistory.Attempt{token=1}};
                    if(scenario=="unsupported-actor")census.actors[0].primaryType="Unknown.Controller";
                    var method=typeof(Performance.PerformanceBoundedRoutePopulation).GetMethod("ValidateObservedAiHistory",BindingFlags.Static|BindingFlags.NonPublic)??throw new InvalidOperationException("Exact warm history validator missing");
                    bool rejected=false;try{method.Invoke(null,new object[]{history,census});}catch(TargetInvocationException e)when(e.InnerException is ArgumentException){rejected=true;}
                    if(rejected!=(scenario!="empty"))throw new InvalidOperationException("Empty AI set was confused with missing or faulted history");
                });
            test("UI unknown measurement arm is rejected",()=>ProcessRequest("unknown-arm"));
            test("UI light arm defaults to full",()=>{var method=Contract.GetMethod("UsesLightControl");if(method==null)throw new InvalidOperationException("UI light policy absent");foreach(string arm in new[]{null,"full"})if((bool)method.Invoke(null,new object[]{arm}))throw new InvalidOperationException("Full UI default changed");if(!(bool)method.Invoke(null,new object[]{"light-control"}))throw new InvalidOperationException("Explicit UI light rejected");Reject(()=>method.Invoke(null,new object[]{"unknown"}));});
            test("UI light completion remains diagnostic",()=>{
                object evidence=LightEvidence();if(!LightCompleted(evidence))throw new InvalidOperationException("Complete synthetic UI light control rejected");
                var session=Activator.CreateInstance(Contract,true);Contract.GetProperty("Evidence").SetValue(session,evidence);
                if(!(bool)Contract.GetProperty("Qualified").GetValue(session)||(bool)Contract.GetProperty("TimingQualified").GetValue(session)||(bool)Contract.GetProperty("PopulationVerified").GetValue(session)||(bool)evidence.GetType().GetProperty("routePopulationQualified").GetValue(evidence))throw new InvalidOperationException("Light functional completion promoted timing/population");
            });
            foreach(string mutation in new[]{"window","conditions","capture-errors","errors","preparation","pose","release","restore","observers","structure","guard","late-preparation","late-release","release-order","nonfinite-release","transition-action","transition-lateness","transition-missing","phase-missing"})
                test("UI light rejects incomplete "+mutation,()=>{var json=LightEvidenceJson();switch(mutation){
                    case "window":json["capture"]["fullWindowCompleted"]=false;break;
                    case "conditions":json["capture"]["conditionsChanged"]=true;break;
                    case "capture-errors":json["capture"]["errors"]=new JArray("fault");break;
                    case "errors":json["errors"]=new JArray("fault");break;
                    case "preparation":json["preparationClosed"]=false;break;
                    case "pose":json["stationaryCameraVerified"]=false;break;
                    case "release":json["finalRelease"]=false;break;
                    case "restore":json["controlsRestored"]=false;break;
                    case "observers":json["lightObserversReleased"]=false;break;
                    case "structure":json["lightStructureUnchanged"]=false;break;
                    case "guard":json["lightPrePostGuardPassed"]=false;break;
                    case "late-preparation":json["lightPreparationEndedElapsed"]=30;break;
                    case "late-release":json["lightObserversReleaseEndedElapsed"]=30;break;
                    case "release-order":json["lightObserversReleaseEndedElapsed"]=28;break;
                    case "nonfinite-release":json["lightObserversReleaseEndedElapsed"]=double.NaN;break;
                    case "transition-action":json["phaseTransitions"][1]["action"]=1;break;
                    case "transition-lateness":json["phaseTransitions"][1]["actualElapsed"]=90.5;break;
                    case "transition-missing":json["phaseTransitions"]=new JArray();break;
                    case "phase-missing":json["phaseObservedFrames"]=new JArray(1,1,0,1);break;
                }if(LightCompleted(json.ToObject(Contract.GetNestedType("EvidenceReport"))))throw new InvalidOperationException("Incomplete light UI evidence accepted: "+mutation);});
            string path=Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-ui-route-fixture-"+Guid.NewGuid().ToString("N")+".json");
            File.WriteAllText(path,JsonConvert.SerializeObject(new{completed=true,passed=rows.All(r=>(bool)r.GetType().GetProperty("passed").GetValue(r)),cases=rows,boundary="Planned UI phase contract only; no runtime/capture acceptance"},Formatting.Indented));Debug.Log("UI route fixture: "+path);
        }
        static JObject LightEvidenceJson()=>JObject.FromObject(new{
            measurementArm="light-control",manifest=Manifest(),capture=new Performance.CaptureResult{fullWindowCompleted=true},
            preparationOpened=true,preparationClosed=true,stationaryCameraVerified=true,finalRelease=true,controlsRestored=true,
            lightObserversReleased=true,lightStructureUnchanged=true,lightPrePostGuardPassed=true,
            lightPreparationStartedElapsed=28d,lightPreparationEndedElapsed=28.01,lightObserversReleaseStartedElapsed=28.02,lightObserversReleaseEndedElapsed=28.03,
            phaseTransitions=new[]{new{index=0,action=1,frame=100,scheduledElapsed=60d,actualElapsed=60.01,lateness=.01},new{index=1,action=2,frame=200,scheduledElapsed=90d,actualElapsed=90.01,lateness=.01},new{index=2,action=2,frame=300,scheduledElapsed=120d,actualElapsed=120.01,lateness=.01}},phaseObservedFrames=new[]{1,1,1,1}});
        static object LightEvidence()=>LightEvidenceJson().ToObject(Contract.GetNestedType("EvidenceReport"));
        static bool LightCompleted(object evidence){var method=Contract.GetMethod("LightControlComplete");if(method==null)throw new InvalidOperationException("UI light completion policy absent");return(bool)method.Invoke(null,new[]{evidence});}
        const string Neutral="{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]},{\"timeSeconds\":149,\"keys\":[]}]}";
        static string Hash(string value){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","");}
        static JObject Manifest()=>JObject.FromObject(new {schemaVersion=1,routeId="tutorial-ui-minimap-v1",scene="Tutorial_Final",sourceRevision=new string('a',40),buildAssemblySha256=new string('a',64),playerBuildGuid=new string('1',32),distributionSha256=new string('a',64),timelineSha256=Hash(Neutral),seedSha256=Hash(Seed),warmupSeconds=30,captureSeconds=120,width=1920,height=1080,quality="High Fidelity",vsync=0,targetFrameRate=-1,renderScale=1,maxLatenessSeconds=.25,cameraPaths=new[]{"Camera","MiniMap/Camera"},miniMapPath="MiniMap",pausePanelPath="HUD/Pause",skillPanelPath="HUD/Skill",composition=new[]{new{role="local-player",primaryType=typeof(PlayerManager).FullName,count=1,active=true,enabled=true,spawned=true}},initialPosition=new[]{0f,0f,0f},initialCameraRotation=new[]{0f,0f,0f,1f},positionTolerance=.25,cameraAngleTolerance=1,counterNames=new[]{"Main Thread"}});
        static string Seed=>"{\"sceneIndex\":1,\"characterName\":\"UI QA\",\"currentHealth\":100,\"vitality\":10,\"endurance\":10,\"xPosition\":0,\"yPosition\":0,\"zPosition\":0}";
        static object Parse(JObject manifest,string timeline=Neutral)=>Contract.GetMethod("ParseManifest").Invoke(null,new object[]{manifest.ToString(),timeline,Seed,new string('a',64),new string('1',32),new string('a',64)});
        static void Reject(Action body){try{body();}catch(TargetInvocationException error)when(error.InnerException is ArgumentException){return;}throw new InvalidOperationException("Invalid UI route accepted");}
        static void ProcessRequest(string scenario)
        {
            string temporary=Path.GetFullPath(Path.GetTempPath());
            string root=Path.GetFullPath(Path.Combine(temporary,"Karawara-ui-request-fixture-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try
            {
                var manifest=Manifest();string seed=Seed;
                if(scenario=="wrong-scene")manifest["scene"]="Video1";
                if(scenario=="wrong-seed"){var data=JObject.Parse(seed);data["sceneIndex"]=2;seed=data.ToString(Formatting.None);manifest["seedSha256"]=Hash(seed);}
                var config=new Performance.PerformanceProcessConfig {mode=scenario=="mixed-mode"?"tutorial-certified-route":"tutorial-ui-certified-route",quality="High Fidelity",width=1920,height=1080,vsync=0,targetFrameRate=-1,certifiedManifestJson=manifest.ToString(Formatting.None),
                    rehearsal=new Performance.TutorialRehearsalConfig {routeId="tutorial-ui-minimap-v1",timelineJson=Neutral,seedSaveJson=seed,outputRoot=Path.Combine(root,"output"),durationSeconds=150,maxLatenessSeconds=.25,checkpoints=Array.Empty<Performance.RehearsalPoseCheckpoint>()}};
                if(scenario=="light-valid")config.measurementArm="light-control";
                if(scenario=="unknown-arm")config.measurementArm="unknown";
                if(scenario=="attribution")config.profilerAttribution=new Performance.ProfilerAttributionConfig{startSeconds=40,durationSeconds=5,allocationCallstacks=false};
                if(scenario=="audit")config.rehearsal.auditMemorySnapshots=true;
                string path=Path.Combine(root,"config.json");File.WriteAllText(path,JsonConvert.SerializeObject(config));
                var response=Performance.PerformanceProcessHarness.ParseRequest(new[]{"-karawaraPerformanceConfig",path,"-karawaraSaveRoot",Path.Combine(root,"save")},Path.Combine(root,"personal"));
                if(!response.handled)throw new InvalidOperationException("Explicit UI config was not handled");
                if(scenario=="valid"||scenario=="light-valid")
                {
                    if(!response.success||response.mode!="tutorial-ui-certified-route"||response.result!=null||response.errors.Count!=0)throw new InvalidOperationException("Valid isolated UI request rejected or runtime acceptance fabricated: "+string.Join(";",response.errors));
                }
                else
                {
                    if(response.success||response.errors.Count==0)throw new InvalidOperationException("Invalid UI request accepted: "+scenario);
                    string detail=string.Join(";",response.errors);
                    if(scenario=="wrong-scene"&&detail.IndexOf("scene",StringComparison.OrdinalIgnoreCase)<0||scenario=="wrong-seed"&&detail.IndexOf("scene1",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Request rejected before exercising UI scene/seed validation: "+detail);
                }
            }
            finally
            {
                if(!root.StartsWith(temporary.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Fixture cleanup escaped temporary root");
                if(Directory.Exists(root))Directory.Delete(root,true);
            }
        }
    }
}

