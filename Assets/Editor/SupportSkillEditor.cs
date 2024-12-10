using UnityEditor;
using UnityEngine;

namespace RPGKarawara.SkillTree
{
    [CustomEditor(typeof(SupportSkill))]
    public class SupportSkillEditor : Editor
    {
        // Propriedades herdadas do Skill
        private SerializedProperty skillTypeProp;
        private SerializedProperty cooldownDurationProp;
        private SerializedProperty spriteProp;
        private SerializedProperty nameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty prefabProp;
        private SerializedProperty isUnlockedProp;
        private SerializedProperty activeProp;
        private SerializedProperty vClip;
        private SerializedProperty texture;

        // Propriedades  do SupportSkill
        private SerializedProperty healingRateProp;
        private SerializedProperty healingAmountProp;
        private SerializedProperty healCirclePrefabProp;
        private SerializedProperty skillPrefabJacareProp;
        private SerializedProperty showHealVariablesProp;
        private SerializedProperty showSkillJacareProp;


        private void OnEnable()
        {
            // Vincula as propriedades herdadas
            skillTypeProp = serializedObject.FindProperty("skillType");
            cooldownDurationProp = serializedObject.FindProperty("cooldownDuration");
            spriteProp = serializedObject.FindProperty("sprite");
            nameProp = serializedObject.FindProperty("name");
            descriptionProp = serializedObject.FindProperty("description");
            prefabProp = serializedObject.FindProperty("prefab");
            isUnlockedProp = serializedObject.FindProperty("isUnlocked");
            activeProp = serializedObject.FindProperty("active");
            vClip = serializedObject.FindProperty("vClip");
            texture = serializedObject.FindProperty("texture");

            // Vincula as propriedades específicas do SupportSkill
            healingRateProp = serializedObject.FindProperty("_healingRate");
            healingAmountProp = serializedObject.FindProperty("_healingAmount");
            healCirclePrefabProp = serializedObject.FindProperty("healCirclePrefab");
            skillPrefabJacareProp = serializedObject.FindProperty("skillPrefabJacare");
            showHealVariablesProp = serializedObject.FindProperty("showHealVariables");
            showSkillJacareProp = serializedObject.FindProperty("showSkillJacare");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Cabeçalho com o ícone (do sprite) e o nome
            GUILayout.BeginHorizontal();
            Texture2D iconTexture = null;

            if (spriteProp.objectReferenceValue is Sprite sprite)
            {
                iconTexture = sprite.texture;
            }

            if (iconTexture != null)
            {
                GUILayout.Label(new GUIContent(iconTexture), GUILayout.Width(64), GUILayout.Height(64));
            }
            else
            {
                GUILayout.Label("No Icon", GUILayout.Width(64), GUILayout.Height(64));
            }

            GUILayout.BeginVertical();
            GUILayout.Label(nameProp.stringValue, EditorStyles.boldLabel);
            if (skillTypeProp.enumValueIndex >= 0 && skillTypeProp.enumValueIndex < skillTypeProp.enumDisplayNames.Length)
            {
                GUILayout.Label(skillTypeProp.enumDisplayNames[skillTypeProp.enumValueIndex], EditorStyles.miniLabel);
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // Seções organizadas
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("General Information", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(skillTypeProp, new GUIContent("Skill Type"));
            EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
            EditorGUILayout.PropertyField(descriptionProp, new GUIContent("Description"));
            EditorGUILayout.PropertyField(vClip);
            EditorGUILayout.PropertyField(texture);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cooldownDurationProp, new GUIContent("Cooldown Duration"));
            EditorGUILayout.PropertyField(spriteProp, new GUIContent("Skill Icon"));
            EditorGUILayout.PropertyField(prefabProp, new GUIContent("Prefab"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Support Skill Settings", EditorStyles.boldLabel);

            // Configurações de cura
            showHealVariablesProp.boolValue = EditorGUILayout.Foldout(showHealVariablesProp.boolValue, "Heal Settings");
            if (showHealVariablesProp.boolValue)
            {
                EditorGUILayout.PropertyField(healingRateProp, new GUIContent("Healing Rate"));
                EditorGUILayout.PropertyField(healingAmountProp, new GUIContent("Healing Amount"));
                EditorGUILayout.PropertyField(healCirclePrefabProp, new GUIContent("Heal Circle Prefab"));
            }

            // Configurações do Jacaré
            showSkillJacareProp.boolValue = EditorGUILayout.Foldout(showSkillJacareProp.boolValue, "Skill Jacare Settings");
            if (showSkillJacareProp.boolValue)
            {
                EditorGUILayout.PropertyField(skillPrefabJacareProp, new GUIContent("Jacare Prefab"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(isUnlockedProp, new GUIContent("Unlocked"));
            EditorGUILayout.PropertyField(activeProp, new GUIContent("Active"));

            // Botões customizados
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Cooldown"))
            {
                SupportSkill supportSkill = (SupportSkill)target;
                supportSkill.ResetCooldown();
            }
            if (GUILayout.Button("Unlock Skill"))
            {
                isUnlockedProp.boolValue = true;
            }
            GUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
