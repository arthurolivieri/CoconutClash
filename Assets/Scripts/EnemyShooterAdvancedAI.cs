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

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string shootBoolName = "is_shooting";
    [SerializeField] private string shootStateName = "Shoot";
    [Tooltip("Failsafe: se o evento final não disparar, desliga o bool após este tempo (s).")]
    [SerializeField] private float shootClipMaxLength = 0.6f;

    [Tooltip("Failsafe extra: se o evento de disparo não vier, dispara automaticamente após este atraso (s).")]
    [SerializeField] private bool fireFailsafeIfNoEvent = true;
    [SerializeField] private float fireFailsafeDelay = 0.2f;

    private Coroutine shootFailsafeCoro;
    private Coroutine fireFailsafeCoro;
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

        shootTimer -= Time.deltaTime;

        if (shootTimer <= 0f)
        {
            ShootProjectile();
            shootTimer = GetNextShootDelay();
        }
    }

    private float GetNextShootDelay()
    {
        float noiseValue = GetPerlinNoise(0f);
        float normalizedNoise = (noiseValue - 0.5f) * 2f;
        float delayModifier = 1f + normalizedNoise * shootRateNoiseAmount;
        float delay = baseShootRate * delayModifier;
        return Mathf.Max(0.1f, delay);
    }

    private void ShootProjectile()
    {
        if (!animator) return;

        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName(shootStateName)) return;

        firedThisCycle = false;

        animator.SetBool(shootBoolName, true);

        if (shootFailsafeCoro != null) StopCoroutine(shootFailsafeCoro);
        shootFailsafeCoro = StartCoroutine(ResetShootBoolFailsafe(shootClipMaxLength));

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
            if (showDebugInfo) Debug.Log("[Failsafe] Disparando Fire() por ausência de Animation Event.");
            Fire();
        }
        fireFailsafeCoro = null;
    }

    public void OnShootFire()
    {
        if (firedThisCycle) return;
        if (showDebugInfo) Debug.Log("[AnimEvent] OnShootFire()");
        Fire();
    }

    public void OnShootAnimEnd()
    {
        if (animator) animator.SetBool(shootBoolName, false);
        if (shootFailsafeCoro != null) { StopCoroutine(shootFailsafeCoro); shootFailsafeCoro = null; }
        if (showDebugInfo) Debug.Log("[AnimEvent] Shoot terminou. Voltando para Idle.");
    }

    private void Fire()
    {
        firedThisCycle = true;

        Transform aimTarget = CalculateAimTarget(out bool willHit, out Vector3 aimPointPosition);

        float heightNoise = GetPerlinNoise(100f);
        float heightVariation = 1f + ((heightNoise - 0.5f) * 2f * heightNoiseAmount);
        float projectileMaxHeight = Mathf.Max(0.05f, baseProjectileMaxHeight * heightVariation);

        Vector3 spawnPos = muzzleTransform ? muzzleTransform.position : transform.position;
        Quaternion spawnRot = Quaternion.identity;

        var projComp = Instantiate(projectilePrefab, spawnPos, spawnRot).GetComponent<Projectile>();

        projComp.InitializeProjectile(
            aimTarget,
            baseProjectileMaxMoveSpeed,
            projectileMaxHeight,
            baseProjectileRotationSpeed
        );

        projComp.InitializeAnimationCurves(
            trajectoryAnimationCurve,
            axisCorrectionAnimationCurve,
            projectileSpeedAnimationCurve
        );

        if (aimVisualizer != null)
            aimVisualizer.RecordShot(aimPointPosition, willHit);

        if (showDebugInfo)
        {
            string result = willHit ? "✓ HIT" : "✗ MISS";
            float aimOffset = Vector3.Distance(target.position, aimPointPosition);
            Debug.Log($"[Fire] Enemy AI shot {result} - Speed: {baseProjectileMaxMoveSpeed:F2}, Height: {projectileMaxHeight:F2}, Aim Offset: {aimOffset:F2}");
        }
    }

    private Transform CalculateAimTarget(out bool willHit, out Vector3 aimPointPosition)
    {
        float clampedAccuracy = Mathf.Clamp01(currentSettings.accuracy);
        float roll = Random.value;
        willHit = roll <= clampedAccuracy;

        if (showDebugInfo)
            Debug.Log($"Accuracy Roll: {roll:F3} | Accuracy: {clampedAccuracy:F3} | Will Hit: {willHit}");

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

    private void OnDrawGizmos()
    {
        if (target == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
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

    public float GetAimAccuracy()
    {
        return currentSettings.accuracy;
    }

    public ShooterSettings GetCurrentSettings()
    {
        return currentSettings;
    }

    public Transform GetTarget()
    {
        return target;
    }

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
