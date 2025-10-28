using UnityEngine;

/// <summary>
/// Attach this to GameObjects that projectiles should bounce off of.
/// Handles bounce detection and calculates reflection vectors.
/// </summary>
public class BounceableSurface : MonoBehaviour
{
    [Header("Bounce Settings")]
    [SerializeField] private float bounceMultiplier = 1f;
    [SerializeField] private bool normalizeAfterBounce = true;
    [SerializeField] private float minBounceVelocity = 0.1f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoColor = Color.green;

    private Collider2D surfaceCollider;

    private void Awake()
    {
        // Get the collider attached to this GameObject
        surfaceCollider = GetComponent<Collider2D>();
        
        if (surfaceCollider == null)
        {
            Debug.LogError($"[BounceableSurface] No Collider2D found on {gameObject.name}! A collider is required for bounce detection.");
        }
        else
        {
            // Make sure the collider is a trigger if needed (or use collision detection)
            // For 2D, we'll use OnTriggerEnter2D or OnCollisionEnter2D
        }
    }

    /// <summary>
    /// Called when a projectile collides with this surface.
    /// Returns the reflected velocity vector.
    /// </summary>
    public Vector2 CalculateBounce(Vector2 incomingVelocity, Vector2 collisionPoint, Vector2 collisionNormal)
    {
        // Reflect the velocity vector off the normal
        Vector2 reflectedVelocity = Vector2.Reflect(incomingVelocity, collisionNormal.normalized);
        
        // Apply bounce multiplier
        reflectedVelocity *= bounceMultiplier;
        
        // Normalize and scale if needed
        if (normalizeAfterBounce && reflectedVelocity.magnitude > 0)
        {
            float originalMagnitude = incomingVelocity.magnitude;
            reflectedVelocity = reflectedVelocity.normalized * originalMagnitude * bounceMultiplier;
        }
        
        // Prevent tiny bounces
        if (reflectedVelocity.magnitude < minBounceVelocity)
        {
            reflectedVelocity = reflectedVelocity.normalized * minBounceVelocity;
        }
        
        Debug.Log($"[BounceableSurface] Bounce calculated - Incoming: {incomingVelocity}, Reflected: {reflectedVelocity}, Normal: {collisionNormal}");
        
        return reflectedVelocity;
    }

    /// <summary>
    /// Get the surface normal at a given point (for 2D, we'll calculate from collision)
    /// </summary>
    public Vector2 GetSurfaceNormal(Vector2 point)
    {
        if (surfaceCollider == null) return Vector2.up;
        
        // Try to get the closest point on the collider boundary
        Vector2 closestPoint = surfaceCollider.ClosestPoint(point);
        Vector2 direction = (point - closestPoint).normalized;
        
        // For a more accurate normal, we'd need collision info
        // For now, return a simple upward normal if points are too close
        if (direction.magnitude < 0.01f)
        {
            // If points are too close, use a default normal
            return Vector2.up;
        }
        
        return direction;
    }

    public Collider2D GetCollider()
    {
        return surfaceCollider;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || surfaceCollider == null) return;
        
        Gizmos.color = gizmoColor;
        
        // Draw the bounds of the collider
        if (surfaceCollider is BoxCollider2D boxCollider)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.offset, boxCollider.size);
        }
        else if (surfaceCollider is CircleCollider2D circleCollider)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(circleCollider.offset, circleCollider.radius);
        }
        else if (surfaceCollider is PolygonCollider2D polyCollider)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < polyCollider.pathCount; i++)
            {
                Vector2[] path = polyCollider.GetPath(i);
                for (int j = 0; j < path.Length; j++)
                {
                    int next = (j + 1) % path.Length;
                    Vector3 start = path[j];
                    Vector3 end = path[next];
                    Gizmos.DrawLine(start, end);
                }
            }
        }
    }
}

