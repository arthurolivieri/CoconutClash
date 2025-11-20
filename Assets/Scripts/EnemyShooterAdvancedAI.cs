using UnityEngine;

public class EnemyShooterAdvancedAI : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform target;
    [Tooltip("Opcional: ponto exato de saída do projétil (ex.: ponta da arma).")]
    [SerializeField] private Transform muzzleTransform;

    [Header("Shooting Rhythm")]
    [SerializeField] private float baseShootRate = 2f;
    [SerializeField] [Range(0f, 1f)] private float shootRateNoiseAmount = 0.25f;

    [Header("Projectile Motion")]
    [SerializeField] private float baseProjectileMaxMoveSpeed = 10f;
    [SerializeField] private float baseProjectileMaxHeight = 0.5f;
    [SerializeField] private float baseProjectileRotationSpeed = 360f;

    [Header("Accuracy")]
    [SerializeField] [Range(0f, 1f)] private float aimAccuracy = 0.5f;
    [SerializeField] private float minAimOffsetDistance = 1f;
    [SerializeField] private float maxAimOffsetDistance = 4f;

    [Header("Height Noise")]
    [SerializeField] private float noiseSpeed = 1f;
    [SerializeField] private float heightNoiseAmount = 0.3f;

    [Header("Animation Curves")]
    [SerializeField] private AnimationCurve trajectoryAnimationCurve;
    [SerializeField] private AnimationCurve axisCorrectionAnimationCurve;
    [SerializeField] private AnimationCurve projectileSpeedAnimationCurve;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    [Header("Physics Control")]
    [SerializeField] private bool usePhysicsMode = false; // liga/desliga disparo físico
    [SerializeField] private float physicsGravityStrength = 9.81f; // gravidade do disparo físico
    
    [Header("Area Effector Handling")]
    [SerializeField] private bool usePhysicsWhenAreaEffectorsDetected = true;
    [SerializeField] private float effectorDetectionWidth = 1.5f;
    [SerializeField] private LayerMask effectorDetectionMask = ~0;
    [SerializeField] private float physicsLaunchSpeed = 0f;

    [Header("Physics Setup")]
    [SerializeField] private int projectileLayer = 11; // EnemyProjectile layer
    
    [Header("Turn Control")]
    [SerializeField] private bool turnBasedControl = false;

    // ===== ANIMAÇÃO =====
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string shootBoolName = "is_shooting";
    [SerializeField] private string shootStateName = "Shoot";
    [Tooltip("Failsafe: se o evento final não disparar, desliga o bool após este tempo (s).")]
    [SerializeField] private float shootClipMaxLength = 0.6f;
    [Tooltip("Se o evento de disparo não vier, dispara automaticamente após este atraso (s).")]
    [SerializeField] private bool fireFailsafeIfNoEvent = true;
    [SerializeField] private float fireFailsafeDelay = 0.2f;
    [Tooltip("Reset automático do bool quando o normalizedTime do estado atinge este valor.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float shootExitNormalizedTime = 0.95f;

    private Coroutine shootFailsafeCoro;
    private Coroutine fireFailsafeCoro;
    private bool shootingCycleActive = false;
    private bool firedThisCycle = false;

    [System.Serializable]
    public struct ShooterSettings
    {
        [Range(0f, 1f)] public float accuracy;
        public float minMissDistance;
        public float maxMissDistance;
        public float shootInterval;
        [Range(0f, 1f)] public float shootIntervalJitter;
        public float projectileSpeed;
        public float projectileArcHeight;
        public float projectileSpin;
        [Range(0f, 1f)] public float heightNoise;

        public static ShooterSettings Lerp(ShooterSettings a, ShooterSettings b, float t)
        {
            t = Mathf.Clamp01(t);
            return new ShooterSettings
            {
                accuracy = Mathf.Lerp(a.accuracy, b.accuracy, t),
                minMissDistance = Mathf.Lerp(a.minMissDistance, b.minMissDistance, t),
                maxMissDistance = Mathf.Lerp(a.maxMissDistance, b.maxMissDistance, t),
                shootInterval = Mathf.Lerp(a.shootInterval, b.shootInterval, t),
                shootIntervalJitter = Mathf.Lerp(a.shootIntervalJitter, b.shootIntervalJitter, t),
                projectileSpeed = Mathf.Lerp(a.projectileSpeed, b.projectileSpeed, t),
                projectileArcHeight = Mathf.Lerp(a.projectileArcHeight, b.projectileArcHeight, t),
                projectileSpin = Mathf.Lerp(a.projectileSpin, b.projectileSpin, t),
                heightNoise = Mathf.Lerp(a.heightNoise, b.heightNoise, t)
            };
        }
    }

    private float shootTimer;
    private float noiseOffset;
    private ShooterSettings currentSettings;
    private AIAimVisualizer aimVisualizer;

    // Event for turn-based system to track projectile
    public event System.Action<Projectile> ProjectileCreated;

    private void Awake()
    {
        CacheSettingsFromFields();
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Start()
    {
        noiseOffset = Random.Range(0f, 100f);
        shootTimer = GetNextShootDelay();
        aimVisualizer = GetComponent<AIAimVisualizer>();
    }

    private void Update()
    {
        if (projectilePrefab == null || target == null) return;

        // Modo por turno: não agenda disparos automáticos
        if (turnBasedControl) return;

        // Só agenda novo tiro se NÃO estiver em um ciclo de tiro ativo
        if (!shootingCycleActive)
        {
            shootTimer -= Time.deltaTime;
            if (shootTimer <= 0f)
            {
                ShootProjectile(); // inicia ciclo de animação
                shootTimer = GetNextShootDelay();
            }
        }

        // Segurança adicional: resetar bool no fim do estado se necessário
        AutoResetShootBoolByState();
    }

    private void AutoResetShootBoolByState()
    {
        if (!animator) return;
        if (!animator.GetBool(shootBoolName)) return;

        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName(shootStateName) && st.normalizedTime >= shootExitNormalizedTime)
        {
            animator.SetBool(shootBoolName, false);
            if (showDebugInfo) Debug.Log($"[AutoReset] is_shooting=false (t={st.normalizedTime:0.00})");
        }
    }

    private float GetNextShootDelay()
    {
        float noiseValue = GetPerlinNoise(0f);
        float normalizedNoise = (noiseValue - 0.5f) * 2f; // [-1, 1]
        float delayModifier = 1f + normalizedNoise * shootRateNoiseAmount;
        float delay = baseShootRate * delayModifier;
        return Mathf.Max(0.1f, delay);
    }

    /// <summary>
    /// Agora inicia apenas o ciclo de animação; o disparo real ocorre em OnShootFire().
    /// </summary>
    private void ShootProjectile()
    {
        if (!animator) return;

        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName(shootStateName)) return; // já está atirando

        shootingCycleActive = true;
        firedThisCycle = false;

        animator.SetBool(shootBoolName, true);

        // failsafe para o bool não ficar preso
        if (shootFailsafeCoro != null) StopCoroutine(shootFailsafeCoro);
        shootFailsafeCoro = StartCoroutine(ResetShootBoolFailsafe(shootClipMaxLength));

        // failsafe do disparo se o evento não vier
        if (fireFailsafeIfNoEvent)
        {
            if (fireFailsafeCoro != null) StopCoroutine(fireFailsafeCoro);
            fireFailsafeCoro = StartCoroutine(FireFailsafeAfter(fireFailsafeDelay));
        }
    }

    private System.Collections.IEnumerator ResetShootBoolFailsafe(float t)
    {
        yield return new WaitForSeconds(t);
        if (animator) animator.SetBool(shootBoolName, false);
        shootFailsafeCoro = null;
        if (showDebugInfo) Debug.Log("[Failsafe] is_shooting resetado por tempo.");
    }

    private System.Collections.IEnumerator FireFailsafeAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (!firedThisCycle)
        {
            if (showDebugInfo) Debug.Log("[Failsafe] Fire() por ausência de Animation Event.");
            DoFireNow(); // dispara mesmo sem evento
        }
        fireFailsafeCoro = null;
    }

    // === Animation Event: frame do disparo ===
    public void OnShootFire()
    {
        if (firedThisCycle) return;
        DoFireNow();
    }

    // === Animation Event: último frame do Shoot ===
    public void OnShootAnimEnd()
    {
        if (animator) animator.SetBool(shootBoolName, false);
        if (shootFailsafeCoro != null) { StopCoroutine(shootFailsafeCoro); shootFailsafeCoro = null; }
        shootingCycleActive = false; // libera para agendar próximo tiro
        if (showDebugInfo) Debug.Log("[AnimEvent] Shoot terminou → Idle");
    }

    /// <summary>
    /// Disparo real (instancia e configura o projétil). Chamada por OnShootFire() ou failsafe.
    /// </summary>
    private void DoFireNow()
    {
        firedThisCycle = true;

        // Recalcula mira no momento do disparo
        Transform aimTarget = CalculateAimTarget(out bool willHit, out Vector3 aimPointPosition);

        // Escolhe modo
        bool usePhysicsShot = usePhysicsMode || ShouldUsePhysicsShot();

        float heightNoise = GetPerlinNoise(100f);
        float heightVariation = 1f + ((heightNoise - 0.5f) * 2f * heightNoiseAmount);
        float projectileMaxHeight = Mathf.Max(0.05f, baseProjectileMaxHeight * heightVariation);

        Vector3 spawnPos = muzzleTransform ? muzzleTransform.position : transform.position;
        Quaternion spawnRot = Quaternion.identity;

        GameObject projectileObj = Instantiate(projectilePrefab, spawnPos, spawnRot);
        if (projectileLayer > 0) projectileObj.layer = projectileLayer;

        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile == null)
        {
            projectile = projectileObj.AddComponent<Projectile>();
        }

        if (usePhysicsShot)
        {
            // Física: calcula velocidade inicial para um arco alto
            float gravityToUse = physicsGravityStrength > 0f ? physicsGravityStrength : 9.81f;

            float horizontalDistance = Mathf.Abs(aimPointPosition.x - spawnPos.x);
            float heightDifference = aimPointPosition.y - spawnPos.y;

            float baseSpeed = physicsLaunchSpeed > 0f ? physicsLaunchSpeed : baseProjectileMaxMoveSpeed;
            float speedVar = Random.Range(0.9f, 1.1f);
            float launchSpeed = baseSpeed * speedVar;

            float launchAngle = CalculateHighArcAngle(horizontalDistance, heightDifference, launchSpeed, gravityToUse);

            if (float.IsNaN(launchAngle) || launchAngle < 45f)
            {
                launchAngle = Random.Range(45f, 60f);
                launchSpeed = CalculateSpeedForAngle(horizontalDistance, heightDifference, launchAngle, gravityToUse);
                launchSpeed = Mathf.Clamp(launchSpeed, baseSpeed * 0.5f, baseSpeed * 2f);
            }

            launchAngle = Mathf.Clamp(launchAngle, 45f, 75f);
            float angleRad = launchAngle * Mathf.Deg2Rad;
            float horizontalSign = (aimPointPosition.x >= spawnPos.x) ? 1f : -1f;

            Vector2 initialVelocity = new Vector2(
                Mathf.Cos(angleRad) * launchSpeed * horizontalSign,
                Mathf.Sin(angleRad) * launchSpeed
            );

            projectile.Initialize(initialVelocity, gravityToUse, baseProjectileRotationSpeed);
        }
        else
        {
            // Modo curva original
            projectile.InitializeProjectile(
                aimTarget,
                baseProjectileMaxMoveSpeed,
                projectileMaxHeight,
                baseProjectileRotationSpeed
            );

            projectile.InitializeAnimationCurves(
                trajectoryAnimationCurve,
                axisCorrectionAnimationCurve,
                projectileSpeedAnimationCurve
            );
        }

        if (aimVisualizer != null)
            aimVisualizer.RecordShot(aimPointPosition, willHit);

        // Notify turn-based system about projectile creation
        ProjectileCreated?.Invoke(projectile);

        if (showDebugInfo)
        {
            string mode = usePhysicsShot ? "Physics" : "Curve";
            string result = willHit ? "✓ HIT" : "✗ MISS";
            float aimOffset = target ? Vector3.Distance(target.position, aimPointPosition) : 0f;
            float loggedSpeed = usePhysicsShot
                ? (physicsLaunchSpeed > 0f ? physicsLaunchSpeed : baseProjectileMaxMoveSpeed)
                : baseProjectileMaxMoveSpeed;
            float loggedHeight = projectileMaxHeight;
            float loggedGravity = usePhysicsShot ? (physicsGravityStrength > 0f ? physicsGravityStrength : 9.81f) : 0f;
            Debug.Log($"[EnemyAI Fire] {result} ({mode}) - v:{loggedSpeed:F2}, h:{loggedHeight:F2}, g:{loggedGravity:F2}, off:{aimOffset:F2}");
        }
    }

    private Transform CalculateAimTarget(out bool willHit, out Vector3 aimPointPosition)
    {
        float clampedAccuracy = Mathf.Clamp01(currentSettings.accuracy);
        float roll = Random.value;
        willHit = roll <= clampedAccuracy;

        if (showDebugInfo)
            Debug.Log($"Accuracy Roll: {roll:F3} | Acc: {clampedAccuracy:F3} | WillHit: {willHit}");

        if (willHit || target == null)
        {
            aimPointPosition = target != null ? target.position : transform.position;
            return target;
        }

        float missSeverity = 1f - clampedAccuracy;
        float minOffset = Mathf.Lerp(currentSettings.minMissDistance * 0.5f, currentSettings.minMissDistance, missSeverity);
        float maxOffset = Mathf.Lerp(currentSettings.minMissDistance, currentSettings.maxMissDistance, missSeverity);
        float missDistance = Random.Range(minOffset, maxOffset);

        Vector2 toTarget2D = new Vector2(target.position.x - transform.position.x,
                                         target.position.y - transform.position.y);

        Vector2 aimDirection = toTarget2D.sqrMagnitude > 0.0001f ? toTarget2D.normalized : Vector2.right;
        Vector2 perpendicular = new Vector2(-aimDirection.y, aimDirection.x);

        if (perpendicular.sqrMagnitude < 0.0001f) perpendicular = Vector2.up;

        perpendicular = perpendicular.normalized * missDistance * (Random.value < 0.5f ? -1f : 1f);
        float verticalNoise = Random.Range(-0.25f, 0.25f) * missDistance;
        Vector2 missOffset2D = perpendicular + new Vector2(0f, verticalNoise);

        Vector3 aimOffset = new Vector3(missOffset2D.x, missOffset2D.y, 0f);

        GameObject aimPoint = new GameObject("AimPoint_Temp");
        aimPoint.transform.position = target.position + aimOffset;
        Destroy(aimPoint, 10f);

        aimPointPosition = aimPoint.transform.position;
        return aimPoint.transform;
    }

    private float GetPerlinNoise(float offset)
    {
        float noiseValue = Mathf.PerlinNoise(
            Time.time * noiseSpeed + noiseOffset + offset,
            noiseOffset
        );
        return noiseValue;
    }

    // --- Balística: helpers ---
    private float CalculateLaunchAngle(float horizontalDistance, float heightDifference, float launchSpeed, float gravity)
    {
        float v2 = launchSpeed * launchSpeed;
        float v4 = v2 * v2;
        float gx = gravity * horizontalDistance;
        float gx2 = gx * horizontalDistance;
        float discriminant = v4 - gravity * (gx2 + 2 * heightDifference * v2);

        if (discriminant < 0) return float.NaN;

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float tanAngle = (v2 - sqrtDiscriminant) / gx; // menor ângulo
        return Mathf.Atan(tanAngle) * Mathf.Rad2Deg;
    }

    private float CalculateHighArcAngle(float horizontalDistance, float heightDifference, float launchSpeed, float gravity)
    {
        if (horizontalDistance < 0.1f) return 45f;

        float v2 = launchSpeed * launchSpeed;
        float v4 = v2 * v2;
        float gx = gravity * horizontalDistance;
        float gx2 = gx * horizontalDistance;
        float discriminant = v4 - gravity * (gx2 + 2 * heightDifference * v2);

        if (discriminant < 0) return 50f;

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float tanAngle = (v2 + sqrtDiscriminant) / gx; // maior ângulo
        float angle = Mathf.Atan(tanAngle) * Mathf.Rad2Deg;
        return Mathf.Max(angle, 45f);
    }

    private float CalculateSpeedForAngle(float horizontalDistance, float heightDifference, float angle, float gravity)
    {
        if (horizontalDistance < 0.1f) return 10f;

        float angleRad = angle * Mathf.Deg2Rad;
        float tanAngle = Mathf.Tan(angleRad);
        float cosAngle = Mathf.Cos(angleRad);

        float denominator = 2f * cosAngle * cosAngle * (horizontalDistance * tanAngle - heightDifference);
        if (denominator <= 0f) return 15f;

        float v2 = (gravity * horizontalDistance * horizontalDistance) / denominator;
        return Mathf.Sqrt(Mathf.Max(v2, 25f)); // clamp mínimo
    }

    private bool ShouldUsePhysicsShot()
    {
        if (!usePhysicsWhenAreaEffectorsDetected || target == null) return false;

        Vector2 start = muzzleTransform ? (Vector2)muzzleTransform.position : (Vector2)transform.position;
        Vector2 end = (Vector2)target.position;

        float segmentDistance = Vector2.Distance(start, end);
        if (segmentDistance <= 0.0001f) return false;

        Vector2 center = (start + end) * 0.5f;
        Vector2 size = new Vector2(segmentDistance, Mathf.Max(0.01f, effectorDetectionWidth));
        float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, angle, effectorDetectionMask);
        if (hits == null || hits.Length == 0) return false;

        foreach (Collider2D hit in hits)
        {
            if (!hit) continue;
            if (hit.transform == transform || (target != null && hit.transform == target)) continue;

            AreaEffector2D effector = hit.GetComponent<AreaEffector2D>();
            if (!effector) effector = hit.GetComponentInParent<AreaEffector2D>();
            if (effector) return true;
        }

        return false;
    }

    public void SetTurnBasedControl(bool enabled)
    {
        if (turnBasedControl == enabled) return;

        turnBasedControl = enabled;
        if (!turnBasedControl)
            shootTimer = GetNextShootDelay();
    }

    public bool CanExecuteTurnShot()
    {
        return projectilePrefab != null && target != null;
    }

    /// <summary>
    /// Para modo por turno: inicia o ciclo de tiro (animação + evento disparam o projétil).
    /// </summary>
    public void ExecuteTurnShot()
    {
        if (!CanExecuteTurnShot())
        {
            if (showDebugInfo)
                Debug.LogWarning("[EnemyShooterAdvancedAI] Turn shot skipped - missing prefab or target.");
            return;
        }

        ShootProjectile();
        shootTimer = GetNextShootDelay();
    }

    // --- Controle direto de física ---
    public void SetPhysicsMode(bool enabled)
    {
        usePhysicsMode = enabled;
        if (showDebugInfo)
            Debug.Log($"[EnemyShooterAdvancedAI] Physics mode set: {enabled}");
    }

    public bool GetPhysicsMode() => usePhysicsMode;

    public void SetPhysicsGravity(float gravity)
    {
        physicsGravityStrength = Mathf.Max(0f, gravity);
        if (showDebugInfo)
            Debug.Log($"[EnemyShooterAdvancedAI] Physics gravity: {physicsGravityStrength}");
    }

    public float GetPhysicsGravity() => physicsGravityStrength;

    private void OnDrawGizmos()
    {
        if (target == null) return;

        Gizmos.color = Color.yellow;
        Vector3 from = muzzleTransform ? muzzleTransform.position : transform.position;
        Gizmos.DrawLine(from, target.position);
        Gizmos.DrawWireSphere(from, 0.4f);
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null || !showDebugInfo) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(target.position, 0.5f);

        float accuracy = Mathf.Clamp01(currentSettings.accuracy);
        float maxMissRadius = currentSettings.maxMissDistance * (1f - accuracy);

        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(target.position, maxMissRadius);
    }

    public void SetAimAccuracy(float accuracy)
    {
        aimAccuracy = Mathf.Clamp01(accuracy);
        CacheSettingsFromFields();
    }

    public float GetAimAccuracy() => currentSettings.accuracy;

    public ShooterSettings GetCurrentSettings() => currentSettings;

    public Transform GetTarget() => target;

    public void ApplySettings(ShooterSettings settings, bool resetShootTimer = true)
    {
        settings.accuracy = Mathf.Clamp01(settings.accuracy);
        settings.shootInterval = Mathf.Max(0.1f, settings.shootInterval);
        settings.shootIntervalJitter = Mathf.Clamp01(settings.shootIntervalJitter);
        settings.projectileSpeed = Mathf.Max(0.1f, settings.projectileSpeed);
        settings.projectileArcHeight = Mathf.Max(0.01f, settings.projectileArcHeight);
        settings.projectileSpin = Mathf.Max(0f, settings.projectileSpin);
        settings.heightNoise = Mathf.Clamp01(settings.heightNoise);
        settings.minMissDistance = Mathf.Max(0f, settings.minMissDistance);
        settings.maxMissDistance = Mathf.Max(settings.minMissDistance, settings.maxMissDistance);

        aimAccuracy = settings.accuracy;
        minAimOffsetDistance = settings.minMissDistance;
        maxAimOffsetDistance = settings.maxMissDistance;

        baseShootRate = settings.shootInterval;
        shootRateNoiseAmount = settings.shootIntervalJitter;

        baseProjectileMaxMoveSpeed = settings.projectileSpeed;
        baseProjectileMaxHeight = settings.projectileArcHeight;
        baseProjectileRotationSpeed = settings.projectileSpin;
        heightNoiseAmount = settings.heightNoise;

        CacheSettingsFromFields();

        if (Application.isPlaying && resetShootTimer)
            shootTimer = GetNextShootDelay();
    }

    private void CacheSettingsFromFields()
    {
        aimAccuracy = Mathf.Clamp01(aimAccuracy);
        minAimOffsetDistance = Mathf.Max(0f, minAimOffsetDistance);
        maxAimOffsetDistance = Mathf.Max(minAimOffsetDistance, maxAimOffsetDistance);
        baseShootRate = Mathf.Max(0.1f, baseShootRate);
        shootRateNoiseAmount = Mathf.Clamp01(shootRateNoiseAmount);
        baseProjectileMaxMoveSpeed = Mathf.Max(0.1f, baseProjectileMaxMoveSpeed);
        baseProjectileMaxHeight = Mathf.Max(0.01f, baseProjectileMaxHeight);
        baseProjectileRotationSpeed = Mathf.Max(0f, baseProjectileRotationSpeed);
        heightNoiseAmount = Mathf.Clamp01(heightNoiseAmount);

        currentSettings = new ShooterSettings
        {
            accuracy = aimAccuracy,
            minMissDistance = minAimOffsetDistance,
            maxMissDistance = maxAimOffsetDistance,
            shootInterval = baseShootRate,
            shootIntervalJitter = shootRateNoiseAmount,
            projectileSpeed = baseProjectileMaxMoveSpeed,
            projectileArcHeight = baseProjectileMaxHeight,
            projectileSpin = baseProjectileRotationSpeed,
            heightNoise = heightNoiseAmount
        };
    }

    private void OnValidate()
    {
        CacheSettingsFromFields();
    }
}
