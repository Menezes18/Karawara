using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [Serializable]
    public class PlayerJumpData
    {
        [field: SerializeField] public Vector3 StationaryForce {  get; set; }
        [field: SerializeField] public Vector3 WeakForce { get; set; }
        [field: SerializeField] public Vector3 MediumForce { get; set; }
        [field: SerializeField] public Vector3 StrongForce { get; set; }

    }
}
