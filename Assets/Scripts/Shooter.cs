using System;
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
    [SerializeField] private float gravityStrength = 9f;    
    [SerializeField] private float projectileRotationSpeed = 180f;

    [Header("Muzzle (optional)")]
    [SerializeField] private Transform muzzleTransform;

    // ===== ANIMAÇÃO =====
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string shootBoolName = "is_shooting";
    [SerializeField] private string shootStateName = "Shoot";
    [SerializeField] private float shootClipMaxLength = 0.6f;
    [SerializeField] private bool fireFailsafeIfNoEvent = true;
    [SerializeField] private float fireFailsafeDelay = 0.15f;

    private bool firedThisCycle = false;
    private Coroutine shootFailsafeCoro;
    private Coroutine fireFailsafeCoro;

    // ===== Turn-Based =====
    [Header("Turn Based")]
    [SerializeField] private bool restrictManualShootingToTurn = false;
    private bool manualTurnEnabled = true;

    // -------- AI settings (curve) --------
    [Header("AI (Curve)")]
    [SerializeField] private Transform target;
    [SerializeField] private float shootRate = 1f;
    [SerializeField] private float projectileMaxMoveSpeed = 6f;
    [SerializeField] private float projectileMaxHeight = 0.5f;
    [SerializeField] private AnimationCurve trajectoryAnimationCurve;
    [SerializeField] private AnimationCurve axisCorrectionAnimationCurve;
    [SerializeField] private AnimationCurve projectileSpeedAnimationCurve;

    [Header("Physics Setup")]
    [SerializeField] private int projectileLayer = 10;

    private Camera mainCam;
    private float shootTimer;

    // Mira armazenada
    private Vector3 cachedStartPos;
    private Vector3 cachedMouseWorldPos;

    public event Action ManualShotFired;
    public event Action<Projectile> ProjectileCreated;

    private void Awake()
    {
        mainCam = Camera.main;
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (useManualShooting) HandleManual();
        else HandleAI();
    }

    // ========================= MANUAL =========================
    private void HandleManual()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (projectilePrefab == null) return;
        if (restrictManualShootingToTurn && !manualTurnEnabled) return;

        cachedStartPos = muzzleTransform ? muzzleTransform.position : transform.position;
        cachedStartPos.z = 0f;

        cachedMouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        cachedMouseWorldPos.z = 0f;

        StartShootCycle();
    }

    private void StartShootCycle()
    {
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

    // ===== Animation Events =====
    public void OnShootFire()
    {
        if (firedThisCycle) return;
        FireManualProjectile();
    }

    public void OnShootAnimEnd()
    {
        animator.SetBool(shootBoolName, false);
        if (shootFailsafeCoro != null) StopCoroutine(shootFailsafeCoro);
    }

    private System.Collections.IEnumerator ResetShootBoolFailsafe(float t)
    {
        yield return new WaitForSeconds(t);
        animator.SetBool(shootBoolName, false);
        shootFailsafeCoro = null;
    }

    private System.Collections.IEnumerator FireFailsafeAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (!firedThisCycle) FireManualProjectile();
        fireFailsafeCoro = null;
    }

    private void FireManualProjectile()
    {
        firedThisCycle = true;

        Vector2 dir = (cachedMouseWorldPos - cachedStartPos).normalized;
        float dist = Vector2.Distance(cachedStartPos, cachedMouseWorldPos);
        float t = Mathf.Clamp01(dist / Mathf.Max(0.0001f, maxChargeDistance));
        float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, t);
        Vector2 initialVelocity = dir * speed;

        GameObject go = Instantiate(projectilePrefab, cachedStartPos, Quaternion.identity);
        if (projectileLayer > 0) go.layer = projectileLayer;

        var proj = go.GetComponent<Projectile>();
        if (!proj) proj = go.AddComponent<Projectile>();

        proj.Initialize(initialVelocity, gravityStrength, projectileRotationSpeed);
        
        ProjectileCreated?.Invoke(proj);
        ManualShotFired?.Invoke();
    }

    // ========================= AI =========================
    private void HandleAI()
    {
        if (projectilePrefab == null || target == null) return;

        shootTimer += Time.deltaTime;
        if (shootTimer < shootRate) return;
        shootTimer = 0f;

        GameObject go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        if (projectileLayer > 0) go.layer = projectileLayer;

        var proj = go.GetComponent<Projectile>();
        if (!proj) proj = go.AddComponent<Projectile>();

        proj.InitializeProjectile(target, projectileMaxMoveSpeed, projectileMaxHeight, projectileRotationSpeed);
        proj.InitializeAnimationCurves(trajectoryAnimationCurve, axisCorrectionAnimationCurve, projectileSpeedAnimationCurve);
    }

    // ========================= TURN SYSTEM SUPPORT =========================
    public void SetRestrictManualShootingToTurn(bool restrict)
    {
        restrictManualShootingToTurn = restrict;
    }

    public void SetManualTurnEnabled(bool enabled)
    {
        manualTurnEnabled = enabled;
    }

    // ========================= PREVIEW SUPPORT =========================
    /// <summary>
    /// Returns the gravity strength used for manual physics shots (in m/s^2).
    /// </summary>
    public float GetGravityStrength()
    {
        return gravityStrength;
    }

    /// <summary>
    /// Returns the current muzzle/world start position used for shooting.
    /// </summary>
    public Vector3 GetMuzzleOrPosition()
    {
        Vector3 p = muzzleTransform ? muzzleTransform.position : transform.position;
        p.z = 0f;
        return p;
    }

    /// <summary>
    /// Predicts the initial velocity that will be used if the player shoots towards targetPos.
    /// Mirrors the manual shooting charge logic.
    /// </summary>
    public Vector2 PredictInitialVelocity(Vector2 startPos, Vector2 targetPos)
    {
        Vector2 dir = (targetPos - startPos).normalized;
        float dist = Vector2.Distance(startPos, targetPos);
        float t = Mathf.Clamp01(dist / Mathf.Max(0.0001f, maxChargeDistance));
        float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, t);
        return dir * speed;
    }

    /// <summary>
    /// Returns true if this shooter is in manual (player) mode.
    /// </summary>
    public bool IsManualMode()
    {
        return useManualShooting;
    }

    /// <summary>
    /// Returns true if the player is currently allowed to aim/shoot (considering turn restriction).
    /// </summary>
    public bool IsManualAimAllowed()
    {
        if (!useManualShooting) return false;
        if (!restrictManualShootingToTurn) return true;
        return manualTurnEnabled;
    }
}
