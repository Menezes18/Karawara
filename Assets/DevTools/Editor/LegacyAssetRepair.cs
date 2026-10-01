using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools
{
    public static class LegacyAssetRepair
    {
        private const string MissingSource = "fd2344e6346e3764b833f1b7802fccac";
        private static readonly string[] Paths =
        {
            "Assets/VFX_Shader/FeedBack_Combat/Alavanca/Circle_ref.prefab",
            "Assets/VFX_Shader/FeedBack_Combat/Alavanca/Circle_ref 1.prefab",
            "Assets/VFX_Shader/FeedBack_Combat/Alavanca/Circle_ref 2.prefab"
        };

        [MenuItem("Tools/Karawara/Repair Preserved FireCircle Prefabs")]
        public static void RepairPreservedFireCircles()
        {
            // Validate every asset before writing any. Preserve every local object byte
            // except obsolete prefab provenance. No components or particle properties change.
            var repaired = new string[Paths.Length];
            for (int i = 0; i < Paths.Length; i++)
                repaired[i] = DetachVerifiedOrphan(File.ReadAllText(Paths[i]), Paths[i]);

            for (int i = 0; i < Paths.Length; i++)
            {
                if (repaired[i] == null)
                    continue;
                File.WriteAllText(Paths[i], repaired[i], new System.Text.UTF8Encoding(false));
                AssetDatabase.ImportAsset(Paths[i], ImportAssetOptions.ForceUpdate);
                Debug.Log("Detached missing FireCircle source; all six local objects and particle data preserved: " + Paths[i]);
            }
        }

        private static string DetachVerifiedOrphan(string original, string path)
        {
            if (!original.Contains(MissingSource))
                return null;

            var blocks = Regex.Matches(original, @"^--- !u!(\d+) &([^\r\n]+)\r?\n[\s\S]*?(?=^--- !u!|\z)", RegexOptions.Multiline);
            int objects = 0, transforms = 0, particles = 0, renderers = 0, instances = 0;
            string instanceBlock = null;
            foreach (Match block in blocks)
            {
                if (block.Groups[2].Value.Contains("stripped"))
                    throw new InvalidOperationException("Cannot detach stripped object: " + path);
                switch (block.Groups[1].Value)
                {
                    case "1": objects++; break;
                    case "4": transforms++; break;
                    case "198": particles++; break;
                    case "199": renderers++; break;
                    case "1001": instances++; instanceBlock = block.Value; break;
                    default: throw new InvalidOperationException("Unexpected local component: " + path);
                }
            }
            if (objects != 2 || transforms != 2 || particles != 1 || renderers != 1 || instances != 1
                || !instanceBlock.Contains("m_SourcePrefab: {fileID: 100100000, guid: " + MissingSource))
                throw new InvalidOperationException("Incomplete or changed FireCircle structure: " + path);

            string modified = original.Replace(instanceBlock, "");
            modified = Regex.Replace(modified,
                @"m_CorrespondingSourceObject: \{fileID: -?\d+, guid: " + MissingSource + @",\r?\n\s+type: 3\}",
                "m_CorrespondingSourceObject: {fileID: 0}");
            modified = Regex.Replace(modified, @"m_PrefabInstance: \{fileID: 7389196884243397547\}", "m_PrefabInstance: {fileID: 0}");
            // Object identities, component fields, references, transforms and material
            // links must be byte-identical after excluding provenance fields.
            if (modified.Contains(MissingSource) || StripProvenance(original.Replace(instanceBlock, "")) != StripProvenance(modified))
                throw new InvalidOperationException("Local data comparison failed: " + path);
            return modified;
        }

        private static string StripProvenance(string text)
        {
            text = Regex.Replace(text, @"^  m_CorrespondingSourceObject:.*(?:\r?\n    type: 3\})?\r?\n", "", RegexOptions.Multiline);
            return Regex.Replace(text, @"^  m_PrefabInstance:.*\r?\n", "", RegexOptions.Multiline);
        }
    }
}
