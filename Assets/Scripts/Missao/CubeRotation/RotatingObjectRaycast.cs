using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RaycastObject : MonoBehaviour
{
    public float raycastLength = 10f;
    public LayerMask detectionLayer;
    public RaycastManager manager;
    public GameObject originObject;
    public Material lineMaterial; // Material para o LineRenderer

    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.material = lineMaterial; // Aplica o material ao LineRenderer
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
    }

    void Update()
    {
        Vector3 startPoint = transform.position;
        Vector3 endPoint = transform.position + transform.forward * raycastLength;

        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        RaycastHit hit;
        if (Physics.Raycast(startPoint, transform.forward, out hit, raycastLength, detectionLayer))
        {
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.red;
            Debug.Log("TRUE");
            manager.RegisterHit(originObject, hit.collider.gameObject);
        }
        
    }
}