using UnityEngine;

[RequireComponent(typeof(AreaEffector2D))]
public class ForceField : MonoBehaviour
{
    [Header("Force Settings")]
    [SerializeField] private float forceMagnitude = 10f;
    [SerializeField] private float forceVariation = 2f;
    [SerializeField] private float pulseSpeed = 1f;
    [SerializeField, Range(0, 360)] private float forceAngle = 0f; // New: Angle control in inspector
    [SerializeField] private bool useGlobalAngle = true; // New: Option to use global or local angle
    
    private AreaEffector2D areaEffector;
    
    private void Awake()
    {
        areaEffector = GetComponent<AreaEffector2D>();
        
        // Configure force field using Inspector values
        areaEffector.forceAngle = forceAngle;
        areaEffector.useGlobalAngle = useGlobalAngle;
        areaEffector.forceMagnitude = forceMagnitude;
    }

    private void Update()
    {
        // Update force angle if changed in Inspector during runtime
        if (areaEffector.forceAngle != forceAngle)
        {
            areaEffector.forceAngle = forceAngle;
        }

        // Create pulsing effect
        float pulse = Mathf.Sin(Time.time * pulseSpeed);
        areaEffector.forceMagnitude = forceMagnitude + (pulse * forceVariation);
    }

    // Optional: Add this to visualize the force direction in the Scene view
    private void OnDrawGizmos()
    {
        float angle = useGlobalAngle ? forceAngle : forceAngle + transform.eulerAngles.z;
        Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
        
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, direction * 2f);
    }
}