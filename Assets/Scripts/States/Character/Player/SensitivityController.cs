using RPGKarawara;
using UnityEngine;
using UnityEngine.UI;

public class SensitivityController : MonoBehaviour
{
    public Slider sensitivitySlider; // Referência ao slider na UI
    private float sensitivity = 1f;
    private float leftAndRightRotationSpeed;
    private float upAndDownRotationSpeed;
    public PlayerCamera playerCamera;

    void Start()
    {
        
        sensitivitySlider.onValueChanged.AddListener(playerCamera.SetSensitivity);

        // Configura o valor inicial do slider
        sensitivitySlider.value = sensitivity;
    }

    
}