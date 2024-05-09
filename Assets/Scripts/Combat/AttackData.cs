using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [CreateAssetMenu(menuName = "Combat System/Create a new attack")]
    public class AttackData : ScriptableObject
    {
        [field: SerializeField] public string AnimName { get; private set; }
        [field: SerializeField] public AttackHitbox HitboxToUse { get; private set; }
        [field: SerializeField] public float ImpactStartTime { get; private set; }
        [field: SerializeField] public float ImpactEndTime { get; private set; }
        
        [field: SerializeField] public bool MoveToTarget { get; private set; }

        [field: SerializeField] public float ImpactMoveDistance { get; private set; }




    }
}
public enum AttackHitbox { LeftHand, RightHand, LeftFoot, RightFoot, Sword }