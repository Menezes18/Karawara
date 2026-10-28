using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara.DevTools.Performance
{
    // Read-only source inspection. Rows alone never exempt Animators from the lifecycle census.
    public static class PerformanceUiSourceInventory
    {
        [Serializable] public sealed class Row
        {
            public int ownerId, sourceRootId, animatorId, graphicId, canvasId, parentId, controllerId, witnessId;
            public int[] declaredReferenceIds;
            public string kind, scene, path, renderingStatus, issue;
            public bool lifecycleBound, referenceValidated, lifecycleCertified = false, renderingCertified = false;
            public bool ownerEnabled, active, animatorEnabled, graphicEnabled, canvasEnabled;
            public float? alpha, fillAmount;
        }
        public static Row[] Capture(string[] args, MonoBehaviour[] owners)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            int flags = 0;
            if (args != null) foreach (var arg in args) if (arg == "-karawaraPerformanceRoster") flags++;
            if (flags == 0) return Array.Empty<Row>();
            if (flags != 1) throw new ArgumentException("Exactly one explicit roster flag required.");
            if (!Application.isPlaying) throw new InvalidOperationException("Actual loaded runtime required for UI source inspection.");
            var rows = new List<Row>();
            if (owners != null) foreach (var owner in owners)
            {
                if (owner == null || !owner.gameObject.scene.IsValid()) continue;
                if (owner is LoadScene loading)
                {
                    // The retained image root may have no Canvas. Report that honestly; don't mutate it.
                    if (!Local(owner, loading.image)) continue;
                    foreach (var animator in loading.image.GetComponentsInChildren<Animator>(true))
                        rows.Add(Describe(owner, loading.image, animator, "loading", new UnityEngine.Object[] { loading.image }, true, false));
                }
                else if (owner is FadeImages skill)
                {
                    // Only the explicitly controlled branch is associated, never the whole PlayerUI tree.
                    if (!Local(owner, skill.galhoFadeCanvas)) continue;
                    bool valid = Local(owner, skill.leafFadeGalho) && Local(owner, skill.painel) && Local(owner, skill.fillImage) && Local(owner, skill.fillIcon);
                    var references = new UnityEngine.Object[] { skill.galhoFadeCanvas, skill.leafFadeGalho, skill.painel, skill.fillImage, skill.fillIcon };
                    foreach (var animator in skill.galhoFadeCanvas.GetComponentsInChildren<Animator>(true))
                        rows.Add(Describe(owner, skill.galhoFadeCanvas.gameObject, animator, "skill-unlock", references, valid, true));
                }
                else if (owner is FadeController fade && fade.animator != null)
                    rows.Add(Describe(owner, owner.gameObject, fade.animator, "screen-fade", new UnityEngine.Object[] { fade.animator }, Local(owner, fade.animator), true));
                else if (owner is System.MiniMap.MiniMapUI minimap && minimap.hitAnimator != null)
                    rows.Add(Describe(owner, owner.gameObject, minimap.hitAnimator, "minimap-damage", new UnityEngine.Object[] { minimap.hitAnimator }, Local(owner, minimap.hitAnimator), true));
            }
            // Shared targets are ambiguous even if each individual owner's references appear well formed.
            var counts = new Dictionary<int, int>();
            foreach (var row in rows) { counts.TryGetValue(row.animatorId, out int count); counts[row.animatorId] = count + 1; }
            var unique = new List<Row>(); var seen = new HashSet<int>();
            foreach (var row in rows)
            {
                if (counts[row.animatorId] != 1) { row.referenceValidated = false; row.issue = "ambiguous-owner"; }
                if (seen.Add(row.animatorId)) unique.Add(row);
            }
            return unique.ToArray();
#else
            return Array.Empty<Row>();
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        static bool Local(MonoBehaviour owner, UnityEngine.Object reference)
        {
            if (reference == null) return false;
            var go = reference as GameObject; if (reference is Component component) go = component.gameObject;
            return go != null && go.scene == owner.gameObject.scene && (go.transform == owner.transform || go.transform.IsChildOf(owner.transform));
        }
        static Row Describe(MonoBehaviour owner, GameObject source, Animator animator, string kind, UnityEngine.Object[] references, bool valid, bool needsCanvas)
        {
            var image = animator.GetComponent<Image>();
            // FadeController animates a child graphic instead of an Image on its own GameObject.
            if (kind == "screen-fade" && image == null) image = animator.GetComponentInChildren<Image>(true);
            var canvas = image == null ? null : image.GetComponentInParent<Canvas>(true);
            var group = animator.GetComponentInParent<CanvasGroup>(true);
            var witness = animator.GetComponent<PerformanceUiSourceWitness>();
            bool local = Local(owner, animator) && Local(owner, image);
            valid &= local && (!needsCanvas || canvas != null && (Local(owner, canvas) || canvas == owner.GetComponentInParent<Canvas>(true)));
            var ids = new int[references.Length]; for (int i = 0; i < ids.Length; i++) ids[i] = references[i] == null ? 0 : references[i].GetInstanceID();
            string path = animator.name; for (var t = animator.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            return new Row { ownerId = owner.GetInstanceID(), sourceRootId = source.GetInstanceID(), animatorId = animator.GetInstanceID(),
                graphicId = image == null ? 0 : image.GetInstanceID(), canvasId = canvas == null ? 0 : canvas.GetInstanceID(),
                parentId = animator.transform.parent == null ? 0 : animator.transform.parent.GetInstanceID(),
                controllerId = animator.runtimeAnimatorController == null ? 0 : animator.runtimeAnimatorController.GetInstanceID(),
                declaredReferenceIds = ids, kind = kind, scene = animator.gameObject.scene.name, path = path,
                referenceValidated = valid, witnessId = witness == null ? 0 : witness.GetInstanceID(), lifecycleBound = valid && witness != null && witness.enabled && witness.animator == animator, renderingStatus = canvas == null ? "no-canvas" : "canvas-present-unverified",
                issue = valid ? "" : "missing-or-foreign-reference", ownerEnabled = owner.enabled, active = animator.gameObject.activeInHierarchy,
                animatorEnabled = animator.enabled, graphicEnabled = image != null && image.enabled, canvasEnabled = canvas != null && canvas.enabled,
                alpha = group == null ? (float?)null : group.alpha, fillAmount = image == null ? (float?)null : image.fillAmount };
        }
#endif
    }
}
