using System;
using System.Collections;
using System.Collections.Generic;
using RPGKarawara.SkillTree;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class SkillHotbar : MonoBehaviour
    {
        public Image skill1;
        public Image skill2;
        public Image skill3;

        private PlayerSkillManager skillManager;
        
        private void Awake()
        {
            Debug.Log(this.gameObject.name + " is Awake");
            skillManager = PlayerSkillManager.instance;
            UpdateUI();
        }

        public void UpdateUI()
        {
            // skill1.sprite = skillManager.attackSkill.sprite;
            // skill2.sprite = skillManager.defenseSkill.sprite;
            // skill3.sprite = skillManager.supportSkill.sprite;
        }
    }
}
