using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceCucaLaserCooldownValidation
    {
        public static void Run()
        {
            var cases=new List<object>();
            Action<string,Action<CucaBoss,Func<float,bool>>> test=(name,body)=>{
                var root=new GameObject("Owned Cuca laser cooldown fixture");
                try {var boss=root.AddComponent<CucaBoss>();boss.cooldownTime=2;
                    var method=typeof(CucaBoss).GetMethod("TryBeginLaserDamage",BindingFlags.Instance|BindingFlags.NonPublic);
                    if(method==null)throw new InvalidOperationException("Production laser cooldown transition missing");
                    Func<float,bool> begin=now=>(bool)method.Invoke(boss,new object[]{now});body(boss,begin);cases.Add(new{name,passed=true,error=""});
                }catch(Exception error){cases.Add(new{name,passed=false,error=error.ToString()});}
                finally{UnityEngine.Object.DestroyImmediate(root);}
            };
            test("Authored initial cooldown preserved",(b,begin)=>{if(begin(1.99f)||!begin(2))throw new InvalidOperationException("Initial authored cooldown changed");});
            test("Repeated render frames cannot reapply laser damage",(b,begin)=>{if(!begin(2))throw new InvalidOperationException("First hit denied");foreach(float t in new[]{2f,2.001f,2.01f,2.1f,3.999f})if(begin(t))throw new InvalidOperationException("Laser admitted another hit inside two-second cooldown");});
            test("Successful hit advances next authored cooldown boundary",(b,begin)=>{if(!begin(2)||!begin(4)||begin(4.1f)||!begin(6))throw new InvalidOperationException("Cooldown boundary was not advanced after each hit");});
            test("Nonfinite and backwards clocks cannot consume a hit",(b,begin)=>{foreach(float t in new[]{float.NaN,float.PositiveInfinity,-1f})if(begin(t))throw new InvalidOperationException("Invalid clock admitted");if(!begin(2))throw new InvalidOperationException("Invalid clock corrupted valid subsequent hit");});
            var path=Path.GetFullPath("Docs/Validation/TOBE/Execution/cuca-laser-cooldown-fixture-"+Guid.NewGuid().ToString("N")+".json");
            File.WriteAllText(path,JsonConvert.SerializeObject(new{passed=cases.All(c=>(bool)c.GetType().GetProperty("passed").GetValue(c)),cases,boundary="Production cooldown state transition only; actual raycast/damage encounter remains required"},Formatting.Indented));Debug.Log(path);
        }
    }
}
