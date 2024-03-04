using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [Serializable]
    public class SlopeData 
    {
        [field: SerializeField][field: Range(0, 1)] public float StepHeightPercentage { get; private set; } = 0.25f;
    }
}
