using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using RPGKarawara.DevTools.Performance;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceCaptureClockIntegrationValidation
    {
        public static void Run()
        {
            var cases=new List<object>(); var root=new GameObject("Owned capture Update clock fixture");
            try {
                var capture=root.AddComponent<PerformanceCaptureController>();
                if(!capture.Begin(new CaptureConfig {qaEnabled=true,routeId="update-clock-integration-fixture",runId="fixture-",outputRoot=Path.GetFullPath("Builds/TOBE-QA/ClockFixtures/"+Guid.NewGuid().ToString("N")),commit="fixture",buildId="fixture",camera="fixture",counterNames=Array.Empty<string>()})) throw new Exception("Owned capture Begin failed");
                var start=(double)typeof(PerformanceCaptureController).GetProperty("StartedTimestampSeconds",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(capture);
                var sample=typeof(PerformanceCaptureController).GetMethod("SampleAt",BindingFlags.Instance|BindingFlags.NonPublic);
                sample.Invoke(capture,new object[]{start+.001}); sample.Invoke(capture,new object[]{start+.002});
                var result=capture.End();
                var field=typeof(CaptureResult).GetField("updateClock");
                if(field==null)throw new Exception("CaptureResult lacks common QPC Update observer export");
                var report=field.GetValue(result);if(report==null)throw new Exception("Actual controller did not export Update endpoints");
                var type=report.GetType();var ticks=(long[])type.GetField("ticks").GetValue(report);
                if(ticks.Length!=2||ticks[1]<=ticks[0]||(bool)type.GetField("faulted").GetValue(report))throw new Exception("Controller did not record exactly one ordered endpoint per actual sampling callback");
                var clock=(PerformanceExternalClock)type.GetField("clock").GetValue(report);
                if(clock.ended==null||clock.processId!=result.externalClock.processId||clock.started.unitySeconds!=result.startedTimestampSeconds)throw new Exception("Update endpoints lack same owned completed route clock");
                if(result.comparisonValid||capture.OwnedRecorderCount!=0)throw new Exception("Manual Editor fixture promoted or recorder leaked");
                cases.Add(new{name="Actual capture common sampler and completed owned anchors",passed=true,error=""});
            }catch(Exception error){cases.Add(new{name="Actual capture common sampler and completed owned anchors",passed=false,error=error.ToString()});}
            finally{UnityEngine.Object.DestroyImmediate(root);}
            var path=Path.GetFullPath("Docs/Validation/TOBE/Execution/capture-update-clock-integration-"+Guid.NewGuid().ToString("N")+".json");
            File.WriteAllText(path,JsonConvert.SerializeObject(new{passed=cases.All(c=>(bool)c.GetType().GetProperty("passed").GetValue(c)),cases,boundary="Actual controller wiring only; no pacing, timing or G1B acceptance"},Formatting.Indented));Debug.Log(path);
        }
    }
}
