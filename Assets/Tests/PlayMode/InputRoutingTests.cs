using System.Collections;
using Karawara.Core.Input;
using NUnit.Framework;
using RPGKarawara;
using UnityEngine;
using UnityEngine.TestTools;
namespace Karawara.Tests.Gameplay
{
    public class InputRoutingTests : SessionFixture
    {
        [UnityTest] public IEnumerator DialogueCancelsOpenSwingAndRejectsLateAnimationEvent()
        {
            var player = bootstrap.Session.SpawnQaPlayer(bootstrap.PlayerPrefab, Vector3.zero, Quaternion.identity);
            var input = PlayerInputManager.instance; input.SendMessage("OnApplicationFocus", true);
            player.playerNetworkManager.currentRightHandWeaponID.SetAndNotify(1);
            player.playerNetworkManager.SetCharacterActionHand(true);
            player.isPerformingAction = true; player.State.SetAttacking(true);
            player.playerAnimatorManager.applyRootMotion = true;
            player.playerEquipmentManager.OpenDamageCollider();
            var hitbox = player.playerEquipmentManager.rightWeaponManager.meleeDamageCollider.QaDamageCollider;
            Assert.That(hitbox.enabled, Is.True, "Open-window precondition");
            using (input.Gate.Acquire(InputBlockReason.Dialogue))
            {
                Assert.That(hitbox.enabled, Is.False, "Dialogue left an existing hit window open");
                Assert.That(player.playerAnimatorManager.applyRootMotion, Is.False);
                player.playerEquipmentManager.OpenDamageCollider();
                Assert.That(hitbox.enabled, Is.False, "Late animation event reopened a canceled hitbox");
            }
            player.playerEquipmentManager.OpenDamageCollider();
            Assert.That(hitbox.enabled, Is.False, "Unlock revived an already canceled action");
            yield return null;
        }
        [UnityTest] public IEnumerator IndependentLocksSurvivePlayerReenableAndRevive()
        {
            var player = bootstrap.Session.SpawnQaPlayer(bootstrap.PlayerPrefab, Vector3.zero, Quaternion.identity);
            var input = PlayerInputManager.instance;
            input.SendMessage("OnApplicationFocus", true);
            var dialogue = input.Gate.Acquire(InputBlockReason.Dialogue);
            var pause = input.Gate.Acquire(InputBlockReason.Pause);
            try
            {
                Assert.That(input.playerControls.UI.enabled, Is.True);
                Assert.That(input.playerControls.PlayerActions.enabled, Is.False);
                player.gameObject.SetActive(false); player.gameObject.SetActive(true);
                pause.Dispose();
                Assert.That(input.Gate.IsBlocked, Is.True);
                Assert.That(input.playerControls.PlayerActions.enabled, Is.False);
            }
            finally { pause.Dispose(); dialogue.Dispose(); }
            Assert.That(input.playerControls.PlayerActions.enabled, Is.True);
            player.State.SetHealth(0);
            using (input.Gate.Acquire(InputBlockReason.Pause)) { }
            player.ReviveCharacter();
            Assert.That(input.playerControls.PlayerActions.enabled, Is.True);
            yield return null;
        }
    }
}
