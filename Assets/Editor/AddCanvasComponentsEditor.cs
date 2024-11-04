using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class AddCanvasComponentsEditor : EditorWindow
{
    [MenuItem("Tools/Add Canvas Components")] // Isso cria um item no menu "Tools"
    public static void ShowWindow()
    {
        GetWindow<AddCanvasComponentsEditor>("Add Canvas Components");
    }

    // Objeto selecionado no Editor
    GameObject selectedObject;

    void OnGUI()
    {
        GUILayout.Label("Adicionar Canvas ao Objeto Selecionado", EditorStyles.boldLabel);

        // Mostrar o objeto selecionado
        selectedObject = (GameObject)EditorGUILayout.ObjectField("Objeto Selecionado", selectedObject, typeof(GameObject), true);

        if (selectedObject != null && GUILayout.Button("Adicionar Canvas"))
        {
            AddCanvasComponents(selectedObject);
        }
    }

    void AddCanvasComponents(GameObject obj)
    {
        // Adicionar o componente Canvas
        if (obj.GetComponent<Canvas>() == null)
        {
            Canvas canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // Adicionar o componente CanvasScaler
        if (obj.GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler canvasScaler = obj.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
        }

        // Adicionar o componente GraphicRaycaster
        if (obj.GetComponent<GraphicRaycaster>() == null)
        {
            obj.AddComponent<GraphicRaycaster>();
        }

        Debug.Log("Componentes adicionados ao " + obj.name);
    }
}