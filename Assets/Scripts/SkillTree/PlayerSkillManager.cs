using System;
using UnityEngine;
using UnityEngine.Events;

namespace RPGKarawara.SkillTree{
    public class PlayerSkillManager : MonoBehaviour{
        private int _life, _speed, _damage;
        private int _skillPoints;

        public int Life => _life;
        public int Speed => _speed;
        public int Damage => _damage;

        public int SkillPoints => _skillPoints;

        public UnityAction OnSkillPointsChanged;

        private void Awake(){
            _skillPoints = 10;
            _life = 0;
            _speed = 0;
            _damage = 0;
        }

        public void GainSkillPoint(){
            _skillPoints++;
            OnSkillPointsChanged?.Invoke();
        }
    }
}