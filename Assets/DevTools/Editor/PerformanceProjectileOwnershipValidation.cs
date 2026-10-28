using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    public sealed class PerformanceProjectileContactProbe : MonoBehaviour
    {
        public GameObject expected;
        public int contacts, anyContacts;
        void OnTriggerEnter(Collider other) { anyContacts++; if (expected != null && other.transform.IsChildOf(expected.transform)) contacts++; }
    }

    public static class PerformanceProjectileOwnershipValidation
    {
        static readonly string[] Names = { "Authored projectile gets endpoint contact then expires", "Lost target releases owned projectile", "Destroyed shooter releases owned projectile", "Disabled shooter GameObject releases owned projectile", "Disabled shooter component releases owned projectile", "One shooter cleanup preserves another shooter's flight" };
        static readonly List<object> cases = new List<object>();
        static readonly List<GameObject> shots = new List<GameObject>();
        static string path; static int index, frame; static float started, minimumDistance; static double deadline;
        static GameObject fixture, target; static FireballShooter owner, second; static PerformanceProjectileContactProbe probe;
        static PerformanceSkillRetentionSources scope;

        public static string Run()
        {
            if (!Application.isPlaying || path != null || Object.FindObjectsByType<RPGKarawara.PlayerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0) throw new InvalidOperationException("Owned empty Play scene required.");
            path = Path.GetFullPath("Docs/Validation/TOBE/Execution/projectile-ownership-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            cases.Clear(); index = 0; Setup(); EditorApplication.update += Tick; return path;
        }
        static FireballShooter NewSource()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/SkillTree/AnimalFogo/AnimalFogo.prefab");
            if (asset == null) throw new InvalidOperationException("Actual authored shooter prefab missing.");
            var root = Object.Instantiate(asset, fixture.transform); var source = root.GetComponent<FireballShooter>();
            if (!source.isActiveAndEnabled) throw new InvalidOperationException("Actual authored source must be enabled before shooting.");
            if (source.fireballPrefab == null || source.fireballSpawnPoint == null || source._loboAnim == null || source.launchForce <= 0) throw new InvalidOperationException("Authored source configuration changed.");
            return source;
        }
        static void Shoot(FireballShooter source) => typeof(FireballShooter).GetMethod("ShootFireball", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { target.transform });
        static void Setup()
        {
            fixture = new GameObject("Owned authored projectile fixture"); shots.Clear();
            owner = NewSource();
            target = new GameObject("Owned non-character endpoint"); target.transform.SetParent(fixture.transform);
            target.transform.position = owner.fireballSpawnPoint.position + Vector3.forward * (index == 5 ? 8 : 1);
            target.AddComponent<BoxCollider>().size = Vector3.one * .75f;
            var body = target.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            probe = target.AddComponent<PerformanceProjectileContactProbe>();
            scope = PerformanceSkillRetentionSources.Open((source, prefab, instance) => { if (source != owner && source != second) throw new InvalidOperationException("Unknown fixture source."); shots.Add(instance); });
            Shoot(owner); if (shots.Count != 1) throw new InvalidOperationException("Actual private production shot did not notify creation."); probe.expected = shots[0];
            if (index == 1) Object.Destroy(target);
            if (index == 2 || index == 5)
            {
                if (index == 5) { second = NewSource(); Shoot(second); if (shots.Count != 2) throw new InvalidOperationException("Second actual source did not emit."); }
                Object.Destroy(owner.gameObject);
            }
            if (index == 3) owner.gameObject.SetActive(false);
            if (index == 4) owner.enabled = false;
            frame = Time.frameCount; started = Time.time; minimumDistance = float.PositiveInfinity; deadline = EditorApplication.timeSinceStartup + 8;
        }
        static void Tick()
        {
            try
            {
                if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Projectile fixture interrupted/timed out.");
                if (index == 0 && shots[0] != null && target != null) minimumDistance = Mathf.Min(minimumDistance, Vector3.Distance(shots[0].transform.position, target.transform.position));
                if (Time.frameCount < frame + 3 || (index == 0 && Time.time < started + 2)) return;
                bool passed = shots[0] == null && (index != 0 || probe.contacts > 0) && (index != 5 || shots[1] != null);
                cases.Add(new { name = Names[index], passed, detail = "Actual authored source/projectile, normal flight coroutine; endpoint contacts=" + (probe == null ? 0 : probe.contacts) + ", anyContacts=" + (probe == null ? 0 : probe.anyContacts) + ", minDistance=" + minimumDistance + ", alive=" + (shots[0] != null) + ", simulation=" + Physics.simulationMode });
                Cleanup(); index++; if (index == Names.Length) Finish(); else Setup();
            }
            catch (Exception error) { cases.Add(new { name = Names[index], passed = false, detail = error.ToString() }); Cleanup(); Finish(); }
        }
        static void Cleanup()
        {
            scope?.Dispose(); scope = null;
            foreach (var shot in shots) if (shot != null) Object.Destroy(shot); shots.Clear();
            if (fixture != null) Object.Destroy(fixture); fixture = null; second = null;
        }
        static void Finish()
        {
            EditorApplication.update -= Tick;
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.Count == Names.Length && cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, boundary = "Actual authored shooter/prefab in owned empty Play scene with non-character target. Physics trigger contact is observed, not CharacterManager damage, impact-secondary effects, natural enemy detection, standalone ten-cycle or native retention acceptance." }, Formatting.Indented));
            Debug.Log("Projectile ownership fixture: " + path); path = null;
        }
    }
}
