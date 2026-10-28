using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceSkillCooldownValidation
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Owned inactive EditMode coroutine fixture required.");
            var cases = new List<object>();
            var previousUi = SkillCooldownUI.instance; var previousManager = PlayerSkillManager.instance;
            foreach (string scenario in new[] { "HUD missing before cooldown", "Image destroyed during cooldown", "HUD array released during cooldown" })
            {
                var container = new GameObject("Owned cooldown boundary fixture"); container.SetActive(false);
                try
                {
                    var manager = container.AddComponent<PlayerSkillManager>(); manager.slot = new[] { new SkillSlot { slotCooldownDuration = 100 } };
                    var ui = container.AddComponent<SkillCooldownUI>();
                    var imageRoot = new GameObject("Owned cooldown image", typeof(RectTransform)); imageRoot.transform.SetParent(container.transform);
                    var image = imageRoot.AddComponent<Image>(); ui.skillCooldownImage = new[] { image };
                    SkillCooldownUI.instance = scenario.StartsWith("HUD missing") ? null : ui;
                    var timer = (IEnumerator)typeof(PlayerSkillManager).GetMethod("SlotCooldownTimer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { 0 });
                    bool first = timer.MoveNext();
                    if (scenario.StartsWith("Image")) UnityEngine.Object.DestroyImmediate(imageRoot);
                    if (scenario.StartsWith("HUD array")) ui.skillCooldownImage = null;
                    bool second = timer.MoveNext();
                    (timer as IDisposable)?.Dispose();
                    cases.Add(new { name = scenario, passed = first && second, detail = "Actual production enumerator tolerates missing UI while its cooldown continues" });
                }
                catch (Exception error) { cases.Add(new { name = scenario, passed = false, detail = error.ToString() }); }
                finally { UnityEngine.Object.DestroyImmediate(container); SkillCooldownUI.instance = previousUi; PlayerSkillManager.instance = previousManager; }
            }
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/skill-cooldown-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, boundary = "Manual actual production coroutine steps with owned inactive objects; no natural cooldown, Player lifecycle or global retention acceptance" }, Formatting.Indented)); return path;
        }
    }
}
