using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [Serializable]
    public class PlayerStats
    {
        [field: SerializeField][field: Range(0, 20)] public int Level { get; private set; } = 0;
        [field: SerializeField][field: Range(1f, 25f)] public float Strenght { get; private set; } = 15f;
        [field: SerializeField][field: Range(1f, 25f)] public float Speed { get; private set; } = 15f;
        [field: SerializeField][field: Range(1f, 100f)] public float Stamina { get; private set; } = 100f;

    }
}
