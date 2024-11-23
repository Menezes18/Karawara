using UnityEditor;
using UnityEngine;

namespace RPGKarawara.SkillTree {
    [CustomEditor(typeof(SupportSkill))]
    public class SupportSkillEditor : Editor {
        public override void OnInspectorGUI() {

            SupportSkill supportSkill = (SupportSkill)target;


            DrawDefaultInspector();

            // Adicionar um botão de toggle para exibir ou esconder variáveis específicas
            supportSkill.showHealVariables = EditorGUILayout.Foldout(supportSkill.showHealVariables, "Heal Settings");
            
            if (supportSkill.showHealVariables) {
                EditorGUILayout.LabelField("Heal Settings", EditorStyles.boldLabel);
                supportSkill._healingRate = EditorGUILayout.FloatField("Healing Rate", supportSkill._healingRate);
                supportSkill._healingAmount = EditorGUILayout.IntField("Healing Amount", supportSkill._healingAmount);
                supportSkill.healCirclePrefab = (GameObject)EditorGUILayout.ObjectField("Heal Circle Prefab", supportSkill.healCirclePrefab, typeof(GameObject), false);
            }


            if (GUI.changed) {
                EditorUtility.SetDirty(supportSkill);
            }
        }
    }
}