using UnityEngine;

public class Shooter : MonoBehaviour
{
    [Header("Projectile Prefab")]
    [SerializeField] private GameObject projectilePrefab;

    // ---------- Manual shooting ----------
    [Header("Manual Shoot (Mouse)")]
    [SerializeField] private bool useManualShooting = true; // ON = manual; OFF = AI
    [SerializeField] private float minLaunchSpeed = 5f;
    [SerializeField] private float maxLaunchSpeed = 20f;
    [SerializeField] private float maxChargeDistance = 10f;
    [SerializeField] private float gravityStrength = 9f;
    [SerializeField] private float projectileRotationSpeed = 180f;

    // ---------- AI shooting ----------
    [Header("AI Shoot (Optional if you use EnemyShooterAdvancedAI elsewhere)")]
    [SerializeField] private Transform target;
    [SerializeField] private float shootRate = 1f;         // seconds between shots
    [SerializeField] private float projectileMaxMoveSpeed = 6f;
    [SerializeField] private float projectileMaxHeight = 0.5f;
    [SerializeField] private AnimationCurve trajectoryAnimationCurve;
    [SerializeField] private AnimationCurve axisCorrectionAnimationCurve;
    [SerializeField] private AnimationCurve projectileSpeedAnimationCurve;

    private float shootTimer = 0f;
    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void Start()
    {
        Debug.Log($"[Shooter] Start - Prefab: {(projectilePrefab ? projectilePrefab.name : "NULL")} | Mode: {(useManualShooting ? "Manual" : "AI")}");
        if (!useManualShooting)
        {
            Debug.Log($"[Shooter] AI target: {(target ? target.name : "NULL")} | shootRate: {shootRate} | maxSpeed: {projectileMaxMoveSpeed}");
        }
    }

    private void Update()
    {
        if (useManualShooting)
        {
            HandleManualShooting();
        }
        else
        {
            HandleAIShooting();
        }
    }

    // =========================
    //       MANUAL MODE
    // =========================
    private void HandleManualShooting()
    {
        if (projectilePrefab == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (mainCam == null)
            {
                Debug.LogError("[Shooter] No MainCamera found. Tag your camera as 'MainCamera'.");
                return;
            }

            // spawn position (usually at player muzzle)
            Vector3 startPos = transform.position;
            startPos.z = 0f;

            // mouse world
            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            // direction + strength
            Vector2 dir = (mouseWorld - startPos).normalized;
            float dist = Vector2.Distance(startPos, mouseWorld);
            float t = Mathf.Clamp01(dist / Mathf.Max(0.0001f, maxChargeDistance));
            float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, t);
            Vector2 initialVelocity = dir * speed;

            // instantiate
            GameObject go = Instantiate(projectilePrefab, startPos, Quaternion.identity);
            Projectile proj = go.GetComponent<Projectile>();
            if (proj == null) proj = go.AddComponent<Projectile>();

            // manual init (simple parabola)
            proj.Initialize(initialVelocity, gravityStrength, projectileRotationSpeed);
            Debug.Log("[Shooter] Manual projectile fired.");
        }
    }

    // =========================
    //         AI MODE
    // =========================
    private void HandleAIShooting()
    {
        if (projectilePrefab == null || target == null) return;

        shootTimer += Time.deltaTime;
        if (shootTimer < shootRate) return;
        shootTimer = 0f;

        // Spawn + initialize for curve/target path
        GameObject go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Projectile projectile = go.GetComponent<Projectile>();
        if (projectile == null) projectile = go.AddComponent<Projectile>();

        projectile.InitializeProjectile(target, projectileMaxMoveSpeed, projectileMaxHeight, projectileRotationSpeed);

        // Curves can be null; Projectile adds safe defaults so AI still works.
        projectile.InitializeAnimationCurves(trajectoryAnimationCurve,
                                             axisCorrectionAnimationCurve,
                                             projectileSpeedAnimationCurve);

        Debug.Log("[Shooter] AI projectile fired.");
    }
}
