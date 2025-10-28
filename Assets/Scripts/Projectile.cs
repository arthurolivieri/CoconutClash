using UnityEngine;
using System.Collections.Generic;

public class Projectile : MonoBehaviour
{
    private Transform target;
    private float moveSpeed;
    private float maxMoveSpeed;
    private float trajectoryMaxRelativeHeight;
    private float projectileRotationSpeed;

    private float distanceToTargetToDestroyProjectile = 1f;

    private AnimationCurve trajectoryAnimationCurve;
    private AnimationCurve axisCorrectionAnimationCurve;
    private AnimationCurve projectileSpeedAnimationCurve;

    private Vector3 trajectoryStartPoint;
    private bool hasLoggedInitialization = false;
    private bool hasLoggedSkippedUpdate = false;

    // Physics System Variables
    [Header("Physics Settings")]
    [SerializeField] private bool usePhysics = true;
    [SerializeField] private bool useBounce = true;
    [SerializeField] private bool useAttractionRepulsion = true;
    [SerializeField] private float physicsUpdateRate = 60f; // Updates per second
    [SerializeField] private float gravityScale = 1f; // <--- nova: gravidade aplicada após bounce

    private Vector2 velocity;
    private Vector2 previousPosition;
    private float physicsUpdateInterval;
    private bool hasBounced = false; // Track if projectile has bounced (switches to physics-only mode)
    private float lastBounceTime = -1f; // Prevent rapid-fire bounces
    private const float BOUNCE_COOLDOWN = 0.1f; // Minimum time between bounces

    // Cache for physics sources to avoid repeated FindObjectOfType calls
    private static AttractionRepulsionSource[] cachedPhysicsSources;
    private static float lastCacheUpdateTime = -1f;
    private static float cacheUpdateInterval = 0.5f; // Update cache every 0.5 seconds

    private Rigidbody2D rb; // <--- nova: cache do Rigidbody2D

