using System;
using System.Collections.Generic;

namespace Karawara.Core.Characters
{
    /// <summary>Pure local state; presentation and gameplay side effects belong to consumers.</summary>
    public sealed class CharacterRuntimeState
    {
        public int MaxHealth { get; private set; } = 400;
        public int Health { get; private set; } = 400;
        public bool IsDead => Health == 0;
        public bool IsActive { get; private set; } = true;
        public CharacterRuntimeState Target { get; private set; }
        public int Vitality { get; private set; } = 1;
        public int Endurance { get; private set; } = 1;
        public bool IsMoving { get; private set; }
        public bool IsChargingAttack { get; private set; }
        public bool IsLockedOn { get; private set; }
        public bool IsBlocking { get; private set; }
        public bool IsPowerPet { get; private set; }
        public bool IsInvulnerable { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool IsPerformingAction { get; private set; }
        public void SetPerformingAction(bool value) => IsPerformingAction = value;

        public bool IsUsingRightHand { get; private set; }
        public event Action<bool, bool> IsUsingRightHandChanged;
        public void SetIsUsingRightHand(bool value)
        {
            if (IsUsingRightHand == value) return;
            bool previous = IsUsingRightHand;
            IsUsingRightHand = value;
            IsUsingRightHandChanged?.Invoke(previous, value);
        }
        public bool IsUsingLeftHand { get; private set; }
        public event Action<bool, bool> IsUsingLeftHandChanged;
        public void SetIsUsingLeftHand(bool value)
        {
            if (IsUsingLeftHand == value) return;
            bool previous = IsUsingLeftHand;
            IsUsingLeftHand = value;
            IsUsingLeftHandChanged?.Invoke(previous, value);
        }
        public bool IsChargingRightSpell { get; private set; }
        public event Action<bool, bool> IsChargingRightSpellChanged;
        public void SetIsChargingRightSpell(bool value)
        {
            if (IsChargingRightSpell == value) return;
            bool previous = IsChargingRightSpell;
            IsChargingRightSpell = value;
            IsChargingRightSpellChanged?.Invoke(previous, value);
        }
        public bool IsChargingLeftSpell { get; private set; }
        public event Action<bool, bool> IsChargingLeftSpellChanged;
        public void SetIsChargingLeftSpell(bool value)
        {
            if (IsChargingLeftSpell == value) return;
            bool previous = IsChargingLeftSpell;
            IsChargingLeftSpell = value;
            IsChargingLeftSpellChanged?.Invoke(previous, value);
        }

        public event Action<int, int> HealthChanged;
        public event Action<int, int> MaxHealthChanged;
        public event Action<bool, bool> ActiveChanged;
        public event Action<CharacterRuntimeState, CharacterRuntimeState> TargetChanged;
        public event Action Died;
        public event Action<int, int> VitalityChanged;
        public event Action<bool, bool> MovingChanged;
        public event Action<bool, bool> ChargingAttackChanged;
        public event Action<bool, bool> LockedOnChanged;

        internal CharacterRegistry Registry { get; set; }
        internal event Action<CharacterRuntimeState> Unavailable;
        private readonly Queue<Action> targetNotifications = new Queue<Action>();
        private bool publishingTarget;

        // Stat calculation/refill is an explicit gameplay subscriber, not a Core side effect.
        public void SetVitality(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (Vitality == value) return;
            int previous = Vitality;
            Vitality = value;
            VitalityChanged?.Invoke(previous, value);
        }

        public void SetEndurance(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Endurance = value;
        }

        public void SetMoving(bool value)
        {
            if (IsMoving == value) return;
            bool previous = IsMoving;
            IsMoving = value;
            MovingChanged?.Invoke(previous, value);
        }

        public void SetChargingAttack(bool value)
        {
            if (IsChargingAttack == value) return;
            bool previous = IsChargingAttack;
            IsChargingAttack = value;
            ChargingAttackChanged?.Invoke(previous, value);
        }

        public void SetLockedOn(bool value)
        {
            if (IsLockedOn == value) return;
            if (!value && Target != null)
            {
                SetTarget(null);
                return;
            }
            bool previous = IsLockedOn;
            IsLockedOn = value;
            PublishTargetChanges(() => LockedOnChanged?.Invoke(previous, value));
        }

        public void SetBlocking(bool value) => IsBlocking = value;
        public void SetPowerPet(bool value) => IsPowerPet = value;
        public void SetInvulnerable(bool value) => IsInvulnerable = value;
        public void SetSprinting(bool value) => IsSprinting = value;
        public void SetJumping(bool value) => IsJumping = value;
        public void SetAttacking(bool value) => IsAttacking = value;

        public void SetMaxHealth(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (MaxHealth == value) return;
            int previous = MaxHealth;
            MaxHealth = value;
            SetHealth(Health);
            MaxHealthChanged?.Invoke(previous, value);
        }

        public void SetHealth(int value)
        {
            int next = Math.Max(0, Math.Min(MaxHealth, value));
            if (Health == next) return;
            int previous = Health;
            Health = next;
            if (IsDead) Invalidate();
            HealthChanged?.Invoke(previous, next);
            if (previous > 0 && next == 0) Died?.Invoke();
        }

        public void SetActive(bool value)
        {
            if (IsActive == value) return;
            bool previous = IsActive;
            IsActive = value;
            if (!value) Invalidate();
            ActiveChanged?.Invoke(previous, value);
        }

        public void SetTarget(CharacterRuntimeState value)
        {
            if (ReferenceEquals(value, this)) throw new ArgumentException("A character cannot target itself.", nameof(value));
            if (value != null && (!value.IsActive || value.IsDead)) value = null;
            if (ReferenceEquals(Target, value))
            {
                if (value == null) SetLockedOn(false);
                return;
            }
            var previous = Target;
            if (previous != null) previous.Unavailable -= OnTargetUnavailable;
            Target = value;
            if (value != null) value.Unavailable += OnTargetUnavailable;
            bool unlock = value == null && IsLockedOn;
            if (unlock) IsLockedOn = false;
            // Publish the complete transition before any reentrant replacement notification.
            PublishTargetChanges(() => TargetChanged?.Invoke(previous, value),
                unlock ? (Action)(() => LockedOnChanged?.Invoke(true, false)) : null);
        }

        private void PublishTargetChanges(Action first, Action second = null)
        {
            targetNotifications.Enqueue(first);
            if (second != null) targetNotifications.Enqueue(second);
            if (publishingTarget) return;
            publishingTarget = true;
            try
            {
                while (targetNotifications.Count > 0) targetNotifications.Dequeue().Invoke();
            }
            finally
            {
                targetNotifications.Clear();
                publishingTarget = false;
            }
        }

        internal void Invalidate()
        {
            SetTarget(null);
            Unavailable?.Invoke(this);
        }

        private void OnTargetUnavailable(CharacterRuntimeState unavailable)
        {
            if (ReferenceEquals(Target, unavailable)) SetTarget(null);
        }
    }
}
