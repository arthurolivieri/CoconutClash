using UnityEngine;

public class ProjectileTrailVisualizer : MonoBehaviour
{
    [Header("Trail Settings")]
    [SerializeField] private bool enableTrail = true;
    [SerializeField] private float trailDuration = 2f;
    [SerializeField] private Color trailColor = new Color(1f, 0.5f, 0f, 0.5f);
    [SerializeField] private float trailWidth = 0.1f;

    [Header("Trail Quality")]
    [SerializeField] private int maxTrailPoints = 50;
    [SerializeField] private float minDistanceBetweenPoints = 0.1f;

    private LineRenderer lineRenderer;
    private Vector3[] trailPoints;
    private int currentPointIndex = 0;
    private float lastPointDistance = 0f;

    private void Start()
    {
        if (!enableTrail) return;

        SetupLineRenderer();
        trailPoints = new Vector3[maxTrailPoints];

        // Initialize first point
        trailPoints[0] = transform.position;
        currentPointIndex = 1;
    }

    private void SetupLineRenderer()
    {
        // Add LineRenderer if not present
        lineRenderer = gameObject.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // Configure LineRenderer
        lineRenderer.startWidth = trailWidth;
        lineRenderer.endWidth = trailWidth * 0.5f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = trailColor;
        lineRenderer.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
        lineRenderer.numCapVertices = 5;
        lineRenderer.numCornerVertices = 5;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
    }

    private void Update()
    {
        if (!enableTrail || lineRenderer == null) return;

        UpdateTrail();
    }

    private void UpdateTrail()
    {
        // Check if we should add a new point
        float distanceFromLastPoint = Vector3.Distance(transform.position, trailPoints[Mathf.Max(0, currentPointIndex - 1)]);

        if (distanceFromLastPoint >= minDistanceBetweenPoints)
        {
            // Add new point
            if (currentPointIndex < maxTrailPoints)
            {
                trailPoints[currentPointIndex] = transform.position;
                currentPointIndex++;
            }
            else
            {
                // Shift array and add to end
                for (int i = 0; i < maxTrailPoints - 1; i++)
                {
                    trailPoints[i] = trailPoints[i + 1];
                }
                trailPoints[maxTrailPoints - 1] = transform.position;
            }

            // Update line renderer
            lineRenderer.positionCount = currentPointIndex;
            lineRenderer.SetPositions(trailPoints);
        }
    }

    private void OnDestroy()
    {
        // Clean up line renderer when projectile is destroyed
        if (lineRenderer != null)
        {
            Destroy(lineRenderer);
        }
    }

    // Public method to enable/disable trail at runtime
    public void SetTrailEnabled(bool enabled)
    {
        enableTrail = enabled;
        if (lineRenderer != null)
        {
            lineRenderer.enabled = enabled;
        }
    }

    // Public method to change trail color
    public void SetTrailColor(Color color)
    {
        trailColor = color;
        if (lineRenderer != null)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
