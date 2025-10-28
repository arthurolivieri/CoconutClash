using UnityEngine;

public class Shooter : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;

    [Header("Cadência")]
    [SerializeField] private float shootRate = 0.5f; // tempo entre tiros
    private float shootTimer;

    [Header("Escala da força pelo alcance do mouse")]
    [SerializeField] private float baseMoveSpeed = 5f;
    [SerializeField] private float extraSpeedPerUnit = 1f;

    [SerializeField] private float baseArcHeight = 0.5f;
    [SerializeField] private float extraArcPerUnit = 0.1f;

    [Header("Rotação do projétil")]
    [SerializeField] private float projectileRotationSpeed = 180f;

    [Header("Curvas do projétil")]
    [SerializeField] private AnimationCurve trajectoryAnimationCurve;
    [SerializeField] private AnimationCurve axisCorrectionAnimationCurve;
    [SerializeField] private AnimationCurve projectileSpeedAnimationCurve;

    private void Update()
    {
        // atualiza cooldown
        shootTimer -= Time.deltaTime;

        // só dispara se:
        // 1) botão esquerdo foi clicado nesse frame
        // 2) passou o cooldown
        if (Input.GetMouseButtonDown(0) && shootTimer <= 0f)
        {
            // reseta cooldown
            shootTimer = shootRate;

            // pega posição do mouse no mundo
            Vector3 mouseScreenPos = Input.mousePosition;
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0f;

            // calcula distância do player até o mouse
            float distance = Vector3.Distance(transform.position, mouseWorldPos);

            // força do tiro baseada nessa distância
            float projectileMaxMoveSpeed = baseMoveSpeed + distance * extraSpeedPerUnit;
            float projectileMaxHeight   = baseArcHeight  + distance * extraArcPerUnit;

            // cria alvo temporário na posição do mouse
            GameObject mouseTargetGO = new GameObject("MouseTargetTemp");
            mouseTargetGO.transform.position = mouseWorldPos;

            // instancia o projétil
            Projectile projectile = Instantiate(
                projectilePrefab,
                transform.position,
                Quaternion.identity
            ).GetComponent<Projectile>();

            // inicializa o projétil
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

            // limpa o target temporário depois
            Destroy(mouseTargetGO, 2f);
        }
    }
}
