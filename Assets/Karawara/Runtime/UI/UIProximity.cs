using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPGKarawara
{
    public class UIProximity : MonoBehaviour
    {
        public GameObject uiElement;
        public string textGUI = "Press 'E' to interact";
        public TextMeshProUGUI uiProximityText;
        public float detectionRadius = 3f;

        private Transform player;
        private bool hadBinding;
        private bool boundToOwnedPlayer;
        private float nextTaggedLookup;
        private const float TaggedLookupInterval = 1f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void OnEnable() => DevTools.Performance.PerformanceActorLifecycleScope.NotifyVisualChild(this, DevTools.Performance.PerformanceActorLifecycle.Enabled);
        void OnDisable() => DevTools.Performance.PerformanceActorLifecycleScope.NotifyVisualChild(this, DevTools.Performance.PerformanceActorLifecycle.Disabled);
        void OnDestroy() => DevTools.Performance.PerformanceActorLifecycleScope.NotifyVisualChild(this, DevTools.Performance.PerformanceActorLifecycle.Destroyed);
#endif

        void Start()
        {
            ResolvePlayer();
            // Permit the first Update to discover a player created after Start.
            if (player == null) nextTaggedLookup = Time.unscaledTime;
            if (uiElement != null)
            {
                uiElement.SetActive(false);
                uiProximityText = uiElement.GetComponent<TextMeshProUGUI>();
            }

            if (uiProximityText != null)
            {
                uiProximityText.text = textGUI;
            }
        }

        void Update()
        {
            ResolvePlayer();
            if (player == null)
            {
                if (uiElement != null) uiElement.SetActive(false);
                return;
            }
            bool isPlayerNearby = Vector3.Distance(player.position, transform.position) <= detectionRadius;
            if (uiElement != null) uiElement.SetActive(isPlayerNearby);

            if (isPlayerNearby && uiProximityText != null)
            {
                uiProximityText.text = textGUI;
            }
        }

        private void ResolvePlayer()
        {
            var owned = WorldSaveGameManager.instance != null ? WorldSaveGameManager.instance.player : null;
            if (owned != null && owned.IsInitialized && GameBootstrap.Instance != null && GameBootstrap.Instance.Session.Player == owned)
            {
                player = owned.transform;
                hadBinding = true;
                boundToOwnedPlayer = true;
                return;
            }
            // A despawned owned player must not be replaced with a stale authored tag.
            if (boundToOwnedPlayer)
            {
                player = null;
                hadBinding = false;
                return;
            }
            if (player != null && player.gameObject.activeInHierarchy) return;
            if (hadBinding)
            {
                player = null;
                hadBinding = false;
                nextTaggedLookup = Time.unscaledTime;
            }
            if (Time.unscaledTime < nextTaggedLookup) return;
            nextTaggedLookup = Time.unscaledTime + TaggedLookupInterval;
            var tagged = GameObject.FindGameObjectWithTag("Player");
            player = tagged != null ? tagged.transform : null;
            hadBinding = player != null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
