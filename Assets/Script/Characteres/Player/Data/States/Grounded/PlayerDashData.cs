using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [Serializable]
    public class PlayerDashData
    {
        [field: SerializeField] [field: Range(1f, 3f)] public float SpeedModifier{get; private set;} = 2f;
    }
}
