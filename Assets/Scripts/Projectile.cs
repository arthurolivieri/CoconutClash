using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private Health.Team defaultTeam = Health.Team.Neutral;

    // ===== MANUAL / PHYSICS MODE =====
    private bool manualPhysics = false;    // if true, use rb physics only
    private float spinSpeed = 0f;          // cosmetic spin (deg/s)

    // ===== AI / CURVE MODE =====
    private bool aiMode = false;
    private Transform target;
    private float maxMoveSpeed = 6f;
    private float moveSpeed = 0f;
    private float trajectoryMaxRelativeHeight = 1f;
    private float projectileRotationSpeed = 180f;

    private AnimationCurve trajectoryAnimationCurve;
    private AnimationCurve axisCorrectionAnimationCurve;
    private AnimationCurve projectileSpeedAnimationCurve;

    private Vector3 trajectoryStartPoint;

    // ===== Lifetime / cleanup =====
    [Header("General")]
    [SerializeField] private float distanceToTargetToDestroyProjectile = 0.2f; // AI only
    [SerializeField] private float lifetimeSeconds = 10f;
    private float lifeTimer = 0f;

    [Header("Collision Settings")]
    [SerializeField] private bool destroyOnGroundHit = true;
    [SerializeField] private bool destroyOnOpponentHit = true;
    [SerializeField] private string groundTag = "Ground";
    
    // Event fired when projectile is destroyed
    public event System.Action OnProjectileDestroyed;

    // ===== Cached components =====
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;

    private bool guardLogged;
    private bool isDestroying = false;
    private float activeDamage;
    private Health.Team activeTeam;

    private void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr  = GetComponent<SpriteRenderer>();

        // Make sure we have the pieces needed for physics collisions
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (col == null)
        {
            var cc = gameObject.AddComponent<CircleCollider2D>();
            cc.radius = 0.15f;
            cc.isTrigger = false;
            col = cc;
        }
        else
        {
            col.isTrigger = false; // we want solid collisions with walls
        }

        // Do not tick Update until initialized
        enabled = false;

        activeDamage = Mathf.Max(0f, baseDamage);
        activeTeam = defaultTeam;
    }

    // =========================
    //    PUBLIC: MANUAL MODE
    // =========================
    /// <summary>
    /// Physics projectile: uses Rigidbody2D so it collides with walls (no curves/target).
    /// </summary>
    public void Initialize(Vector2 initialVelocity, float gravityStrength, float rotationSpeed)
    {
        manualPhysics = true;
        aiMode = false;

        spinSpeed = rotationSpeed;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = gravityStrength / 9.81f;  // convert to Unity gravity units
        rb.linearVelocity = initialVelocity;
        
        // Prevent rolling - freeze rotation so projectile only bounces
        rb.freezeRotation = true;
        rb.angularVelocity = 0f;

        enabled = true;  // we use Update only for cosmetic spin + lifetime
    }

    // =========================
    //       PUBLIC: AI MODE
    // =========================
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

        manualPhysics = false;
        aiMode = true;

        this.target = target;
        this.maxMoveSpeed = maxMoveSpeed;
        this.projectileRotationSpeed = projectileRotationSpeed;

        float xDist = target.position.x - transform.position.x;
        trajectoryMaxRelativeHeight = Mathf.Abs(xDist) * Mathf.Max(0.0f, trajectoryMaxHeight);

        trajectoryStartPoint = transform.position;

        // In AI mode, we kinematically place the projectile along the curve
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Do not enable yet — we enable when curves are set (or defaults applied)
    }

    public void InitializeAnimationCurves(AnimationCurve traj,
                                          AnimationCurve axis,
                                          AnimationCurve speed)
    {
        // Safe defaults so you don't need to touch the inspector
        trajectoryAnimationCurve      = traj  ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        axisCorrectionAnimationCurve  = axis  ?? new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
        projectileSpeedAnimationCurve = speed ?? new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));

        // Now we can run AI update
        enabled = true;
    }

    // =========================
    //          UPDATE
    // =========================
    private void Update()
    {
        // Lifetime
        lifeTimer += Time.deltaTime;
        if (lifetimeSeconds > 0f && lifeTimer >= lifetimeSeconds)
        {
            DestroyProjectile();
            return;
        }

        if (manualPhysics)
        {
            // Prevent rolling - continuously enforce frozen rotation
            if (rb != null && rb.angularVelocity != 0f)
            {
                rb.angularVelocity = 0f;
            }
            
            // cosmetic spin only — movement is handled by Rigidbody2D
            if (spinSpeed != 0f)
                transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime);
            return;
        }

        if (aiMode)
        {
            if (target == null || trajectoryAnimationCurve == null ||
                axisCorrectionAnimationCurve == null || projectileSpeedAnimationCurve == null)
            {
                if (!guardLogged)
                {
                    Debug.LogWarning("[Projectile] AI Update skipped — missing target or curves.");
                    guardLogged = true;
                }
                return;
            }

            Vector3 toTarget = target.position - trajectoryStartPoint;
            float dx = toTarget.x;
            if (Mathf.Abs(dx) < 1e-4f) dx = Mathf.Sign(dx) * 1e-4f;

            // progress along X (0..1)
            float tNow = Mathf.InverseLerp(trajectoryStartPoint.x, target.position.x, transform.position.x);
            float speed01 = Mathf.Clamp01(projectileSpeedAnimationCurve.Evaluate(tNow));
            moveSpeed = Mathf.Sign(dx) * Mathf.Max(0.001f, speed01) * maxMoveSpeed;

            // next X
            float nextX = transform.position.x + moveSpeed * Time.deltaTime;
            float tX = Mathf.InverseLerp(trajectoryStartPoint.x, target.position.x, nextX);
            tX = Mathf.Clamp01(tX);

            // Y from curves
            float baseYNorm = Mathf.Clamp01(trajectoryAnimationCurve.Evaluate(tX));
            float y = trajectoryStartPoint.y + baseYNorm * trajectoryMaxRelativeHeight;

            float axisCorrNorm = axisCorrectionAnimationCurve.Evaluate(tX);
            y += axisCorrNorm * trajectoryMaxRelativeHeight;

            Vector3 nextPos = new Vector3(nextX, y, 0f);

            // Move kinematically
            if (rb.bodyType == RigidbodyType2D.Kinematic) rb.MovePosition(nextPos);
            else transform.position = nextPos;

            // Rotate toward movement
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

            // Destroy near target (AI only)
            if (Vector3.Distance(transform.position, target.position) < distanceToTargetToDestroyProjectile)
            {
                DestroyProjectile();
            }
        }
    }

    // =========================
    //    COLLISION HANDLING
    // =========================
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDestroying) return;

        bool damagedOpponent = TryApplyDamage(collision.collider);

        if (damagedOpponent && destroyOnOpponentHit)
        {
            Debug.Log($"[Projectile] Hit opponent: {collision.gameObject.name}");
            DestroyProjectile();
            return;
        }

        // Check if hit ground - destroy immediately
        if (collision.gameObject.CompareTag(groundTag) && destroyOnGroundHit)
        {
            Debug.Log($"[Projectile] Hit ground - destroying");
            DestroyProjectile();
            return;
        }
    }

    private bool TryApplyDamage(Collider2D targetCollider)
    {
        if (activeDamage <= 0f || targetCollider == null) return false;

        Health targetHealth = targetCollider.GetComponentInParent<Health>();
        if (targetHealth == null) return false;

        float before = targetHealth.CurrentHealth;
        targetHealth.TakeDamage(activeDamage, activeTeam, gameObject);
        return before > targetHealth.CurrentHealth;
    }

    private void DestroyProjectile()
    {
        if (isDestroying) return;
        isDestroying = true;
        
        OnProjectileDestroyed?.Invoke();
        Destroy(gameObject);
    }
}
