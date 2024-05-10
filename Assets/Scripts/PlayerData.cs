using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [CreateAssetMenu(menuName = "Player")]
    public class PlayerData : ScriptableObject{
        
        [field: SerializeField] public int vida = 100;
        [field: SerializeField] public float stamina = 5f;
        [field: SerializeField] public int maxVida = 100;
    }
}
