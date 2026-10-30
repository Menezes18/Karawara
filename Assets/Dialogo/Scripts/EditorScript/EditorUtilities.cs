#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;

using UnityEngine;

namespace DialogueSystem
{
    public class EditorUtilities
    {
        private static int reorderableListIndex = -1;

        public static ReorderableList CreateReorderableList(string fieldName, List<DialogueEvent> ilist, SerializedProperty arrayData, Type type, SerializedObject serializedObject, Action<ReorderableList> onCreation, int contentHeight)
        {
            bool hasArrayData = arrayData != null && arrayData.isArray;
            XNode.Node node = serializedObject.targetObject as XNode.Node;
            ReorderableList list = new ReorderableList(serializedObject, arrayData, true, true, true, true);
            string label = arrayData != null ? arrayData.displayName : ObjectNames.NicifyVariableName(fieldName);

            list.drawElementCallback += (Rect rect, int index, bool isActive, bool isFocused) => {
                if (hasArrayData && arrayData.propertyType != SerializedPropertyType.String)
                {
                    if (arrayData.arraySize <= index)
                    {
                        EditorGUI.LabelField(rect, "Array[" + index + "] data out of range");
                        return;
                    }
                    SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index);
                    EditorGUI.PropertyField(rect, itemData, true);
                }
                else EditorGUI.LabelField(rect, "asd");
            };

            list.elementHeightCallback = (int index) =>
            {
                if (hasArrayData)
                {
                    if (arrayData.arraySize <= index) return EditorGUIUtility.singleLineHeight;
                    SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index);
                    if (!itemData.isExpanded) return 23;
                    return EditorGUI.GetPropertyHeight(itemData) == 23 ? 138 : EditorGUI.GetPropertyHeight(itemData);
                }
                else return EditorGUIUtility.singleLineHeight;
            };

            list.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, label);
            };

            list.onSelectCallback = (ReorderableList rl) => {
                reorderableListIndex = rl.index;
            };

            list.onReorderCallback = (ReorderableList rl) => {
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();

                if (hasArrayData)
                {
                    arrayData.MoveArrayElement(reorderableListIndex, rl.index);
                }

                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                XNodeEditor.NodeEditorWindow.current.Repaint();
                EditorApplication.delayCall += XNodeEditor.NodeEditorWindow.current.Repaint;
            };

            list.onAddCallback = (ReorderableList rl) => {
                serializedObject.Update();
                if (hasArrayData)
                {
                    arrayData.InsertArrayElementAtIndex(arrayData.arraySize);
                }
                serializedObject.ApplyModifiedProperties();
            };

            list.onRemoveCallback = (ReorderableList rl) => {
                serializedObject.Update();
                int index = rl.index;

                if (hasArrayData && arrayData.propertyType != SerializedPropertyType.String)
                {
                    if (arrayData.arraySize <= index)
                    {
                        Debug.LogWarning("Attempted to remove array index " + index + " where only " + arrayData.arraySize + " exist - Skipped");
                        return;
                    }
                    arrayData.DeleteArrayElementAtIndex(index);
                }

                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            };

            if (hasArrayData)
            {
                int dynamicPortCount = ilist.Count;
                while (dynamicPortCount < arrayData.arraySize)
                {
                    EditorUtility.SetDirty(node);
                    dynamicPortCount++;
                }
                while (arrayData.arraySize < dynamicPortCount)
                {
                    arrayData.InsertArrayElementAtIndex(arrayData.arraySize);
                }
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            onCreation?.Invoke(list);
            return list;
        }

        public static ReorderableList GetListWithFoldout(SerializedObject serializedObject, SerializedProperty property, bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton)
        {
            var list = new ReorderableList(serializedObject, property, draggable, displayHeader, displayAddButton, displayRemoveButton);

            list.drawHeaderCallback = (Rect rect) => {
                var newRect = new Rect(rect.x + 10, rect.y, rect.width - 10, rect.height);
                property.isExpanded = EditorGUI.Foldout(newRect, property.isExpanded, property.displayName);
            };

            list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                if (!property.isExpanded)
                {
                    GUI.enabled = index == list.count;
                    return;
                }

                var element = list.serializedProperty.GetArrayElementAtIndex(index);
                rect.y += 2;
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            list.elementHeightCallback = (int indexer) => {
                if (!property.isExpanded)
                    return 0;
                else
                    return list.elementHeight;
            };

            return list;
        }
    }
}
#endif