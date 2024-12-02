using UnityEditor;
using UnityEngine;

namespace RPGKarawara.SkillTree
{
    [CustomEditor(typeof(Skill), true)]
    public class SkillEditor : Editor
    {
        private SerializedProperty skillTypeProp;
        private SerializedProperty cooldownDurationProp;
        private SerializedProperty spriteProp;
        private SerializedProperty nameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty prefabProp;
        private SerializedProperty isUnlockedProp;
        private SerializedProperty activeProp;

        private void OnEnable()
        {
            // Vincula as propriedades ao editor
            skillTypeProp = serializedObject.FindProperty("skillType");
            cooldownDurationProp = serializedObject.FindProperty("cooldownDuration");
            spriteProp = serializedObject.FindProperty("sprite");
            nameProp = serializedObject.FindProperty("name");
            descriptionProp = serializedObject.FindProperty("description");
            prefabProp = serializedObject.FindProperty("prefab");
            isUnlockedProp = serializedObject.FindProperty("isUnlocked");
            activeProp = serializedObject.FindProperty("active");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Cabeçalho com o ícone (do sprite) e o nome
            GUILayout.BeginHorizontal();
            Texture2D iconTexture = null;

            if (spriteProp.objectReferenceValue != null)
            {
                Sprite sprite = (Sprite)spriteProp.objectReferenceValue;
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
            GUILayout.Label(skillTypeProp.enumDisplayNames[skillTypeProp.enumValueIndex], EditorStyles.miniLabel);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // Seções organizadas
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("General Information", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(skillTypeProp);
            EditorGUILayout.PropertyField(nameProp);
            EditorGUILayout.PropertyField(descriptionProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cooldownDurationProp);
            EditorGUILayout.PropertyField(spriteProp);
            EditorGUILayout.PropertyField(prefabProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(isUnlockedProp, new GUIContent("Unlocked"));
            EditorGUILayout.PropertyField(activeProp, new GUIContent("Active"));

            // Botões customizados
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Cooldown"))
            {
                Skill skill = (Skill)target;
                skill.ResetCooldown();
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
