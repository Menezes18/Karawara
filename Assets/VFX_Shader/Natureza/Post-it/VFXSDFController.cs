using UnityEngine;
using UnityEngine.VFX;

public class VFXSDFController : MonoBehaviour
{
    public VisualEffect visualEffect; // Referência ao seu Visual Effect
    public Texture3D sdfTexture; // Referência ao seu arquivo SDF (deve ser importado como Texture3D)

    void Start()
    {
        visualEffect.Play();
        // Certifique-se de que o Visual Effect e o SDF estão configurados
        if (visualEffect != null && sdfTexture != null)
        {
            // Defina a propriedade do SDF no VFX Graph
            visualEffect.SetTexture("SDF", sdfTexture);
        }
        else
        {
            Debug.LogWarning("VisualEffect or SDF Texture is not assigned.");
        }
    }

    // Você pode criar métodos adicionais para modificar outras propriedades dinamicamente
    public void UpdateSDFTexture(Texture3D newSDFTexture)
    {
        if (visualEffect != null && newSDFTexture != null)
        {
            visualEffect.SetTexture("SDF", newSDFTexture);
        }
    }
}
