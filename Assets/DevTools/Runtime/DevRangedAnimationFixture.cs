using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RPGKarawara.DevTools
{
    public class DevRangedAnimationFixture : MonoBehaviour
    {
        private readonly List<Object> temporaryAssets = new List<Object>();
        // Used only on an instantiated development enemy, never its production prefab.
        public static void Configure(GameObject instance)
        {
            if (instance.GetComponentInChildren<MagicSpeel>(true) == null ||
                instance.GetComponentInChildren<AISaciCombatManager>(true) != null) return;
            if (instance.GetComponent<DevRangedAnimationFixture>() != null) return;
            var lifetime = instance.AddComponent<DevRangedAnimationFixture>();
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
            {
                var source = animator.runtimeAnimatorController;
                if (source == null) continue;
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                foreach (var clip in source.animationClips.Distinct())
                {
                    var events = clip.events;
                    if (!events.Any(e => e.functionName == "ActivateSaciWhirlwind")) continue;
                    var copy = Object.Instantiate(clip);
                    copy.name = clip.name + " (Dev ranged)";
                    copy.hideFlags = HideFlags.HideAndDontSave;
                    lifetime.temporaryAssets.Add(copy);
                    copy.events = events.Where(e => e.functionName != "ActivateSaciWhirlwind").ToArray();
                    overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip, copy));
                }
                if (overrides.Count == 0) continue;
                var controller = new AnimatorOverrideController(source) { hideFlags = HideFlags.HideAndDontSave };
                lifetime.temporaryAssets.Add(controller);
                controller.ApplyOverrides(overrides);
                animator.runtimeAnimatorController = controller;
            }
        }

        private void OnDestroy()
        {
            foreach (var asset in temporaryAssets) if (asset != null) Destroy(asset);
        }
    }
}
