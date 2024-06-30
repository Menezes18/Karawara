using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OlhaPraCamera : MonoBehaviour
{
    public Camera targetCamera; // Referência à câmera que o canvas deve olhar

    void Start()
    {
        // Se a referência à câmera não foi atribuída no Inspector, atribui a câmera principal
        /*if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }*/
    }

    void Update()
    {
        // Verifica se a referência à câmera é válida
        if (targetCamera != null)
        {
            // Faz com que o canvas olhe para a câmera
            transform.LookAt(transform.position + targetCamera.transform.rotation * Vector3.forward,
                             targetCamera.transform.rotation * Vector3.up);
        }
    }
}
