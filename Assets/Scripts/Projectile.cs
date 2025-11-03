using UnityEngine;

public class Projectile : MonoBehaviour
{
    // velocidade atual (vx, vy)
    private Vector2 velocity;

    // "gravidade" que puxa pra baixo
    private float gravityStrength;

    // só pra visual girar
    private float rotationSpeed;

    // optional data expected by other systems
    private Transform aimTarget;
    private AnimationCurve trajectoryAnimationCurve;
    private AnimationCurve axisCorrectionAnimationCurve;
    private AnimationCurve projectileSpeedAnimationCurve;

    private void Update()
    {
        float dt = Time.deltaTime;

        // aplica gravidade manual (faz a parábola SEMPRE existir)
        velocity += new Vector2(0f, -gravityStrength) * dt;

        // atualiza posição com a velocidade atual
        Vector3 newPos = transform.position + (Vector3)(velocity * dt);

        // trava no plano 2D
        newPos.z = 0f;

        transform.position = newPos;

        // rotação visual (puramente cosmética)
        transform.Rotate(Vector3.forward, rotationSpeed * dt);
    }

    // chamado pelo Shooter (existing initializer)
    public void Initialize(
        Vector2 initialVelocity,
        float gravityStrength,
        float rotationSpeed
    )
    {
        this.velocity = initialVelocity;
        this.gravityStrength = gravityStrength;
        this.rotationSpeed = rotationSpeed;
    }

    // --- Added to satisfy EnemyShooterAdvancedAI expectations ---

    // Called by EnemyShooterAdvancedAI after Instantiate(...)
    // Creates a simple initial velocity aimed at the provided target and calls the existing Initialize(...)
    public void InitializeProjectile(Transform target, float maxMoveSpeed, float trajectoryMaxHeight, float projectileRotationSpeed)
    {
        if (target == null)
        {
            // fallback: do nothing, keep existing velocity
            return;
        }

        aimTarget = target;

        // Simple aimed initial velocity:
        // - horizontal component points toward target and uses maxMoveSpeed
        // - vertical component provides an upward boost proportional to requested arc height and distance
        Vector2 toTarget = (Vector2)(target.position - transform.position);
        float distance = Mathf.Max(0.001f, toTarget.magnitude);
        Vector2 dir = toTarget / distance;

        // Compute a basic upward component: scale by arc height and distance (not a full ballistic solver,
        // but works for gameplay). You can replace this with a full ballistic calculation later.
        float approximateGravity = 9.81f;
        float upward = Mathf.Sqrt(2f * approximateGravity * Mathf.Max(0.01f, trajectoryMaxHeight * distance * 0.5f));

        Vector2 initialVelocity = new Vector2(dir.x * maxMoveSpeed, upward);

        // Use a reasonable gravity strength (can be adjusted by the shooter via curves or separate parameter)
        float gravityToUse = approximateGravity;

        Initialize(initialVelocity, gravityToUse, projectileRotationSpeed);
    }

    // Store animation curves if shooter provides them (not used by this simple projectile implementation,
    // but kept for compatibility)
    public void InitializeAnimationCurves(AnimationCurve trajectoryAnimationCurve,
                                          AnimationCurve axisCorrectionAnimationCurve,
                                          AnimationCurve projectileSpeedAnimationCurve)
    {
        this.trajectoryAnimationCurve = trajectoryAnimationCurve;
        this.axisCorrectionAnimationCurve = axisCorrectionAnimationCurve;
        this.projectileSpeedAnimationCurve = projectileSpeedAnimationCurve;
    }
}
