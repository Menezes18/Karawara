using UnityEngine;
using UnityEngine.UI;

public class FPSDisplay : MonoBehaviour
{
    public Text fpsText; // UI Text element to display the FPS
    private float deltaTime;

    void Update()
    {
        // Calculate the time between frames
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        
        // Calculate the FPS
        float fps = 1.0f / deltaTime;

        // Display the FPS
        if (fpsText != null)
        {
            fpsText.text = "FPS: " + Mathf.Ceil(fps).ToString();
        }
    }
}