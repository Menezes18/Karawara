using System.Collections.Generic;
using UnityEngine;

public class RaycastManager : MonoBehaviour
{
    public List<Position> targetPositions;

    void Start()
    {
        foreach (var position in targetPositions)
        {
            position.hasHitTarget = false;
        }
    }

    public void RegisterHit(GameObject originObject, GameObject hitObject)
    {
        foreach (var position in targetPositions)
        {
            if (position.targetObject == originObject && position.originObject == hitObject)
            {
                position.hasHitTarget = true;
                Debug.Log("Colisão correta detectada: " + originObject.name + " bateu em " + hitObject.name);
                CheckAllTargetsHit();
                break;
            }
        }
    }

    private void CheckAllTargetsHit()
    {
        foreach (var position in targetPositions)
        {
            if (!position.hasHitTarget) return;
        }

        Debug.Log("Parabéns! Todos os alvos foram atingidos.");
    }
}

[System.Serializable]
public class Position
{
    public GameObject originObject;
    public GameObject targetObject;
    public bool hasHitTarget;
}