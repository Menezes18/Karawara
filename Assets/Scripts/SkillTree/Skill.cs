using System;
using UnityEngine;

namespace RPGKarawara.SkillTree {
    public abstract class Skill : ScriptableObject {
        public float cooldownDuration;
        private float lastActivationTime;

        public bool IsOnCooldown {
            get {
                return Time.time < lastActivationTime + cooldownDuration;
            }
        }

        private void OnEnable() {
            ResetCooldown();
        }

        public void ResetCooldown() {
            lastActivationTime = -cooldownDuration;
        }

        public void Activate(GameObject user) {
            if (!IsOnCooldown) {
                lastActivationTime = Time.time;
                Execute(user);
            } else {
                Debug.Log("Skill is on cooldown.");
            }
        }

        protected abstract void Execute(GameObject user);
    }
}