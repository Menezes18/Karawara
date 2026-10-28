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
    // Pure cache-shaped data: validates attribution/export semantics, not snapshot capture.
    public static class PerformanceNativeOwnershipValidation
    {
        sealed class Source { public string Id; public long Index; }
        static Source Native(long index) => new Source { Id = "NativeObject", Index = index };
        static object Snapshot(bool roots = true, bool allocations = true, long rootId = 42, bool graphicsSupported = true, bool graphics = true, bool emptyGraphics = false)
        {
            var target = Native(1);
            return new
            {
                NativeTypes = new { TypeName = new[] { "Texture2D", "VisualEffectAsset" } },
                NativeObjects = new { Count = 3L, ObjectName = new[] { "Cuca icon", "ShieldTurtle", "Other" }, NativeTypeArrayIndex = new[] { 0, 1, 0 }, Size = new ulong[] { 20, 22504, 30 }, InstanceId = new[] { 12095, 12096, 12097 }, ManagedObjectIndex = new[] { -1, -1, -1 }, Flags = new[] { 0, 0, 0 }, HideFlags = new[] { 0, 0, 0 }, RootReferenceId = new[] { 99L, rootId, 99L } },
                Connections = new { Count = 3L, ReferencedBy = new Dictionary<Source, List<Source>> { [target] = new List<Source> { Native(0), Native(2), new Source { Id = "ManagedObject", Index = 7 } } } },
                NativeRootReferences = roots ? new { Count = 2L, Id = new[] { 0L, 42L }, AreaName = new[] { "System", "VFX" }, ObjectName = new[] { "ExecutableAndDlls", "Shield root" }, AccumulatedSize = new ulong[] { 999, 50 } } : null,
                NativeAllocations = allocations ? new { Count = 3L, RootReferenceId = new[] { 42L, 99L, 42L }, Address = new ulong[] { 100, 200, 300 }, Size = new ulong[] { 11, 500, 17 }, OverheadSize = new[] { 1, 2, 3 }, PaddingSize = new[] { 0, 0, 0 }, MemoryRegionIndex = new[] { 0, 0, 0 }, AllocationSiteId = new ulong[] { 7, 8, 7 } } : null,
                NativeMemoryRegions = new { Count = 1L, MemoryRegionName = new[] { "ALLOC_DEFAULT_MAIN" }, ParentIndex = new[] { -1 }, AddressBase = new ulong[] { 0 }, AddressSize = new ulong[] { 1000 } },
                NativeAllocationSites = new { Count = 1L, Id = new ulong[] { 7 }, MemoryLabelIndex = new[] { 1 } },
                NativeMemoryLabels = new { Count = 2L, MemoryLabelName = new[] { "delimiter", "VFX label" } },
                HasGfxResourceReferencesAndAllocators = graphicsSupported,
                NativeGfxResourceReferences = graphics ? new { Count = emptyGraphics ? 0L : 3L, RootId = emptyGraphics ? Array.Empty<long>() : new[] { 42L, 99L, 42L }, GfxResourceId = emptyGraphics ? Array.Empty<ulong>() : new ulong[] { 501, 502, 503 }, GfxSize = emptyGraphics ? Array.Empty<ulong>() : new ulong[] { 11, 1000, 17 } } : null
            };
        }
        static JObject Export(object snapshot, string selector, int edges = 2, int allocations = 1)
        {
            var method = typeof(PerformanceMemoryInspection).GetMethod("BuildNativeOwnership", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Targeted native ownership export is not implemented.");
            try { return JObject.Parse(JsonConvert.SerializeObject(method.Invoke(null, new object[] { snapshot, selector, edges, allocations }))); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        static JObject Target(JObject result) => (JObject)result["targets"][0];
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("EditMode fixture required.");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "" }); } catch (Exception error) { cases.Add(new { name, passed = false, detail = error.ToString() }); } };
            const string selector = "[{\"index\":1,\"instanceId\":\"12096\",\"type\":\"VisualEffectAsset\",\"name\":\"ShieldTurtle\"}]";
            test("Exact index and ID select identity", () => { var row = Target(Export(Snapshot(), selector)); Require((long)row["identity"]["index"] == 1 && (string)row["identity"]["instanceId"] == "12096" && (string)row["identity"]["name"] == "ShieldTurtle", "Exact identity missing."); });
            test("Mismatched ID cannot silently fall back to index", () => { var result = Export(Snapshot(), "[{\"index\":1,\"instanceId\":\"12097\"}]"); Require(!result["targets"].Any() && (int)result["selections"][0]["matchCount"] == 0, "Conjunctive identity selection required."); });
            test("Name and type selector supports later icon investigations", () => { var row = Target(Export(Snapshot(), "[{\"name\":\"Cuca icon\",\"type\":\"Texture2D\"} ]")); Require((long)row["identity"]["index"] == 0, "Exact name/type selector missing."); });
            test("Incoming native references are independently bounded", () => { var row = Target(Export(Snapshot(), selector)); var graph = row["incoming"]; Require((bool)graph["available"] && (long)graph["rawCount"] == 3 && graph["edges"].Count() == 2 && (bool)graph["truncated"], "Raw references and edge bound lost."); Require((string)graph["edges"][0]["source"]["name"] == "Cuca icon", "Referenced native identity missing."); });
            test("Root IDs map by ID rather than array index", () => { var row = Target(Export(Snapshot(), selector)); Require((long)row["root"]["id"] == 42 && (long)row["root"]["index"] == 1 && (string)row["root"]["objectName"] == "Shield root", "Root ID lookup incorrect."); });
            test("Allocation bound preserves complete raw count and size", () => { var a = Target(Export(Snapshot(), selector))["allocations"]; Require((bool)a["available"] && (long)a["rawCount"] == 2 && (ulong)a["totalBytes"] == 28 && a["rows"].Count() == 1 && (bool)a["truncated"], "Allocation totals/budget incorrect."); Require((string)a["rows"][0]["region"]["name"] == "ALLOC_DEFAULT_MAIN" && (string)a["rows"][0]["label"]["name"] == "VFX label", "Region or site label attribution missing."); });
            test("Missing root/allocation tables remain unavailable", () => { var row = Target(Export(Snapshot(false, false), selector)); Require(!(bool)row["root"]["available"] && !(bool)row["allocations"]["available"] && row["allocations"]["rawCount"].Type == JTokenType.Null, "Missing data fabricated as zero/owner."); });
            test("Invalid root zero does not alias executable root", () => { var row = Target(Export(Snapshot(rootId: 0), selector)); Require(!(bool)row["root"]["available"] && !(bool)row["allocations"]["available"], "Invalid zero root was attributed."); });
            test("Graphics roots match exact IDs with independently bounded resource rows", () =>
            {
                var row = Target(Export(Snapshot(), selector)); var graphics = row["graphicsResources"];
                Require(graphics != null, "Targeted graphics-resource export is not implemented.");
                Require((bool)graphics["available"] && (long)graphics["rawTableCount"] == 3 && (long)graphics["rawCount"] == 2 && (ulong)graphics["totalBytes"] == 28 && graphics["rows"].Count() == 1 && (bool)graphics["truncated"], "Graphics matching totals and independent resource cap required.");
                Require((long)graphics["rows"][0]["index"] == 0 && (long)graphics["rows"][0]["rootId"] == 42 && (ulong)graphics["rows"][0]["resourceId"] == 501 && (ulong)graphics["rows"][0]["bytes"] == 11 && row["allocations"]["rows"].Count() == 1, "Raw graphics identity or independent native allocation budget lost.");
            });
            test("Unsupported graphics data remains unavailable despite cache-shaped rows", () =>
            {
                var graphics = Target(Export(Snapshot(graphicsSupported: false), selector))["graphicsResources"];
                Require(graphics != null, "Targeted graphics-resource export is not implemented.");
                Require(!(bool)graphics["available"] && graphics["rawCount"].Type == JTokenType.Null && graphics["totalBytes"].Type == JTokenType.Null, "Unsupported graphics data fabricated as owner/zero.");
            });
            test("Missing or empty graphics capture remains unavailable", () =>
            {
                foreach (var snapshot in new[] { Snapshot(graphics: false), Snapshot(emptyGraphics: true) })
                {
                    var graphics = Target(Export(snapshot, selector))["graphicsResources"];
                    Require(graphics != null, "Targeted graphics-resource export is not implemented.");
                    Require(!(bool)graphics["available"] && graphics["rawCount"].Type == JTokenType.Null, "Missing/empty graphics capture fabricated as zero resources.");
                }
            });
            test("Invalid zero graphics root stays unavailable", () =>
            {
                var graphics = Target(Export(Snapshot(rootId: 0), selector))["graphicsResources"];
                Require(graphics != null, "Targeted graphics-resource export is not implemented.");
                Require(!(bool)graphics["available"] && graphics["totalBytes"].Type == JTokenType.Null, "Invalid root zero attributed to graphics resources.");
            });
            test("Complete graphics table with no positive-root match reports measured zero", () =>
            {
                var graphics = Target(Export(Snapshot(rootId: 100), selector))["graphicsResources"];
                Require(graphics != null, "Targeted graphics-resource export is not implemented.");
                Require((bool)graphics["available"] && (long)graphics["rawCount"] == 0 && (ulong)graphics["totalBytes"] == 0 && !graphics["rows"].Any() && !(bool)graphics["truncated"], "Available complete-table no-match differs from unsupported data.");
            });
            var path = Path.GetFullPath("Docs/Validation/TOBE/Execution/native-ownership-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, boundary = "Synthetic installed-cache-shaped data validates selection, IDs, bounds and unavailable semantics. Real snapshot export remains a separate gate; no retention acceptance." }, Formatting.Indented));
            Debug.Log("Native ownership fixture: " + path); return path;
        }
    }
}
