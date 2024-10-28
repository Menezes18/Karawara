using UnityEditor;
using UnityEngine;
using RPGKarawara;
[CustomEditor(typeof(Skill), true)] // true permite herdar as classes derivadas
public class SkillEditor : Editor
{
    public override void OnInspectorGUI()
    {
        
        Skill skill = (Skill)target;

        
        if (skill.sprite != null)
        {
            GUILayout.Label(skill.sprite.texture, GUILayout.Width(64), GUILayout.Height(64));
        }

        
        DrawDefaultInspector();
    }
}