using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    // Read-only offline inspection through the installed Memory Profiler's full
    // processing/crawler. Internal schema changes fail explicitly, never fallback.
    public static class PerformanceMemoryInspection
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly Regex Suspect = new Regex("PetAttackController|DurationSkill|SkillJacare|SkillEscudo|Fireball|HealCircle|PerformanceSkillRetentionProcess|IconDataCuca|SceneChecker|UserMaterial|SpiritPrefab|Fury_effs|Newtonsoft.Json.Utilities.ImmutableCollectionsUtils", RegexOptions.IgnoreCase);
        sealed class Native { public long index; public string type, name, instanceId, objectFlags, hideFlags; public ulong bytes; public int managedIndex; }
        sealed class Managed { public long index, nativeIndex, bytes; public string type, address; public int refCount; }
        sealed class Edge { public string from, to, field, valueField; public long arrayIndex; }
        static object Get(object value, string name)
        {
            var type = value.GetType(); var field = type.GetField(name, Flags);
            if (field != null) return field.GetValue(value);
            var property = type.GetProperty(name, Flags);
            if (property != null) return property.GetValue(value, null);
            throw new MissingMemberException(type.FullName, name);
        }
        static object[] Copy(object collection)
        {
            var array = collection as Array;
            if (array != null) return array.Cast<object>().ToArray();
            var enumerator = (IEnumerator)collection.GetType().GetMethod("GetEnumerator", Flags).Invoke(collection, null);
            var result = new List<object>();
            try { while (enumerator.MoveNext()) result.Add(enumerator.Current); }
            finally { (enumerator as IDisposable)?.Dispose(); }
            return result.ToArray();
        }
        static string Key(object source) => Get(source, "Id") + ":" + Get(source, "Index");
        static string Sha(string path) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }

        static object Optional(object value, string name)
        {
            if (value == null) return null;
            try { return Get(value, name); } catch (MissingMemberException) { return null; }
        }
        static IEnumerable<object> Values(object collection)
        {
            var iterator = ((IEnumerable)collection).GetEnumerator();
            try { while (iterator.MoveNext()) yield return iterator.Current; }
            finally { (iterator as IDisposable)?.Dispose(); }
        }
        static object[] Column(object table, string name, long count)
        {
            var value = Optional(table, name);
            if (value == null) throw new InvalidDataException("Unavailable column: " + name);
            var result = Copy(value);
            if (result.LongLength != count) throw new InvalidDataException("Table length differs: " + name);
            return result;
        }
        static Native[] NativeRows(object snapshot)
        {
            var native = Get(snapshot, "NativeObjects"); var count = (long)Get(native, "Count");
            var names = Column(native, "ObjectName", count); var types = (string[])Get(Get(snapshot, "NativeTypes"), "TypeName");
            var typeIndices = Column(native, "NativeTypeArrayIndex", count); var sizes = Column(native, "Size", count);
            var ids = Column(native, "InstanceId", count); var managed = Column(native, "ManagedObjectIndex", count);
            var flags = Column(native, "Flags", count); var hidden = Column(native, "HideFlags", count);
            return Enumerable.Range(0, checked((int)count)).Select(i => new Native { index = i, type = types[(int)typeIndices[i]], name = (string)names[i], instanceId = ids[i].ToString(), bytes = (ulong)sizes[i], managedIndex = (int)managed[i], objectFlags = flags[i].ToString(), hideFlags = hidden[i].ToString() }).ToArray();
        }
        static JObject[] Selectors(string json)
        {
            var array = JArray.Parse(json);
            if (array.Count < 1 || array.Count > 16) throw new ArgumentException("One to sixteen exact native selectors required.");
            var result = new List<JObject>();
            foreach (var token in array)
            {
                var selector = token as JObject;
                if (selector == null || !selector.Properties().Any() || selector.Properties().Any(p => !new[] { "index", "instanceId", "name", "type" }.Contains(p.Name))) throw new ArgumentException("Selector requires only index, instanceId, name or type.");
                foreach (var p in selector.Properties())
                    if (p.Name == "index" ? p.Value.Type != JTokenType.Integer || (long)p.Value < 0 : p.Value.Type != JTokenType.String || string.IsNullOrEmpty((string)p.Value)) throw new ArgumentException("Selector values must be nonnegative index or nonempty exact strings.");
                if (selector["index"] == null && selector["instanceId"] == null && selector["name"] == null) throw new ArgumentException("Type alone is too broad; provide native index, instanceId or exact name.");
                result.Add(selector);
            }
            return result.ToArray();
        }
        static bool Matches(Native row, JObject selector) =>
            (selector["index"] == null || row.index == (long)selector["index"]) &&
            (selector["instanceId"] == null || row.instanceId == (string)selector["instanceId"]) &&
            (selector["name"] == null || row.name == (string)selector["name"]) &&
            (selector["type"] == null || row.type == (string)selector["type"]);
        static object Unavailable(string reason) => new { available = false, reason, rawCount = (long?)null, totalBytes = (ulong?)null, truncated = false };
        static object SourceIdentity(object source, Native[] natives)
        {
            string id = Get(source, "Id").ToString(); long index = (long)Get(source, "Index");
            if (id == "NativeObject" && index >= 0 && index < natives.LongLength) return natives[index];
            return new { sourceId = id, index };
        }
        static object NativeIncoming(object snapshot, Native target, Native[] natives, int maximumEdges)
        {
            var connections = Optional(snapshot, "Connections"); var dictionary = Optional(connections, "ReferencedBy") as IDictionary;
            if (dictionary == null) return Unavailable("Installed Connections.ReferencedBy unavailable.");
            var edges = new List<object>(); long count = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (Get(entry.Key, "Id").ToString() != "NativeObject" || (long)Get(entry.Key, "Index") != target.index) continue;
                foreach (var source in (IEnumerable)entry.Value)
                {
                    count++;
                    if (edges.Count < maximumEdges) edges.Add(new { from = Key(source), to = "NativeObject:" + target.index, source = SourceIdentity(source, natives) });
                }
                break;
            }
            return new { available = true, cache = "Connections.ReferencedBy (installed remapped SourceIndex)", rawTableCount = Optional(connections, "Count"), rawCount = count, maximumEdges, truncated = count > edges.Count, edges };
        }
        static object NativeRoot(object snapshot, long rootId)
        {
            if (rootId <= 0) return Unavailable("Native object has no positive RootReferenceId; zero must not alias ExecutableAndDlls.");
            var table = Optional(snapshot, "NativeRootReferences"); var count = Optional(table, "Count");
            if (count == null || (long)count == 0) return Unavailable("NativeRootReferences table unavailable or empty.");
            try
            {
                var ids = Column(table, "Id", (long)count); int index = Array.FindIndex(ids, x => (long)x == rootId);
                if (index < 0) return Unavailable("Positive native object root ID not found in NativeRootReferences.");
                return new { available = true, id = rootId, index, areaName = (string)Column(table, "AreaName", (long)count)[index], objectName = (string)Column(table, "ObjectName", (long)count)[index], accumulatedBytes = (ulong)Column(table, "AccumulatedSize", (long)count)[index] };
            }
            catch (InvalidDataException error) { return Unavailable(error.Message); }
        }
        // Select values with bounded retained memory; allocation columns can contain millions of rows.
        static object[] Pick(object table, string name, long count, List<long> indices)
        {
            var column = Optional(table, name);
            if (column == null) return null;
            var columnCount = column is Array array ? array.LongLength : (long?)Optional(column, "Count");
            if (columnCount == 0) return null;
            if (columnCount.HasValue && columnCount.Value != count) throw new InvalidDataException("Allocation column count differs: " + name);
            var result = new object[indices.Count]; int next = 0; long index = 0;
            foreach (var value in Values(column))
            {
                if (next < indices.Count && index == indices[next]) result[next++] = value;
                index++;
                if (next == indices.Count) break;
            }
            if (next != indices.Count) throw new InvalidDataException("Truncated allocation column: " + name);
            return result;
        }
        static object AllocationRegion(object snapshot, int index)
        {
            var regions = Optional(snapshot, "NativeMemoryRegions"); var count = Optional(regions, "Count");
            if (count == null || index < 0 || index >= (long)count) return Unavailable("Allocation memory region index/table unavailable.");
            return new { available = true, index, name = (string)Column(regions, "MemoryRegionName", (long)count)[index], parentIndex = (int)Column(regions, "ParentIndex", (long)count)[index], address = "0x" + ((ulong)Column(regions, "AddressBase", (long)count)[index]).ToString("x"), bytes = (ulong)Column(regions, "AddressSize", (long)count)[index] };
        }
        static object AllocationLabel(object snapshot, ulong? siteId)
        {
            if (!siteId.HasValue || siteId == 0) return Unavailable("Allocation site ID not captured or zero; label attribution unavailable.");
            var sites = Optional(snapshot, "NativeAllocationSites"); var count = Optional(sites, "Count");
            var labels = Optional(snapshot, "NativeMemoryLabels"); var labelCount = Optional(labels, "Count");
            if (count == null || labelCount == null || (long)count == 0) return Unavailable("Native allocation site/label tables unavailable.");
            var ids = Column(sites, "Id", (long)count); int siteIndex = Array.FindIndex(ids, x => (ulong)x == siteId);
            if (siteIndex < 0) return Unavailable("Allocation site ID not found in site table.");
            int labelIndex = (int)Column(sites, "MemoryLabelIndex", (long)count)[siteIndex];
            if (labelIndex <= 0 || labelIndex >= (long)labelCount) return Unavailable("Site memory label index unavailable or invalid.");
            return new { available = true, siteIndex, index = labelIndex, name = (string)Column(labels, "MemoryLabelName", (long)labelCount)[labelIndex] };
        }
        static object NativeAllocations(object snapshot, long rootId, int maximumAllocations)
        {
            if (rootId <= 0) return Unavailable("No positive native object root ID for allocation attribution.");
            var table = Optional(snapshot, "NativeAllocations"); var countValue = Optional(table, "Count");
            if (countValue == null || (long)countValue == 0) return Unavailable("NativeAllocations table unavailable or empty; capture support cannot be inferred as zero allocations.");
            long count = (long)countValue; var roots = Optional(table, "RootReferenceId"); var sizes = Optional(table, "Size");
            if (roots == null || sizes == null) return Unavailable("Native allocation root/size columns unavailable.");
            try
            {
                var indices = new List<long>(); long matches = 0, scanned = 0; ulong total = 0;
                using (var rootRows = Values(roots).GetEnumerator()) using (var sizeRows = Values(sizes).GetEnumerator())
                {
                    while (rootRows.MoveNext())
                    {
                        if (!sizeRows.MoveNext()) throw new InvalidDataException("Native allocation size table truncated.");
                        if ((long)rootRows.Current == rootId) { matches++; total = checked(total + (ulong)sizeRows.Current); if (indices.Count < maximumAllocations) indices.Add(scanned); }
                        scanned++;
                    }
                    if (scanned != count || sizeRows.MoveNext()) throw new InvalidDataException("Native allocation root/size counts differ.");
                }
                var addresses = Pick(table, "Address", count, indices); var bytes = Pick(table, "Size", count, indices);
                var regions = Pick(table, "MemoryRegionIndex", count, indices); var sites = Pick(table, "AllocationSiteId", count, indices);
                var overhead = Pick(table, "OverheadSize", count, indices); var padding = Pick(table, "PaddingSize", count, indices);
                var regionCache = new Dictionary<int, object>(); var labelCache = new Dictionary<ulong, object>();
                Func<int, object> regionFor = index => { if (!regionCache.TryGetValue(index, out var value)) regionCache[index] = value = AllocationRegion(snapshot, index); return value; };
                Func<ulong?, object> labelFor = id => { if (!id.HasValue) return AllocationLabel(snapshot, null); if (!labelCache.TryGetValue(id.Value, out var value)) labelCache[id.Value] = value = AllocationLabel(snapshot, id); return value; };
                var rows = indices.Select((index, i) => new { index, rootReferenceId = rootId, address = addresses == null ? null : "0x" + ((ulong)addresses[i]).ToString("x"), bytes = (ulong)bytes[i], overheadBytes = overhead == null ? (int?)null : (int)overhead[i], paddingBytes = padding == null ? (int?)null : (int)padding[i], memoryRegionIndex = regions == null ? (int?)null : (int)regions[i], region = regions == null ? Unavailable("Allocation memory region column unavailable.") : regionFor((int)regions[i]), allocationSiteId = sites == null ? (ulong?)null : (ulong)sites[i], label = labelFor(sites == null ? (ulong?)null : (ulong)sites[i]) }).ToArray();
                return new { available = true, rawTableCount = count, rawCount = matches, totalBytes = total, maximumAllocations, truncated = matches > rows.Length, rows };
            }
            catch (InvalidDataException error) { return Unavailable(error.Message); }
        }
        static object GraphicsResources(object snapshot, long rootId, int maximumGraphicsResources)
        {
            if (rootId <= 0) return Unavailable("No positive native object root ID for graphics attribution.");
            if (!(Optional(snapshot, "HasGfxResourceReferencesAndAllocators") is bool supported) || !supported)
                return Unavailable("Installed snapshot reports no graphics-resource/root mapping support.");
            var table = Optional(snapshot, "NativeGfxResourceReferences"); var countValue = Optional(table, "Count");
            if (countValue == null || (long)countValue <= 0)
                return Unavailable("NativeGfxResourceReferences table unavailable or empty; captured graphics resources cannot be inferred as zero.");
            long count = (long)countValue;
            var roots = Optional(table, "RootId"); var ids = Optional(table, "GfxResourceId"); var sizes = Optional(table, "GfxSize");
            if (roots == null || ids == null || sizes == null) return Unavailable("Graphics root/resource ID/size columns unavailable.");
            try
            {
                long scanned = 0, matches = 0; ulong total = 0; var rows = new List<object>();
                using (var rootRows = Values(roots).GetEnumerator()) using (var idRows = Values(ids).GetEnumerator()) using (var sizeRows = Values(sizes).GetEnumerator())
                {
                    while (rootRows.MoveNext())
                    {
                        if (!idRows.MoveNext() || !sizeRows.MoveNext()) throw new InvalidDataException("Graphics resource ID/size table truncated.");
                        if ((long)rootRows.Current == rootId)
                        {
                            matches++; total = checked(total + (ulong)sizeRows.Current);
                            if (rows.Count < maximumGraphicsResources) rows.Add(new { index = scanned, rootId, resourceId = (ulong)idRows.Current, bytes = (ulong)sizeRows.Current });
                        }
                        scanned++;
                    }
                    if (scanned != count || idRows.MoveNext() || sizeRows.MoveNext()) throw new InvalidDataException("Graphics resource table counts differ.");
                }
                return new { available = true, supported = true, cache = "NativeGfxResourceReferences (exact positive RootId)", rootId, rawTableCount = count, rawCount = matches, totalBytes = total, maximumGraphicsResources, truncated = matches > rows.Count, rows,
                    boundary = "Snapshot-reported graphics resources attributed by root ID, separate from native allocation payloads; not additional independently measured GPU residency, retaining ownership, growth cause or retention acceptance." };
            }
            catch (InvalidDataException error) { return Unavailable(error.Message); }
        }
        static object BuildNativeOwnership(object snapshot, string targetsJson, int maximumNativeEdges, int maximumNativeAllocations)
        {
            if (maximumNativeEdges < 1 || maximumNativeEdges > 2000 || maximumNativeAllocations < 1 || maximumNativeAllocations > 2000) throw new ArgumentOutOfRangeException("Native edge/allocation limits must each be 1..2000 per target.");
            var selectors = Selectors(targetsJson); var natives = NativeRows(snapshot);
            var selections = selectors.Select(s => new { selector = s, matchCount = natives.Count(n => Matches(n, s)) }).ToArray();
            var selected = natives.Where(n => selectors.Any(s => Matches(n, s))).ToArray();
            if (selected.Length > 32) throw new InvalidOperationException("Target selectors matched more than 32 native objects; narrow exact identities.");
            var rootValues = Optional(Get(snapshot, "NativeObjects"), "RootReferenceId");
            var rootIds = rootValues == null ? null : Copy(rootValues);
            if (rootIds != null && rootIds.Length != natives.Length) rootIds = null;
            return new { available = true, selections, maximumNativeEdges, maximumNativeAllocations, selectedCount = selected.Length,
                targets = selected.Select(n => { long? rootId = rootIds == null ? (long?)null : (long)rootIds[n.index]; return new { identity = n, rootReferenceId = rootId, incoming = NativeIncoming(snapshot, n, natives, maximumNativeEdges), root = rootId.HasValue ? NativeRoot(snapshot, rootId.Value) : Unavailable("NativeObjects.RootReferenceId column unavailable or mismatched."), allocations = rootId.HasValue ? NativeAllocations(snapshot, rootId.Value, maximumNativeAllocations) : Unavailable("Native object root ID unavailable."), graphicsResources = rootId.HasValue ? GraphicsResources(snapshot, rootId.Value, maximumNativeAllocations) : Unavailable("Native object root ID unavailable.") }; }).ToArray(),
                boundary = "Exact snapshot-local native identities; immediate snapshot Connections.ReferencedBy only, not complete retaining closure or crawled managed-shell references. Positive root ID allocation attribution is not an allocation callstack, retaining owner, leak/cache verdict or G1B acceptance. Per-target independent edge/allocation caps; raw matching counts/totals include omitted rows." };
        }

        public static string Inspect(string path, string output, int maximumEdges = 20000)
            => InspectCore(path, output, maximumEdges, null, 200, 200);

        public static string InspectTargeted(string path, string output, string targetsJson, int maximumEdges = 20000, int maximumNativeEdges = 200, int maximumNativeAllocations = 200)
        {
            Selectors(targetsJson);
            if (maximumNativeEdges < 1 || maximumNativeEdges > 2000 || maximumNativeAllocations < 1 || maximumNativeAllocations > 2000) throw new ArgumentOutOfRangeException("Native edge/allocation limits must each be 1..2000 per target.");
            return InspectCore(path, output, maximumEdges, targetsJson, maximumNativeEdges, maximumNativeAllocations);
        }

        static string InspectCore(string path, string output, int maximumEdges, string targetsJson, int maximumNativeEdges, int maximumNativeAllocations)
        {
            if (maximumEdges < 1 || maximumEdges > 20000) throw new ArgumentOutOfRangeException(nameof(maximumEdges));
            if (Application.isPlaying || UnityEditor.EditorApplication.isCompiling) throw new InvalidOperationException("Offline EditMode required.");
            path = Path.GetFullPath(path); output = Path.GetFullPath(output);
            if (!File.Exists(path) || !path.EndsWith(".snap", StringComparison.OrdinalIgnoreCase) || File.Exists(output)) throw new InvalidOperationException("Existing snapshot and fresh output required.");
            object reader = null, snapshot = null;
            var startedUtc = DateTime.UtcNow.ToString("O");
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetType("Unity.MemoryProfiler.Editor.CachedSnapshot") != null);
                reader = Activator.CreateInstance(assembly.GetType("Unity.MemoryProfiler.Editor.Format.QueriedSnapshot.FileReader", true));
                var opened = reader.GetType().GetMethod("Open", Flags).Invoke(reader, new object[] { path });
                if (opened.ToString() != "Success") throw new IOException("Installed snapshot reader: " + opened);
                var type = assembly.GetType("Unity.MemoryProfiler.Editor.CachedSnapshot", true);
                snapshot = Activator.CreateInstance(type, Flags, null, new[] { reader }, null);
                var processing = (IEnumerator)type.GetMethod("PostProcess", Flags).Invoke(snapshot, new object[] { true });
                try { while (processing.MoveNext()) { } } finally { (processing as IDisposable)?.Dispose(); }
                if (!(bool)Get(snapshot, "Valid")) throw new InvalidDataException("Full installed crawler did not produce a valid snapshot.");
                var meta = Get(snapshot, "MetaData");
                var native = Get(snapshot, "NativeObjects");
                var nativeNames = (string[])Get(native, "ObjectName");
                var nativeTypes = (string[])Get(Get(snapshot, "NativeTypes"), "TypeName");
                var nativeTypeIndices = Copy(Get(native, "NativeTypeArrayIndex"));
                var nativeSizes = Copy(Get(native, "Size"));
                var nativeIds = Copy(Get(native, "InstanceId"));
                var nativeManaged = Copy(Get(native, "ManagedObjectIndex"));
                var objectFlags = Copy(Get(native, "Flags")); var hideFlags = Copy(Get(native, "HideFlags"));
                int nativeCount = checked((int)(long)Get(native, "Count"));
                if (new[] { nativeNames.Length, nativeTypeIndices.Length, nativeSizes.Length, nativeIds.Length, nativeManaged.Length, objectFlags.Length, hideFlags.Length }.Any(n => n != nativeCount)) throw new InvalidDataException("Native table lengths differ.");
                var natives = Enumerable.Range(0, nativeCount).Select(i => new Native { index = i, type = nativeTypes[(int)nativeTypeIndices[i]], name = nativeNames[i], instanceId = nativeIds[i].ToString(), bytes = (ulong)nativeSizes[i], managedIndex = (int)nativeManaged[i], objectFlags = objectFlags[i].ToString(), hideFlags = hideFlags[i].ToString() }).ToArray();
                var crawled = Get(snapshot, "CrawledData");
                var objects = Copy(Get(crawled, "m_ManagedObjects"));
                var typeNames = (string[])Get(Get(snapshot, "TypeDescriptions"), "TypeDescriptionName");
                var managed = objects.Select((o, i) => new Managed { index = i, nativeIndex = (long)Get(o, "NativeObjectIndex"), bytes = (long)Get(o, "Size"), type = (int)Get(o, "ITypeDescription") < 0 ? "<unknown>" : typeNames[(int)Get(o, "ITypeDescription")], address = "0x" + ((ulong)Get(o, "PtrObject")).ToString("x"), refCount = (int)Get(o, "RefCount") }).ToArray();
                var fields = (string[])Get(Get(snapshot, "FieldDescriptions"), "FieldDescriptionName");
                var connections = Get(crawled, "Connections");
                var incoming = new Dictionary<string, List<Edge>>();
                long remaining = (long)Get(connections, "Count");
                foreach (Array block in (IEnumerable)Get(connections, "m_Blocks"))
                {
                    for (int i = 0; i < block.Length && remaining > 0; i++, remaining--)
                    {
                        var connection = block.GetValue(i);
                        int field = (int)Get(connection, "FieldFrom"), valueField = (int)Get(connection, "ValueTypeFieldFrom");
                        var edge = new Edge { from = Key(Get(connection, "IndexFrom")), to = Key(Get(connection, "IndexTo")), field = field < 0 ? null : fields[field], valueField = valueField < 0 ? null : fields[valueField], arrayIndex = (long)Get(connection, "ArrayIndexFrom") };
                        if (!incoming.TryGetValue(edge.to, out var list)) incoming[edge.to] = list = new List<Edge>();
                        list.Add(edge);
                    }
                }
                if (remaining != 0) throw new InvalidDataException("Managed connection table truncated.");
                var selected = new HashSet<string>(managed.Where(m => Suspect.IsMatch(m.type)).Select(m => "ManagedObject:" + m.index));
                var roots = selected.ToArray(); var edges = new List<Edge>(); var frontier = selected.ToArray(); int depth = 0; bool budgetExhausted = false;
                for (; depth < 8 && frontier.Length != 0 && edges.Count < maximumEdges; depth++)
                {
                    var next = new List<string>();
                    foreach (string key in frontier)
                    {
                        if (incoming.TryGetValue(key, out var list)) foreach (var edge in list)
                        {
                            if (edges.Count == maximumEdges) { budgetExhausted = true; break; }
                            edges.Add(edge); if (selected.Add(edge.from)) next.Add(edge.from);
                        }
                        if (budgetExhausted) break;
                    }
                    frontier = next.ToArray();
                    if (budgetExhausted) break;
                }
                var nativeOwnership = targetsJson == null ? null : BuildNativeOwnership(snapshot, targetsJson, maximumNativeEdges, maximumNativeAllocations);
                var document = new { startedUtc, finishedUtc = DateTime.UtcNow.ToString("O"), completed = true, path, sha256 = Sha(path), bytes = new FileInfo(path).Length, installedAssembly = assembly.FullName, installedAssemblySha256 = Sha(assembly.Location), inspectorSourceSha256 = Sha("Assets/DevTools/Editor/PerformanceMemoryInspection.cs"), inspectorAssemblySha256 = Sha(typeof(PerformanceMemoryInspection).Assembly.Location),
                    metadata = new { product = Get(meta, "ProductName"), platform = Get(meta, "Platform"), unityVersion = Get(meta, "UnityVersion"), sessionGuid = Get(meta, "SessionGUID"), editorCapture = Get(meta, "IsEditorCapture"), flags = Get(meta, "CaptureFlags").ToString(), timestamp = Get(snapshot, "TimeStamp") },
                    nativeObjectCount = nativeCount, nativeTypes = natives.GroupBy(n => n.type).Select(g => new { type = g.Key, count = g.Count(), nativeBytes = g.Aggregate(0UL, (sum, row) => checked(sum + row.bytes)) }).OrderByDescending(g => g.nativeBytes).ToArray(),
                    nativeDetails = natives.Where(n => n.type == "Material" || n.type == "ScriptableObject" || n.type == "VisualEffectAsset" || Suspect.IsMatch(n.name ?? "")).ToArray(),
                    managedTypeNames = typeNames, selectedNativeSources = natives.Where(n => selected.Contains("NativeObject:" + n.index)).ToArray(),
                    managedObjectCount = managed.Length, managedTypes = managed.GroupBy(m => m.type).Select(g => new { type = g.Key, count = g.Count(), bytes = g.Sum(m => m.bytes), missingNative = g.Count(m => m.nativeIndex < 0) }).OrderByDescending(g => g.bytes).ToArray(),
                    managedIncoming = new { selectionPattern = Suspect.ToString(), roots, depth, maximumEdges, truncated = budgetExhausted || frontier.Length != 0, edges, nodes = managed.Where(m => selected.Contains("ManagedObject:" + m.index)).ToArray(), terminalSources = selected.Where(k => !k.StartsWith("ManagedObject:")).OrderBy(k => k).ToArray() },
                    nativeOwnership,
                    boundary = "Offline installed full crawler/native tables and bounded incoming managed connection paths. MissingNative is also normal for pure managed types; never a leak verdict. Native reference graph/remaining paths and gameplay acceptance require separate interpretation. No global retention or timing acceptance." };
                using (var stream = new FileStream(output, FileMode.CreateNew)) using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(document, Formatting.Indented));
                return output;
            }
            finally { (snapshot as IDisposable)?.Dispose(); (reader as IDisposable)?.Dispose(); }
        }
    }
}
