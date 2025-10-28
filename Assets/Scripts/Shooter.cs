using UnityEngine;

public class Shooter : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform target;

    [SerializeField] private float shootRate;
    [SerializeField] private float projectileMaxMoveSpeed;
    [SerializeField] private float projectileMaxHeight;
    [SerializeField] private float projectileRotationSpeed;

    [SerializeField] private AnimationCurve trajectoryAnimationCurve;
    [SerializeField] private AnimationCurve axisCorrectionAnimationCurve;
    [SerializeField] private AnimationCurve projectileSpeedAnimationCurve;

    private float shootTimer;

    private void Start()
    {
        Debug.Log($"[Shooter] Start - Validating configuration...");
        Debug.Log($"[Shooter] projectilePrefab: {(projectilePrefab != null ? projectilePrefab.name : "NULL")}");
        Debug.Log($"[Shooter] target: {(target != null ? target.name : "NULL")}");
        Debug.Log($"[Shooter] shootRate: {shootRate}");
        Debug.Log($"[Shooter] projectileMaxMoveSpeed: {projectileMaxMoveSpeed}");
        Debug.Log($"[Shooter] trajectoryAnimationCurve: {(trajectoryAnimationCurve != null ? "SET" : "NULL")}");
        Debug.Log($"[Shooter] axisCorrectionAnimationCurve: {(axisCorrectionAnimationCurve != null ? "SET" : "NULL")}");
        Debug.Log($"[Shooter] projectileSpeedAnimationCurve: {(projectileSpeedAnimationCurve != null ? "SET" : "NULL")}");
    }

    private void Update()
    {
        shootTimer -= Time.deltaTime;

        if (shootTimer <= 0) {
            shootTimer = shootRate;
            
            // Validate before shooting
            if (projectilePrefab == null) {
                Debug.LogError("[Shooter] Cannot shoot: projectilePrefab is NULL!");
                return;
            }
            
            if (target == null) {
                Debug.LogError("[Shooter] Cannot shoot: target is NULL!");
                return;
            }
            
            Debug.Log($"[Shooter] Attempting to shoot projectile at position {transform.position} towards target {target.name} at {target.position}");
            
            GameObject projectileObj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            Projectile projectile = projectileObj.GetComponent<Projectile>();
            
            if (projectile == null) {
                Debug.LogError($"[Shooter] Cannot shoot: projectilePrefab '{projectilePrefab.name}' does not have a Projectile component!");
                Destroy(projectileObj);
                return;
            }
            
            Debug.Log($"[Shooter] Projectile instantiated. Initializing with maxSpeed={projectileMaxMoveSpeed}, maxHeight={projectileMaxHeight}, rotationSpeed={projectileRotationSpeed}");
            
            projectile.InitializeProjectile(target,
                                            projectileMaxMoveSpeed,
                                            projectileMaxHeight,
                                            projectileRotationSpeed);
            
            Debug.Log($"[Shooter] Initializing animation curves...");
            projectile.InitializeAnimationCurves(trajectoryAnimationCurve,
                                                 axisCorrectionAnimationCurve,
                                                 projectileSpeedAnimationCurve);
            
            Debug.Log($"[Shooter] Projectile fully initialized!");
        }
    }
}
