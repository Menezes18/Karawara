using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Snapshot exact authored choice/producer/prefab references before replay input or mission creation.
    public static class PerformanceMissionProducerInventory
    {
        [Serializable] public sealed class Row
        {
            public int npcId, choiceIndex, choiceRootId, missionId, spawnerId, prefabId, existingCreatedId, frame;
            public double time;
            public string primaryType, issue;
        }
        public static Row[] Capture(NPCDialogue[] owners)
        {
            if (!Application.isPlaying || owners == null) throw new ArgumentException("Actual loaded NPC owners required.");
            var rows = new List<Row>();
            foreach (var owner in owners)
            {
                if (owner == null || !owner.gameObject.scene.IsValid()) continue;
                var choices = new[] { owner.escolha1, owner.escolha2 };
                for (int i = 0; i < choices.Length; i++)
                {
                    var choice = choices[i]; if (choice == null) continue;
                    var mission = choice.GetComponent<MissionManager>();
                    foreach (var spawner in choice.GetComponentsInChildren<AICharacterSpawner>(true))
                    {
                        var prefab = spawner.QaPrefab;
                        var actor = prefab == null ? null : prefab.GetComponent<AICharacterManager>();
                        rows.Add(new Row { npcId = owner.GetInstanceID(), choiceIndex = i + 1, choiceRootId = choice.GetInstanceID(), missionId = mission == null ? 0 : mission.GetInstanceID(),
                            spawnerId = spawner.GetInstanceID(), prefabId = prefab == null ? 0 : prefab.GetInstanceID(), existingCreatedId = spawner.QaSpawned == null ? 0 : spawner.QaSpawned.GetInstanceID(),
                            frame = Time.frameCount, time = Time.realtimeSinceStartupAsDouble, primaryType = actor == null ? null : actor.GetType().FullName,
                            issue = mission == null || actor == null ? "Missing actual mission or AI prefab component." : null });
                    }
                    foreach (var activator in choice.GetComponentsInChildren<AtivarSaci>(true))
                    {
                        var spawner = activator.Input;
                        var prefab = spawner == null ? null : spawner.bossPrefab;
                        var actor = prefab == null ? null : prefab.GetComponent<AIBossCharacterManager>();
                        rows.Add(new Row { npcId = owner.GetInstanceID(), choiceIndex = i + 1, choiceRootId = choice.GetInstanceID(), missionId = mission == null ? 0 : mission.GetInstanceID(),
                            spawnerId = spawner == null ? 0 : spawner.GetInstanceID(), prefabId = prefab == null ? 0 : prefab.GetInstanceID(),
                            existingCreatedId = spawner == null || spawner.QaSpawned == null ? 0 : spawner.QaSpawned.GetInstanceID(),
                            frame = Time.frameCount, time = Time.realtimeSinceStartupAsDouble, primaryType = actor == null ? null : actor.GetType().FullName,
                            issue = mission == null || actor == null || spawner == null || spawner.spawnPoint == null ? "Missing actual mission, boss producer/spawn point or boss prefab component." : null });
                    }
                }
            }
            foreach (var group in rows.GroupBy(r => r.spawnerId).Where(g => g.Count() != 1))
                foreach (var row in group) row.issue = "Producer is referenced by multiple NPC choice owners.";
            return rows.ToArray();
        }
    }
#endif
}
