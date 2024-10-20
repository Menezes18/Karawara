using UnityEngine;
using UnityEditor;

public class LayerAssigner : EditorWindow
{
    private GameObject targetObject;

    [MenuItem("Tools/Assign Damageable Character Layer")]
    public static void ShowWindow()
    {
        GetWindow<LayerAssigner>("Assign Damageable Character Layer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Assign 'Damageable Character' Layer", EditorStyles.boldLabel);
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);

        if (GUILayout.Button("Assign Layer"))
        {
            if (targetObject != null)
            {
                AssignLayerToChildren(targetObject);
                Debug.Log("Layer assignment completed.");
            }
            else
            {
                Debug.LogWarning("Please select a target object.");
            }
        }
    }

    private void AssignLayerToChildren(GameObject obj)
    {
        // Get the layer index for 'Damageable Character'
        int damageableLayer = LayerMask.NameToLayer("Damageble Character");
        int CharacterLayer = LayerMask.NameToLayer("Character");
        if (damageableLayer == -1)
        {
            Debug.LogError("'Damageable Character' layer does not exist. Please add the layer in the Tags & Layers settings.");
            return;
        }

        // Recursively assign the layer to the children with a CapsuleCollider
        AssignLayerRecursively(obj.transform, damageableLayer, CharacterLayer);
    }

    private void AssignLayerRecursively(Transform currentTransform, int layer, int CharacterLayer)
    {
        // If the current object has a CapsuleCollider, assign the layer
        if (currentTransform.GetComponent<CapsuleCollider>() != null)
        {
            currentTransform.gameObject.layer = layer;
        }
        else
        {
            currentTransform.gameObject.layer = CharacterLayer;
        }

        // Recursively call this method for each child
        foreach (Transform child in currentTransform)
        {
            AssignLayerRecursively(child, layer,CharacterLayer);
        }
    }
}
