using UnityEngine;

public class AIAimVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    [SerializeField] private bool showAimLine = true;
    [SerializeField] private bool showAccuracyCircle = true;
    [SerializeField] private bool showPredictedTrajectory = false;
    [SerializeField] private Color aimLineColor = Color.cyan;
    [SerializeField] private Color accuracyCircleColor = new Color(1f, 1f, 0f, 0.3f);
    [SerializeField] private Color hitColor = Color.green;
    [SerializeField] private Color missColor = Color.red;

    [Header("References")]
    [SerializeField] private EnemyShooterAdvancedAI aiController;
    [SerializeField] private Transform target;

    [Header("Trajectory Preview")]
    [SerializeField] private int trajectoryPointCount = 20;
    [SerializeField] private float trajectoryTimeStep = 0.1f;

    private Vector3 lastAimPoint;
    private bool lastShotWasHit = false;
    private float lastShotTime = 0f;
    private float shotIndicatorDuration = 2f;

    private void Start()
    {
        if (aiController == null)
        {
            aiController = GetComponent<EnemyShooterAdvancedAI>();
        }

        if (target == null && aiController != null)
        {
            target = aiController.GetTarget();
        }
    }

    private void OnDrawGizmos()
    {
        if (!enabled || aiController == null || target == null) return;

        DrawAccuracyCircle();
        DrawAimLine();
        DrawLastShotIndicator();
    }

    private void OnDrawGizmosSelected()
    {
        if (!enabled || aiController == null || target == null) return;

        DrawDetailedInfo();

        if (showPredictedTrajectory)
        {
            DrawTrajectoryPreview();
        }
    }

    private void DrawAccuracyCircle()
    {
        if (!showAccuracyCircle || target == null) return;

        float accuracy = aiController.GetAimAccuracy();
        float maxOffset = GetMaxAimOffset();

        // Draw circle showing potential miss area
        Gizmos.color = accuracyCircleColor;
        DrawCircle(target.position, maxOffset * (1f - accuracy), 32);

        // Draw smaller circle for actual target
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
        DrawCircle(target.position, 0.3f, 16);
    }

    private void DrawAimLine()
    {
        if (!showAimLine) return;

        // Get current accuracy
        float accuracy = aiController.GetAimAccuracy();

        // Color based on accuracy
        Color lineColor = Color.Lerp(missColor, hitColor, accuracy);
        lineColor.a = 0.7f;
        Gizmos.color = lineColor;

        // Draw line from shooter to target
        Gizmos.DrawLine(transform.position, target.position);

        // Draw arrow head
        Vector3 direction = (target.position - transform.position).normalized;
        Vector3 arrowPoint = target.position - direction * 0.5f;
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f) * 0.3f;

        Gizmos.DrawLine(target.position, arrowPoint + perpendicular);
        Gizmos.DrawLine(target.position, arrowPoint - perpendicular);
    }

    private void DrawLastShotIndicator()
    {
        // Show where the last shot was aimed for a few seconds
        if (Time.time - lastShotTime < shotIndicatorDuration)
        {
            Color indicatorColor = lastShotWasHit ? hitColor : missColor;
            indicatorColor.a = 1f - ((Time.time - lastShotTime) / shotIndicatorDuration);

            Gizmos.color = indicatorColor;
            Gizmos.DrawWireSphere(lastAimPoint, 0.4f);
            Gizmos.DrawLine(transform.position, lastAimPoint);
        }
    }

    private void DrawDetailedInfo()
    {
        float accuracy = aiController.GetAimAccuracy();
        float maxOffset = GetMaxAimOffset();

        // Draw accuracy percentage text
        Vector3 labelPos = transform.position + Vector3.up * 2f;

        #if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(labelPos,
            $"Accuracy: {(accuracy * 100f):F0}%\n" +
            $"Max Miss Radius: {maxOffset * (1f - accuracy):F1}u");
        #endif

        // Draw grid showing possible aim points
        Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
        for (int i = 0; i < 8; i++)
        {
            float angle = (i / 8f) * 360f * Mathf.Deg2Rad;
            float radius = maxOffset * (1f - accuracy);
            Vector3 point = target.position + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * 0.5f,
                0f
            );
            Gizmos.DrawLine(target.position, point);
            Gizmos.DrawWireSphere(point, 0.2f);
        }
    }

    private void DrawTrajectoryPreview()
    {
        // This is a simplified trajectory preview
        // For accurate trajectory, you'd need to simulate the actual projectile physics

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);

        Vector3 start = transform.position;
        Vector3 end = target.position;
        Vector3 lastPoint = start;

        for (int i = 1; i <= trajectoryPointCount; i++)
        {
            float t = i / (float)trajectoryPointCount;

            // Simple parabolic arc
            Vector3 point = Vector3.Lerp(start, end, t);
            point.y += Mathf.Sin(t * Mathf.PI) * 2f; // Arc height

            Gizmos.DrawLine(lastPoint, point);
            Gizmos.DrawWireSphere(point, 0.1f);

            lastPoint = point;
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    private float GetMaxAimOffset()
    {
        if (aiController == null)
        {
            return 3f;
        }

        return aiController.GetCurrentSettings().maxMissDistance;
    }

    // Public method to record a shot for visualization
    public void RecordShot(Vector3 aimPoint, bool wasAccurate)
    {
        lastAimPoint = aimPoint;
        lastShotWasHit = wasAccurate;
        lastShotTime = Time.time;
    }

    // Toggle visualization at runtime
    public void ToggleVisualization(bool enabled)
    {
        this.enabled = enabled;
    }

    // Public setters for runtime control
    public void SetShowAimLine(bool show) => showAimLine = show;
    public void SetShowAccuracyCircle(bool show) => showAccuracyCircle = show;
    public void SetShowTrajectory(bool show) => showPredictedTrajectory = show;
}
