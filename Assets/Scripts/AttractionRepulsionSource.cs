using UnityEngine;

/// <summary>
/// Creates a point-based attraction or repulsion force that affects nearby objects.
/// Can be used for magnets, gravity wells, force fields, etc.
/// </summary>
public class AttractionRepulsionSource : MonoBehaviour
{
    public enum ForceType
    {
        Attraction,
        Repulsion
    }

    [Header("Force Settings")]
    [SerializeField] private ForceType forceType = ForceType.Attraction;
    [SerializeField] private float forceStrength = 10f;
    [SerializeField] private float forceRadius = 5f;
    [SerializeField] private AnimationCurve forceFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    
    [Header("Layer Settings")]
    [SerializeField] private LayerMask affectedLayers = -1; // All layers by default
    
    [Header("Advanced")]
    [SerializeField] private bool useConstantForce = false; // If false, uses inverse square law
    [SerializeField] private float constantForceMultiplier = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color attractionColor = Color.blue;
    [SerializeField] private Color repulsionColor = Color.red;

    /// <summary>
    /// Calculate the force vector at a given position.
    /// Returns the force that should be applied to an object at that position.
    /// </summary>
    public Vector2 CalculateForceAtPosition(Vector2 position)
    {
        Vector2 sourcePosition = transform.position;
        Vector2 direction = position - sourcePosition;
        float distance = direction.magnitude;
        
        // If outside radius, no force
        if (distance > forceRadius || distance < 0.01f)
        {
            return Vector2.zero;
        }
        
        // Normalize direction
        direction.Normalize();
        
        // Calculate force strength based on distance
        float normalizedDistance = distance / forceRadius;
        float curveValue = forceFalloffCurve.Evaluate(normalizedDistance);
        
        float finalForce;
        if (useConstantForce)
        {
            // Constant force based on curve
            finalForce = forceStrength * curveValue * constantForceMultiplier;
        }
        else
        {
            // Inverse square law (like gravity/electric fields)
            float inverseSquareFactor = 1f / (distance * distance + 0.1f); // Add small value to prevent division by zero
            finalForce = forceStrength * curveValue * inverseSquareFactor;
        }
        
        // Determine direction based on force type
        if (forceType == ForceType.Repulsion)
        {
            // Push away from source
            return direction * finalForce;
        }
        else
        {
            // Pull toward source
            return -direction * finalForce;
        }
    }

    /// <summary>
    /// Check if a position is within the force radius
    /// </summary>
    public bool IsPositionInRange(Vector2 position)
    {
        float distance = Vector2.Distance(transform.position, position);
        return distance <= forceRadius;
    }

    /// <summary>
    /// Get the distance from this source to a position
    /// </summary>
    public float GetDistanceToPosition(Vector2 position)
    {
        return Vector2.Distance(transform.position, position);
    }

    public float GetForceRadius()
    {
        return forceRadius;
    }

    public ForceType GetForceType()
    {
        return forceType;
    }

    public LayerMask GetAffectedLayers()
    {
        return affectedLayers;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Draw force radius
        Color gizmoColor = forceType == ForceType.Attraction ? attractionColor : repulsionColor;
        gizmoColor.a = 0.3f;
        
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, forceRadius);
        
        // Draw force direction indicator
        gizmoColor.a = 1f;
        Gizmos.color = gizmoColor;
        
        if (forceType == ForceType.Attraction)
        {
            // Draw arrows pointing inward
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 start = (Vector2)transform.position + direction * forceRadius;
                Vector2 end = (Vector2)transform.position + direction * (forceRadius - 0.5f);
                Gizmos.DrawLine(start, end);
            }
        }
        else
        {
            // Draw arrows pointing outward
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 start = (Vector2)transform.position + direction * (forceRadius - 0.5f);
                Vector2 end = (Vector2)transform.position + direction * forceRadius;
                Gizmos.DrawLine(start, end);
            }
        }
    }
}

