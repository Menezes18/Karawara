#if UNITY_EDITOR
using System;
using Karawara.Core.Characters;
using UnityEngine;

namespace RPGKarawara.DevTools
{
    public sealed class LocalDamageValidationProbe : DamageCollider
    {
        public bool CanHit(CharacterManager target) => CanDamage(target);
    }

    public static class LocalPlayerReviewRegression
    {
        public static void DeadAttackerCannotHitNewTarget()
        {
            var sourceObject = new GameObject("G2 source validation");
            var targetObject = new GameObject("G2 target validation");
            try
            {
                var source = sourceObject.AddComponent<CharacterManager>();
                var target = targetObject.AddComponent<CharacterManager>();
                source.characterGroup = CharacterGroup.Team01;
                target.characterGroup = CharacterGroup.Team02;
                var registry = new CharacterRegistry();
                source.InitializeLocal(source.State, registry); target.InitializeLocal(target.State, registry);
                var probe = sourceObject.AddComponent<LocalDamageValidationProbe>();
                probe.sourceCharacter = source;
                if (!probe.CanHit(target)) throw new InvalidOperationException("Live hostile source precondition failed.");
                source.State.SetHealth(0);
                if (probe.CanHit(target)) throw new InvalidOperationException("Dead attacker can still hit a fresh target.");
            }
            finally { UnityEngine.Object.DestroyImmediate(sourceObject); UnityEngine.Object.DestroyImmediate(targetObject); }
        }

        public static void SessionPlayerReenableRestoresInput()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Requires Play Mode at the menu.");
            var bootstrap = GameBootstrap.Instance;
            var session = bootstrap.Session;
            try
            {
                var player = session.SpawnQaPlayer(bootstrap.PlayerPrefab, Vector3.zero, Quaternion.identity);
                var input = PlayerInputManager.instance;
                if (input.player != player || !input.playerControls.asset.enabled) throw new InvalidOperationException("Active input precondition failed.");
                player.gameObject.SetActive(false);
                player.gameObject.SetActive(true);
                if (!player.IsInitialized || input.player != player || !input.playerControls.asset.enabled)
                    throw new InvalidOperationException("Re-enabled session player lost input binding/control.");
                input.SetSessionInputEnabled(false);
                player.gameObject.SetActive(false); player.gameObject.SetActive(true);
                if (input.player != player || input.playerControls.asset.enabled)
                    throw new InvalidOperationException("Re-enable must preserve an existing input block.");
                var weapon = new GameObject("G2 active hit window");
                input.SetSessionInputEnabled(true);
                weapon.transform.SetParent(player.transform);
                var collider = weapon.AddComponent<BoxCollider>();
                var probe = weapon.AddComponent<LocalDamageValidationProbe>();
                probe.EnableDamageCollider();
                if (!collider.enabled) throw new InvalidOperationException("Open hit-window precondition failed.");
                player.State.SetHealth(0);
                if (collider.enabled) throw new InvalidOperationException("Player death left its active hitbox open.");
                player.gameObject.SetActive(false); player.gameObject.SetActive(true);
                player.ReviveCharacter();
                if (player.State.IsDead || input.player != player || !input.SessionInputEnabled || !input.playerControls.asset.enabled)
                    throw new InvalidOperationException("Death/re-enable/revive permanently lost the prior input intent.");
            }
            finally { session.DisposeSession(); }
        }
    }
}
#endif
