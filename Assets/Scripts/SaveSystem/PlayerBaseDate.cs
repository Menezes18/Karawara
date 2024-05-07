using System;
using UnityEngine;

namespace RPGKarawara
{
    [Serializable]
    public class PlayerBaseDate
    {
        public int Level;
        public Vector3 pos;

        
        public PlayerBaseDate(int Level, Vector3 pos)
        {
            this.Level = Level;
            this.pos = pos;
        }
    }
}
