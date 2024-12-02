using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Corruption : MonoBehaviour
{
    public List<GameObject> gameObjectsToDissolve; // Lista de GameObjects cujos filhos têm materiais com a propriedade Erode
    public List<ParticleSystem> vfxEffects; // Lista de efeitos visuais (ou qualquer tipo de VFX que possua o 'Arc')
    public GameObject gameObjectToDeactivate; // GameObject a ser desativado
    public bool dissolveActive; // Controla o comportamento de dissolução e efeitos

    public float dissolveSpeed = 0.5f; // Velocidade com que a dissolução acontece
    public float vfxSpeed = 0.5f; // Velocidade com que o Arc diminui

    private void Update()
    {
        // Se a dissolução estiver ativa
        if (dissolveActive)
        {
            // Modificar o valor de "Erode" dos materiais dos filhos
            DissolveObjects();

            // Diminuir gradualmente o "Arc" dos efeitos visuais
            DecreaseVFXArc();

            // Desativar o GameObject fornecido
            if (gameObjectToDeactivate != null)
            {
                gameObjectToDeactivate.SetActive(false);
            }
        }
        else
        {
            if (gameObjectToDeactivate != null)
            {

                ReverseDissolveObjects();
                ReverseVFXArc();
            }
            // Se dissolveActive for falso, o processo é invertido
        }
    }

    // Função que dissolverá os GameObjects na lista, modificando a propriedade Erode
    private void DissolveObjects()
    {
        foreach (GameObject obj in gameObjectsToDissolve)
        {
            foreach (Transform child in obj.transform)
            {
                Renderer childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    Material material = childRenderer.material;
                    if (material.HasProperty("_Erode"))
                    {
                        // Faz o Erode diminuir gradualmente
                        float erodeValue = material.GetFloat("_Erode");
                        erodeValue += Time.deltaTime * dissolveSpeed; // Incrementa o valor de Erode gradualmente
                        erodeValue = Mathf.Clamp01(erodeValue); // Garante que o valor fique entre 0 e 1
                        material.SetFloat("_Erode", erodeValue);
                    }
                }
            }
        }
    }

    // Função que aumenta o valor de "Arc" dos efeitos visuais (inverso do Decrease)
    private void ReverseVFXArc()
    {
        foreach (ParticleSystem vfx in vfxEffects)
        {
            if (vfx == null) continue;

            var shape = vfx.shape;

            if (shape.enabled)
            {
                // Aumenta gradualmente o valor do arco até o valor original (360)
                float newArc = Mathf.Min(shape.arc + Time.deltaTime * vfxSpeed, 360f);
                shape.arc = newArc; // Atualiza o valor do arco
            }
        }
    }

    // Função que reverte a dissolução dos objetos (inverso do DissolveObjects)
    private void ReverseDissolveObjects()
    {
        foreach (GameObject obj in gameObjectsToDissolve)
        {
            foreach (Transform child in obj.transform)
            {
                Renderer childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    Material material = childRenderer.material;
                    if (material.HasProperty("_Erode"))
                    {
                        // Reverte o valor de Erode (fazendo ele aumentar gradualmente até 0)
                        float erodeValue = material.GetFloat("_Erode");
                        erodeValue -= Time.deltaTime * dissolveSpeed; // Decrementa o valor de Erode
                        erodeValue = Mathf.Clamp01(erodeValue); // Garante que o valor fique entre 0 e 1
                        material.SetFloat("_Erode", erodeValue);
                    }
                }
            }
        }
    }


    // Função que diminui o valor de "Arc" dos efeitos visuais
        private void DecreaseVFXArc()
        {
            foreach (ParticleSystem vfx in vfxEffects)
            {
                if (vfx == null) continue; // Certifica-se de que o ParticleSystem não é nulo

                // Obtém o módulo Shape do ParticleSystem
                var shape = vfx.shape;

                // Verifica se o módulo Shape está habilitado
                if (shape.enabled)
                {
                    // Reduz o valor do arco gradualmente e mantém entre 0 e o valor atual
                    float newArc = Mathf.Max(shape.arc - Time.deltaTime * vfxSpeed, 0f);
                    shape.arc = newArc; // Atualiza o valor
                }
                //Debug.Log($"Processing VFX: {vfx.name}, Arc: {shape.arc}"); 
            }
        }


}