    private void Awake() {
        // Disable Update until initialization is complete
        enabled = false;
        
        // Setup physics
        physicsUpdateInterval = 1f / physicsUpdateRate;
        previousPosition = transform.position;
        velocity = Vector2.zero;
        
        // Ensure we have a collider for bounce detection
        if (useBounce)
        {
            // Add Collider2D if none exists
            Collider2D existingCollider = GetComponent<Collider2D>();
            if (existingCollider == null)
            {
                CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
                col.isTrigger = false;
                col.radius = 0.1f;
                Debug.Log($"[Projectile COLLISION] Added CircleCollider2D - Radius: {col.radius}, IsTrigger: {col.isTrigger}");
            }
            else
            {
                existingCollider.isTrigger = false;
                Debug.Log($"[Projectile COLLISION] Using existing Collider2D - Type: {existingCollider.GetType().Name}, IsTrigger: {existingCollider.isTrigger}");
            }
            
            // CRITICAL: Add Rigidbody2D for collision detection to work
            rb = GetComponent<Rigidbody2D>(); // <-- cache aqui
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f; // manter zero até bounce
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
                Debug.Log($"[Projectile COLLISION] Added Rigidbody2D - BodyType: {rb.bodyType}, CollisionMode: {rb.collisionDetectionMode}");
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f; // manter zero até bounce
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
                Debug.Log($"[Projectile COLLISION] Configured Rigidbody2D - BodyType: {rb.bodyType}, CollisionMode: {rb.collisionDetectionMode}");
            }
            
            // Verify components
            Collider2D finalCollider = GetComponent<Collider2D>();
            Rigidbody2D finalRb = rb;
            Debug.Log($"[Projectile COLLISION] Setup Complete - Collider: {(finalCollider != null ? finalCollider.GetType().Name : "NULL")}, " +
                     $"Rigidbody2D: {(finalRb != null ? "EXISTS" : "NULL")}, " +
                     $"UseBounce: {useBounce}, " +
                     $"Layer: {gameObject.layer}");
        }
    }

    private void Start() {
        trajectoryStartPoint = transform.position;
        previousPosition = transform.position;
        
        // Verify collision setup one more time
        if (useBounce)
        {
            Collider2D col = GetComponent<Collider2D>();
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            Debug.Log($"[Projectile COLLISION] Start() - Collider: {(col != null ? $"{col.GetType().Name}, IsTrigger={col.isTrigger}" : "NULL")}, " +
                     $"Rigidbody2D: {(rb != null ? $"EXISTS, BodyType={rb.bodyType}" : "NULL")}");
        }
    }

    private void Update() {
        // Safety check (should not be needed since we enable Update only after init, but extra safety)
        if (target == null || trajectoryAnimationCurve == null || 
            axisCorrectionAnimationCurve == null || projectileSpeedAnimationCurve == null) {
            enabled = false; // Disable if somehow Update got enabled without proper init
            if (!hasLoggedSkippedUpdate) {
                Debug.LogWarning($"[Projectile] Update skipped - Not fully initialized yet. " +
                               $"target: {(target != null ? target.name : "NULL")}, " +
                               $"trajectoryCurve: {(trajectoryAnimationCurve != null ? "SET" : "NULL")}, " +
                               $"axisCorrectionCurve: {(axisCorrectionAnimationCurve != null ? "SET" : "NULL")}, " +
                               $"speedCurve: {(projectileSpeedAnimationCurve != null ? "SET" : "NULL")}");
                hasLoggedSkippedUpdate = true;
            }
            return;
        }

        // Log once when projectile starts moving
        if (!hasLoggedInitialization) {
            hasLoggedInitialization = true;
        }

        // Calculate current velocity from trajectory movement (for bounce detection)
        // Only calculate from trajectory if we haven't bounced yet
        if (useBounce && !hasBounced)
        {
            CalculateVelocityFromTrajectory();
        }
        
        // Update physics forces if enabled (modifies velocity)
        // Always apply physics forces, especially after bounce
        if (usePhysics && useAttractionRepulsion)
        {
            ApplyPhysicsForces();
        }
        
        // Apply gravity to custom velocity if in physics mode but Rigidbody isn't dynamic
        if (hasBounced)
        {
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
            {
                // apply gravity manually to velocity (Physics2D.gravity respects project gravity)
                velocity += (Vector2)Physics2D.gravity * gravityScale * Time.deltaTime;
            }
            else
            {
                // if rb is dynamic, keep rb.velocity in sync with our velocity (optional)
                // but prefer to let physics control rb.velocity (we set it at bounce)
            }
        }
        
        // Apply friction/damping to velocity if bounced (optional, for more realistic physics)
        if (hasBounced && velocity.magnitude > 0.01f)
        {
            // Small velocity damping to prevent infinite movement (optional)
            // velocity *= 0.999f; // Uncomment if you want projectiles to slow down over time
        }
        
        UpdateProjectilePosition();
        UpdateProjectileRotation();
        
        // Manual collision check as fallback (checks every few frames for performance)
        if (useBounce && Time.frameCount % 3 == 0) // Every 3 frames
        {
            CheckForManualCollision();
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget < distanceToTargetToDestroyProjectile) {
            Destroy(gameObject);
        }
    }

    private void UpdateProjectilePosition() {
        Vector3 newPosition;
        
        // If bounced, use physics-only movement (ignore trajectory)
        if (hasBounced)
        {
            // If we have a dynamic rigidbody, let physics move the object (do not set transform manually)
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                // keep local velocity in sync with rb (optional)
                velocity = rb.linearVelocity;
                return; // physics will update transform
            }

            // Pure physics movement after bounce (manual integration when no dynamic rb)
            if (velocity.magnitude > 0.01f)
            {
                Vector3 physicsMovement = (Vector3)velocity * Time.deltaTime;
                newPosition = transform.position + physicsMovement;
            }
            else
            {
                // No velocity, stay in place
                newPosition = transform.position;
            }
        }
        else
        {
            // Normal trajectory-based movement
            Vector3 trajectoryRange = target.position - trajectoryStartPoint;

            // Ensure moveSpeed has the correct sign based on direction once
            moveSpeed = Mathf.Sign(trajectoryRange.x) * Mathf.Abs(moveSpeed);

            float nextPositionX = transform.position.x + moveSpeed * Time.deltaTime;
            
            // Check for division by zero
            if (Mathf.Abs(trajectoryRange.x) < 0.001f) {
                Debug.LogError($"[Projectile] trajectoryRange.x is too small ({trajectoryRange.x}), cannot normalize!");
                return;
            }
            
            float nextPositionXNormalized = (nextPositionX - trajectoryStartPoint.x) / trajectoryRange.x;

            float nextPositionYNormalized = trajectoryAnimationCurve.Evaluate(nextPositionXNormalized);
            float nextPositionYCorrectionNormalized = axisCorrectionAnimationCurve.Evaluate(nextPositionXNormalized);
            float nextPositionYCorrectionAbsolute = nextPositionYCorrectionNormalized * trajectoryRange.y;

            float nextPositionY = trajectoryStartPoint.y + nextPositionYNormalized * trajectoryMaxRelativeHeight + nextPositionYCorrectionAbsolute;
        
            newPosition = new Vector3(nextPositionX, nextPositionY, 0);

            CalculateNextProjectileSpeed(nextPositionXNormalized);

            // Apply physics velocity as small adjustment (only before bounce)
            if (usePhysics && useAttractionRepulsion && velocity.magnitude > 0.01f)
            {
                Vector3 physicsOffset = (Vector3)velocity * Time.deltaTime * 0.3f; // Smaller influence before bounce
                newPosition += physicsOffset;
            }
        }

        // Update position - if we have a Rigidbody2D (kinematic), update it there too
        if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
        {
            rb.MovePosition(newPosition);
        }
        else if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            // let physics engine move dynamic rigidbody; do not override transform
            // optionally keep rb.velocity in sync with `velocity` if using manual forces
        }
        else
        {
            transform.position = newPosition;
        }
    }

    /// <summary>
    /// Apply physics forces from AttractionRepulsionSource objects
    /// </summary>
    private void ApplyPhysicsForces()
    {
        if (!useAttractionRepulsion) return;
        
        // Update cache of physics sources periodically
        if (Time.time - lastCacheUpdateTime > cacheUpdateInterval)
        {
            cachedPhysicsSources = FindObjectsByType<AttractionRepulsionSource>(FindObjectsSortMode.None);
            lastCacheUpdateTime = Time.time;
        }
        
        if (cachedPhysicsSources == null || cachedPhysicsSources.Length == 0) return;
        
        Vector2 totalForce = Vector2.zero;
        
        foreach (AttractionRepulsionSource source in cachedPhysicsSources)
        {
            if (source == null || !source.IsPositionInRange(transform.position)) continue;
            
            // Check layer mask
            LayerMask affectedLayers = source.GetAffectedLayers();
            if (((1 << gameObject.layer) & affectedLayers.value) == 0) continue;
            
            Vector2 force = source.CalculateForceAtPosition(transform.position);
            totalForce += force;
        }
        
        // Apply force to velocity
        if (totalForce.magnitude > 0.01f)
        {
            velocity += totalForce * Time.deltaTime;
            
            // Clamp velocity to prevent infinite acceleration
            float maxVelocity = maxMoveSpeed * 2f; // Allow up to 2x max speed from physics
            if (velocity.magnitude > maxVelocity)
            {
                velocity = velocity.normalized * maxVelocity;
            }
        }
    }

    /// <summary>
    /// Calculate velocity from trajectory-based movement (for bounce detection)
    /// </summary>
    private void CalculateVelocityFromTrajectory()
    {
        // Calculate velocity based on trajectory movement direction
        Vector3 trajectoryRange = target.position - trajectoryStartPoint;
        
        if (Mathf.Abs(trajectoryRange.x) > 0.001f)
        {
            // Calculate direction from trajectory
            float normalizedX = (transform.position.x - trajectoryStartPoint.x) / trajectoryRange.x;
            
            // Get Y change from trajectory curves
            float currentYFromCurve = trajectoryAnimationCurve.Evaluate(normalizedX);
            float nextYFromCurve = trajectoryAnimationCurve.Evaluate(normalizedX + 0.01f);
            
            // Calculate trajectory direction
            Vector2 trajectoryDirection = new Vector2(
                Mathf.Sign(moveSpeed), // X direction from move speed
                (nextYFromCurve - currentYFromCurve) * trajectoryMaxRelativeHeight / Mathf.Abs(trajectoryRange.x)
            ).normalized;
            
            // Set velocity based on trajectory movement
            velocity = trajectoryDirection * Mathf.Abs(moveSpeed);
        }
        else
        {
            // Fallback: calculate from position delta
            Vector2 currentPosition = transform.position;
            if (Time.deltaTime > 0)
            {
                velocity = (currentPosition - previousPosition) / Time.deltaTime;
            }
            previousPosition = currentPosition;
        }
    }

    private void UpdateProjectileRotation() {
        transform.Rotate(Vector3.forward, projectileRotationSpeed * Time.deltaTime);
    }

    private void CalculateNextProjectileSpeed(float nextPositionXNormalized) {
        float nextMoveSpeedNormalized = projectileSpeedAnimationCurve.Evaluate(nextPositionXNormalized);
        moveSpeed = nextMoveSpeedNormalized * maxMoveSpeed;
    }

    public void InitializeProjectile(Transform target,
                                     float maxMoveSpeed,
                                     float trajectoryMaxHeight,
                                     float projectileRotationSpeed) {
        if (target == null) {
            Debug.LogError("[Projectile] InitializeProjectile: target is NULL!");
            return;
        }
        
        this.target = target;
        this.maxMoveSpeed = maxMoveSpeed;
        float xDistanceToTarget = target.position.x - transform.position.x;
        this.trajectoryMaxRelativeHeight = Mathf.Abs(xDistanceToTarget) * trajectoryMaxHeight;
        this.projectileRotationSpeed = projectileRotationSpeed;
        this.moveSpeed = maxMoveSpeed;
    }

    public void InitializeAnimationCurves(AnimationCurve trajectoryAnimationCurve,
                                          AnimationCurve axisCorrectionAnimationCurve,
                                          AnimationCurve projectileSpeedAnimationCurve) {
        if (trajectoryAnimationCurve == null) {
            Debug.LogError("[Projectile] InitializeAnimationCurves: trajectoryAnimationCurve is NULL!");
        }
        if (axisCorrectionAnimationCurve == null) {
            Debug.LogError("[Projectile] InitializeAnimationCurves: axisCorrectionAnimationCurve is NULL!");
        }
        if (projectileSpeedAnimationCurve == null) {
            Debug.LogError("[Projectile] InitializeAnimationCurves: projectileSpeedAnimationCurve is NULL!");
        }
        
        this.trajectoryAnimationCurve = trajectoryAnimationCurve;
        this.axisCorrectionAnimationCurve = axisCorrectionAnimationCurve;
        this.projectileSpeedAnimationCurve = projectileSpeedAnimationCurve;
        
        // Enable Update now that everything is initialized
        if (target != null && trajectoryAnimationCurve != null && 
            axisCorrectionAnimationCurve != null && projectileSpeedAnimationCurve != null) {
            enabled = true;
        } else {
            Debug.LogError("[Projectile] Cannot enable Update - missing required values!");
        }
    }

    /// <summary>
    /// Handle collision with bounceable surfaces
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Prevent rapid-fire bounces
        if (Time.time - lastBounceTime < BOUNCE_COOLDOWN)
        {
            return;
        }
        
        if (!useBounce)
        {
            return;
        }
        
        BounceableSurface bounceSurface = collision.gameObject.GetComponent<BounceableSurface>();
        if (bounceSurface == null)
        {
            return;
        }
        
        Debug.Log($"[Projectile COLLISION] ✓ Bounce! Object: {collision.gameObject.name}, Velocity before: {velocity}");
        
        // Get collision normal from contact point
        if (collision.contactCount > 0)
        {
            ContactPoint2D contact = collision.contacts[0];
            Vector2 collisionNormal = contact.normal;
            Vector2 collisionPoint = contact.point;
            
            // Calculate bounced velocity
            Vector2 bouncedVelocity = bounceSurface.CalculateBounce(velocity, collisionPoint, collisionNormal);
            
            // Switch to physics-only mode and set bounced velocity
            hasBounced = true;
            velocity = bouncedVelocity;
            lastBounceTime = Time.time;
            
            // If we have a Rigidbody2D, enable dynamic physics so gravity/physics run naturally
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = gravityScale;
                rb.linearVelocity = velocity;
            }
            
            // Move projectile away from surface to prevent immediate re-collision
            Vector2 correctionOffset = collisionNormal * 0.15f;
            Vector2 newPos = (Vector2)transform.position + correctionOffset;
            
            // Update position immediately (only if not dynamic)
            if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
            {
                rb.MovePosition(newPos);
            }
            else if (rb == null)
            {
                transform.position = newPos;
            }

            // Apply force to the target if it has a Rigidbody2D
            Rigidbody2D targetRb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (targetRb != null)
            {
                // Calculate the force to apply to the target
                Vector2 forceDirection = collisionNormal * -1; // Push back in the opposite direction of the collision
                float forceMagnitude = 5f; // Adjust this value as needed
                targetRb.AddForce(forceDirection * forceMagnitude, ForceMode2D.Impulse);
            }
            
            Debug.Log($"[Projectile COLLISION] ✓ BOUNCED! Switched to physics mode. Velocity: {velocity}, Normal: {collisionNormal}");
        }
    }
    
    /// <summary>
    /// Handle staying in collision (for continuous collision)
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!useBounce) return;
        
        BounceableSurface bounceSurface = collision.gameObject.GetComponent<BounceableSurface>();
        if (bounceSurface == null) return;
        
        // Push away from surface if stuck
        if (collision.contactCount > 0)
        {
            ContactPoint2D contact = collision.contacts[0];
            Vector2 correctionOffset = contact.normal * 0.05f;
            transform.position = (Vector2)transform.position + correctionOffset;
            
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.MovePosition(transform.position);
            }
        }
    }
    
    /// <summary>
    /// Manual collision detection as fallback (using overlap circle)
    /// </summary>
    private void CheckForManualCollision()
    {
        Collider2D projectileCollider = GetComponent<Collider2D>();
        if (projectileCollider == null) return;
        
        // Get all colliders overlapping with projectile
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter(); // Check all layers
        filter.useTriggers = false; // Don't check triggers
        
        List<Collider2D> results = new List<Collider2D>();
        int overlapCount = 0;
        
        if (projectileCollider is CircleCollider2D circleCollider)
        {
            overlapCount = Physics2D.OverlapCircle(transform.position, circleCollider.radius, filter, results);
        }
        else
        {
            overlapCount = projectileCollider.Overlap(filter, results);
        }
        
        if (overlapCount > 0)
        {
            foreach (Collider2D hitCollider in results)
            {
                // Don't collide with self
                if (hitCollider.gameObject == gameObject) continue;
                
                BounceableSurface bounceSurface = hitCollider.GetComponent<BounceableSurface>();
                if (bounceSurface != null)
                {
                    // Prevent rapid-fire bounces
                    if (Time.time - lastBounceTime < BOUNCE_COOLDOWN)
                    {
                        continue;
                    }
                    
                    Debug.Log($"[Projectile COLLISION] Manual check detected overlap with {hitCollider.gameObject.name}!");
                    
                    // Calculate collision normal
                    Vector2 directionToWall = (Vector2)hitCollider.bounds.center - (Vector2)transform.position;
                    if (directionToWall.magnitude > 0.01f)
                    {
                        // Get closest point on wall
                        Vector2 closestPoint = hitCollider.ClosestPoint(transform.position);
                        Vector2 normal = ((Vector2)transform.position - closestPoint).normalized;
                        
                        if (normal.magnitude < 0.01f)
                        {
                            normal = -directionToWall.normalized;
                        }
                        
                        // Switch to physics mode and apply bounce
                        hasBounced = true;
                        Vector2 bouncedVelocity = bounceSurface.CalculateBounce(velocity, closestPoint, normal);
                        velocity = bouncedVelocity;
                        lastBounceTime = Time.time;
                        
                        // If we have a Rigidbody2D, enable dynamic physics so gravity/physics run naturalmente
                        if (rb != null)
                        {
                            rb.bodyType = RigidbodyType2D.Dynamic;
                            rb.gravityScale = gravityScale;
                            rb.linearVelocity = velocity;
                        }
                        
                        // Push away
                        Vector2 correctionOffset = normal * 0.2f;
                        Vector2 newPos = (Vector2)transform.position + correctionOffset;
                        
                        if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
                        {
                            rb.MovePosition(newPos);
                        }
                        else if (rb == null)
                        {
                            transform.position = newPos;
                        }
                        
                        Debug.Log($"[Projectile COLLISION] Manual bounce applied! Switched to physics mode. Velocity: {velocity}");
                        break; // Only process one bounce per frame
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handle trigger collision (for non-physical interactions)
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // You can add trigger-based interactions here if needed
    }

    /// <summary>
    /// Get current velocity (useful for external scripts)
    /// </summary>
    public Vector2 GetVelocity()
    {
        return velocity;
    }

    /// <summary>
    /// Set velocity directly (useful for external scripts)
    /// </summary>
    public void SetVelocity(Vector2 newVelocity)
    {
        velocity = newVelocity;
    }

    /// <summary>
    /// Add force to projectile (useful for external scripts)
    /// </summary>
    public void AddForce(Vector2 force)
    {
        velocity += force * Time.deltaTime;
    }
    
}
