using UnityEngine;

public class Shooter : MonoBehaviour
{
    [Header("Projectile Prefab")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Mode")]
    [SerializeField] private bool useManualShooting = true; // ON = Manual / OFF = AI

    // -------- Manual settings (physics) --------
    [Header("Manual (Physics)")]
    [SerializeField] private float minLaunchSpeed = 5f;
    [SerializeField] private float maxLaunchSpeed = 20f;
    [SerializeField] private float maxChargeDistance = 10f;
    [SerializeField] private float gravityStrength = 9f;    // m/s²
    [SerializeField] private float projectileRotationSpeed = 180f;

    // -------- AI settings (curve) --------
    [Header("AI (Curve)")]
    [SerializeField] private Transform target;
    [SerializeField] private float shootRate = 1f;
    [SerializeField] private float projectileMaxMoveSpeed = 6f;
    [SerializeField] private float projectileMaxHeight = 0.5f;
    [SerializeField] private AnimationCurve trajectoryAnimationCurve;
    [SerializeField] private AnimationCurve axisCorrectionAnimationCurve;
    [SerializeField] private AnimationCurve projectileSpeedAnimationCurve;

    private float shootTimer;
    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (useManualShooting) HandleManual();
        else HandleAI();
    }

    // =========================
    //        MANUAL
    // =========================
    private void HandleManual()
    {
        if (projectilePrefab == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        if (mainCam == null)
        {
            Debug.LogError("[Shooter] No MainCamera found. Tag your camera as 'MainCamera'.");
            return;
        }

        Vector3 startPos = transform.position; startPos.z = 0f;
        Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition); mouseWorld.z = 0f;

        Vector2 dir = (mouseWorld - startPos).normalized;
        float dist = Vector2.Distance(startPos, mouseWorld);
        float t = Mathf.Clamp01(dist / Mathf.Max(0.0001f, maxChargeDistance));
        float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, t);
        Vector2 initialVelocity = dir * speed;

        GameObject go = Instantiate(projectilePrefab, startPos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) proj = go.AddComponent<Projectile>();

        // Manual physics init → uses Rigidbody2D (collides with walls)
        proj.Initialize(initialVelocity, gravityStrength, projectileRotationSpeed);
        // Debug.Log("[Shooter] Manual projectile fired.");
    }

    // =========================
    //          AI
    // =========================
    private void HandleAI()
    {
        if (projectilePrefab == null || target == null) return;

        shootTimer += Time.deltaTime;
        if (shootTimer < shootRate) return;
        shootTimer = 0f;

        GameObject go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) proj = go.AddComponent<Projectile>();

        proj.InitializeProjectile(target, projectileMaxMoveSpeed, projectileMaxHeight, projectileRotationSpeed);
        // Curves optional — defaults applied in Projectile if null
        proj.InitializeAnimationCurves(trajectoryAnimationCurve, axisCorrectionAnimationCurve, projectileSpeedAnimationCurve);

        // Debug.Log("[Shooter] AI projectile fired.");
    }
}
