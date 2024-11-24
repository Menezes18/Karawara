using System.Collections.Generic;
using UnityEngine;

public class RaycastManager : MonoBehaviour
{
    public List<Position> targetPositions;
    public GameObject ponte;

    private Dictionary<GameObject, GameObject> targetMap;

    void Start()
    {
        if (targetPositions == null || targetPositions.Count == 0)
        {
            Debug.LogError("Nenhuma posição foi definida no RaycastManager.");
            return;
        }

        targetMap = new Dictionary<GameObject, GameObject>();

        foreach (var position in targetPositions)
        {
            if (position.originObject != null && position.targetObject != null)
            {
                targetMap[position.originObject] = position.targetObject;
                position.hasHitTarget = false;
            }
            else
            {
                Debug.LogError("Um ou mais objetos de origem ou alvo não foram atribuídos em Position.");
            }
        }

        if (ponte != null) ponte.SetActive(false);
        else Debug.LogWarning("Objeto 'ponte' não foi atribuído.");
    }

    public void RegisterHit(GameObject originObject, GameObject hitObject)
    {
        if (targetMap.TryGetValue(originObject, out var target) && target == hitObject)
        {
            foreach (var position in targetPositions)
            {
                if (position.originObject == originObject && position.targetObject == hitObject)
                {
                    position.hasHitTarget = true;
                    Debug.Log("Colisão correta detectada: " + originObject.name + " bateu em " + hitObject.name);
                    CheckAllTargetsHit();
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning("Colisão inválida ou não esperada detectada.");
        }
    }

    private void CheckAllTargetsHit()
    {
        foreach (var position in targetPositions)
        {
            if (!position.hasHitTarget) return;
        }

        Debug.Log("Parabéns! Todos os alvos foram atingidos.");
        if (ponte != null) ponte.SetActive(true);
    }
}

[System.Serializable]
public class Position
{
    public GameObject originObject;
    public GameObject targetObject;
    public bool hasHitTarget;
}
