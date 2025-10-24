using UnityEngine;

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

    private void Start() {
        trajectoryStartPoint = transform.position;
    }

    private void Update() {

        UpdateProjectilePosition();
        UpdateProjectileRotation();

        if (Vector3.Distance(transform.position, target.position) < distanceToTargetToDestroyProjectile) {
            Destroy(gameObject);
        }
    }

    private void UpdateProjectilePosition() {
        Vector3 trajectoryRange = target.position - trajectoryStartPoint;

        if (trajectoryRange.x < 0) {
            // Shooter is located behind the target
            moveSpeed = -moveSpeed;
        }

        float nextPositionX = transform.position.x + moveSpeed * Time.deltaTime;
        float nextPositionXNormalized = (nextPositionX - trajectoryStartPoint.x) / trajectoryRange.x;

        float nextPositionYNormalized = trajectoryAnimationCurve.Evaluate(nextPositionXNormalized);
        float nextPositionYCorrectionNormalized = axisCorrectionAnimationCurve.Evaluate(nextPositionXNormalized);
        float nextPositionYCorrectionAbsolute = nextPositionYCorrectionNormalized * trajectoryRange.y;

        float nextPositionY = trajectoryStartPoint.y + nextPositionYNormalized * trajectoryMaxRelativeHeight + nextPositionYCorrectionAbsolute;
    
        Vector3 newPosition = new Vector3(nextPositionX, nextPositionY, 0);

        CalculateNextProjectileSpeed(nextPositionXNormalized);

        transform.position = newPosition;
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
        this.target = target;
        this.maxMoveSpeed = maxMoveSpeed;
        float xDistanceToTarget = target.position.x - transform.position.x;
        this.trajectoryMaxRelativeHeight = Mathf.Abs(xDistanceToTarget) * trajectoryMaxHeight;
        this.projectileRotationSpeed = projectileRotationSpeed;
    }

    public void InitializeAnimationCurves(AnimationCurve trajectoryAnimationCurve,
                                          AnimationCurve axisCorrectionAnimationCurve,
                                          AnimationCurve projectileSpeedAnimationCurve) {
        this.trajectoryAnimationCurve = trajectoryAnimationCurve;
        this.axisCorrectionAnimationCurve = axisCorrectionAnimationCurve;
        this.projectileSpeedAnimationCurve = projectileSpeedAnimationCurve;
    }
    
}
