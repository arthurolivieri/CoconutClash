using UnityEngine;

public class Shooter : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;

    [SerializeField] private float shootRate;
    [SerializeField] private float projectileMaxMoveSpeed;
    [SerializeField] private float projectileMaxHeight;
    [SerializeField] private float projectileRotationSpeed;

    [SerializeField] private AnimationCurve trajectoryAnimationCurve;
    [SerializeField] private AnimationCurve axisCorrectionAnimationCurve;
    [SerializeField] private AnimationCurve projectileSpeedAnimationCurve;

    private float shootTimer;

    private void Update()
    {
        shootTimer -= Time.deltaTime;

        if (shootTimer <= 0)
        {
            shootTimer = shootRate;

            // 1. pega posição do mouse em coordenadas de mundo (2D)
            Vector3 mouseScreenPos = Input.mousePosition;
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0f; // garantimos z = 0 pro 2D

            // 2. cria um "target" temporário nessa posição
            GameObject mouseTargetGO = new GameObject("MouseTargetTemp");
            mouseTargetGO.transform.position = mouseWorldPos;

            // 3. instancia o projétil
            Projectile projectile = Instantiate(
                projectilePrefab,
                transform.position,
                Quaternion.identity
            ).GetComponent<Projectile>();

            // 4. inicializa o projétil com esse target temporário
            projectile.InitializeProjectile(
                mouseTargetGO.transform,
                projectileMaxMoveSpeed,
                projectileMaxHeight,
                projectileRotationSpeed
            );

            projectile.InitializeAnimationCurves(
                trajectoryAnimationCurve,
                axisCorrectionAnimationCurve,
                projectileSpeedAnimationCurve
            );

            // 5. opcional: destruir o GO auxiliar depois de um tempo
            //    (porque esse objeto só serve pra guardar a posição alvo fixa)
            Destroy(mouseTargetGO, 2f);
        }
    }
}
