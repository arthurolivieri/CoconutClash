using UnityEngine;

public class Shooter : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;

    // NÃO usamos mais o target fixo no Inspector
    // [SerializeField] private Transform target;

    [Header("Cadência")]
    [SerializeField] private float shootRate = 0.5f;
    private float shootTimer;

    [Header("Escala da força pelo alcance do mouse")]
    [SerializeField] private float baseMoveSpeed = 5f;      // velocidade mínima da bala
    [SerializeField] private float extraSpeedPerUnit = 1f;  // quanto a velocidade cresce por unidade de distância

    [SerializeField] private float baseArcHeight = 0.5f;      // altura mínima do arco
    [SerializeField] private float extraArcPerUnit = 0.1f;    // quanto a altura do arco cresce por unidade de distância

    [Header("Rotação do projétil")]
    [SerializeField] private float projectileRotationSpeed = 180f;

    [Header("Curvas do projétil")]
    [SerializeField] private AnimationCurve trajectoryAnimationCurve;
    [SerializeField] private AnimationCurve axisCorrectionAnimationCurve;
    [SerializeField] private AnimationCurve projectileSpeedAnimationCurve;

    private void Update()
    {
        shootTimer -= Time.deltaTime;

        if (shootTimer <= 0f)
        {
            shootTimer = shootRate;

            // 1. posição do mouse no mundo (2D)
            Vector3 mouseScreenPos = Input.mousePosition;
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0f;

            // 2. calcula quão longe o mouse está do jogador
            float distance = Vector3.Distance(transform.position, mouseWorldPos);

            // 3. calcula a força baseada nessa distância
            float projectileMaxMoveSpeed = baseMoveSpeed + distance * extraSpeedPerUnit;
            float projectileMaxHeight   = baseArcHeight  + distance * extraArcPerUnit;

            // 4. cria um "alvo" temporário na posição do mouse
            GameObject mouseTargetGO = new GameObject("MouseTargetTemp");
            mouseTargetGO.transform.position = mouseWorldPos;

            // 5. instancia o projétil
            Projectile projectile = Instantiate(
                projectilePrefab,
                transform.position,
                Quaternion.identity
            ).GetComponent<Projectile>();

            // 6. inicializa o projétil usando os valores que dependem da distância
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

            // 7. destrói o alvo auxiliar depois (pra não poluir a cena)
            Destroy(mouseTargetGO, 2f);
        }
    }
}
