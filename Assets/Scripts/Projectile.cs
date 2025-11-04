using UnityEngine;

public class Projectile : MonoBehaviour
{
    // ===== Manual / Free-Flight (simple parabola) =====
    private bool isManual = false;
    private Vector2 velocity;               // current (vx, vy)
    private float gravityStrength = 9f;     // downward accel for manual mode
    private float rotationSpeed = 0f;       // cosmetic spin for manual mode

    // ===== AI / Curve-Driven =====
    private Transform target;                       // aim target
    private AnimationCurve trajectoryAnimationCurve;     // main Y arc (0..1 over X progress)
    private AnimationCurve axisCorrectionAnimationCurve; // extra Y correction
    private AnimationCurve projectileSpeedAnimationCurve;// normalized speed over X progress

    private Vector3 trajectoryStartPoint;
    private float maxMoveSpeed = 6f;
    private float moveSpeed = 0f;
    private float trajectoryMaxRelativeHeight = 1f;
    private float projectileRotationSpeed = 180f;

    // ===== Housekeeping =====
    [Header("General")]
    [SerializeField] private float distanceToTargetToDestroyProjectile = 0.2f; // only in AI/curve mode
    [SerializeField] private float lifetimeSeconds = 10f;
    private float lifeTimer = 0f;

    private bool guardLogged = false;
    private bool aiStartedLogged = false;

    private void Awake()
    {
        // We enable only after being initialized by the shooter (manual or AI).
        enabled = false;
    }

    // =========================
    //  PUBLIC API (MANUAL MODE)
    // =========================

    /// <summary>
    /// Manual / free-flight initializer (simple parabola).
    /// </summary>
    public void Initialize(Vector2 initialVelocity, float gravityStrength, float rotationSpeed)
    {
        isManual = true;
        this.velocity = initialVelocity;
        this.gravityStrength = gravityStrength;
        this.rotationSpeed = rotationSpeed;

        // Manual mode drives from Update() immediately
        enabled = true;
    }

    // =======================
    //  PUBLIC API (AI MODE)
    // =======================

    /// <summary>
    /// Called by EnemyShooterAdvancedAI (or AI Shooter) to set target and core params.
    /// Will be enabled after curves are set (InitializeAnimationCurves).
    /// </summary>
    public void InitializeProjectile(Transform target,
                                     float maxMoveSpeed,
                                     float trajectoryMaxHeight,
                                     float projectileRotationSpeed)
    {
        if (target == null)
        {
            Debug.LogError("[Projectile] InitializeProjectile: target is NULL!");
            return;
        }

        this.target = target;
        this.maxMoveSpeed = maxMoveSpeed;
        this.projectileRotationSpeed = projectileRotationSpeed;

        // Height scales with horizontal distance (same logic you had)
        float xDistanceToTarget = target.position.x - transform.position.x;
        this.trajectoryMaxRelativeHeight = Mathf.Abs(xDistanceToTarget) * trajectoryMaxHeight;

        trajectoryStartPoint = transform.position;
        // Don't enable yet — we’ll enable in InitializeAnimationCurves when curves are present.
    }

    /// <summary>
    /// Curves for AI mode. Safe defaults are applied if any curve is null,
    /// so you don't need to touch the Inspector if you don't want to.
    /// </summary>
    public void InitializeAnimationCurves(AnimationCurve traj,
                                          AnimationCurve axis,
                                          AnimationCurve speed)
    {
        // Safe defaults so the AI path still works even if curves aren't set in the Inspector.
        trajectoryAnimationCurve      = traj  ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        axisCorrectionAnimationCurve  = axis  ?? new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
        projectileSpeedAnimationCurve = speed ?? new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));

        if (target != null)
        {
            enabled = true; // Now AI update can run
        }
        else
        {
            Debug.LogWarning("[Projectile] Curves set but target is missing; not enabling.");
        }
    }

    // =======================
    //  UPDATE (BOTH MODES)
    // =======================

    private void Update()
    {
        // Lifetime cap (safety)
        lifeTimer += Time.deltaTime;
        if (lifetimeSeconds > 0f && lifeTimer >= lifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (isManual)
        {
            // --- FREE-FLIGHT / PARABOLA ---
            float dt = Time.deltaTime;

            // gravity
            velocity += new Vector2(0f, -gravityStrength) * dt;

            // integrate
            Vector3 newPos = transform.position + (Vector3)(velocity * dt);
            newPos.z = 0f; // stay in 2D plane
            transform.position = newPos;

            // cosmetic spin
            if (rotationSpeed != 0f)
                transform.Rotate(Vector3.forward, rotationSpeed * dt);

            // no target-based auto-destroy in manual mode
            return;
        }

        // --- AI / CURVE MODE ---
        if (target == null || trajectoryAnimationCurve == null ||
            axisCorrectionAnimationCurve == null || projectileSpeedAnimationCurve == null)
        {
            if (!guardLogged)
            {
                Debug.LogWarning("[Projectile] Update skipped — missing target or curves for AI mode.");
                guardLogged = true;
            }
            return;
        }

        if (!aiStartedLogged)
        {
            aiStartedLogged = true;
            // Debug.Log($"[Projectile] AI mode start from {trajectoryStartPoint} to {target.position}");
        }

        Vector3 toTarget = target.position - trajectoryStartPoint;
        float dx = toTarget.x;
        if (Mathf.Abs(dx) < 0.0001f) dx = Mathf.Sign(dx) * 0.0001f; // avoid division by zero

        // Current progress along X (0..1)
        float tNow = Mathf.InverseLerp(trajectoryStartPoint.x, target.position.x, transform.position.x);
        float speed01 = Mathf.Clamp01(projectileSpeedAnimationCurve.Evaluate(tNow));
        moveSpeed = Mathf.Sign(dx) * Mathf.Max(0.001f, speed01) * maxMoveSpeed;

        // Advance X by moveSpeed
        float nextX = transform.position.x + moveSpeed * Time.deltaTime;
        float tX = Mathf.InverseLerp(trajectoryStartPoint.x, target.position.x, nextX);
        tX = Mathf.Clamp01(tX);

        // Y from curves
        float baseYNorm = Mathf.Clamp01(trajectoryAnimationCurve.Evaluate(tX));
        float y = trajectoryStartPoint.y + baseYNorm * trajectoryMaxRelativeHeight;

        float axisCorrNorm = axisCorrectionAnimationCurve.Evaluate(tX);
        y += axisCorrNorm * trajectoryMaxRelativeHeight;

        Vector3 nextPos = new Vector3(nextX, y, 0f);

        // Move
        transform.position = nextPos;

        // Rotate to movement direction
        Vector2 dir = (Vector2)(nextPos - transform.position);
        if (dir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.AngleAxis(angle, Vector3.forward),
                projectileRotationSpeed * Time.deltaTime
            );
        }

        // Destroy when near the target (AI only)
        if (Vector3.Distance(transform.position, target.position) < distanceToTargetToDestroyProjectile)
        {
            Destroy(gameObject);
        }
    }
}
