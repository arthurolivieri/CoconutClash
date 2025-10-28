using UnityEngine;

public class Shooter : MonoBehaviour
{
    [Header("Prefab do projétil")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Força do disparo (controlada pela distância do mouse)")]
    [SerializeField] private float minLaunchSpeed = 5f;     // tiro fraquinho
    [SerializeField] private float maxLaunchSpeed = 20f;    // tiro forte
    [SerializeField] private float maxChargeDistance = 10f; // distância pra já ser força máxima

    [Header("Gravidade manual aplicada no projétil")]
    [SerializeField] private float gravityStrength = 9f;

    [Header("Rotação visual do projétil")]
    [SerializeField] private float projectileRotationSpeed = 180f;

    private void Update()
    {
        // toda vez que você CLICA botão esquerdo (down) gera UMA bala
        if (Input.GetMouseButtonDown(0))
        {
            // posição da boca do tiro (normalmente seu player)
            Vector3 startPos = transform.position;
            startPos.z = 0f;

            // pega posição do mouse no mundo
            Vector3 mouseScreenPos = Input.mousePosition;
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0f;

            // direção normalizada da mira
            Vector2 dir = (mouseWorldPos - startPos).normalized;

            // distância até o mouse → controla quão forte é o tiro
            float distToMouse = Vector2.Distance(startPos, mouseWorldPos);

            // converte distância em velocidade inicial entre min e max
            float t = Mathf.Clamp01(distToMouse / maxChargeDistance);
            float launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, t);

            // velocidade inicial
            Vector2 initialVelocity = dir * launchSpeed;

            // instancia projétil
            GameObject projGO = Instantiate(
                projectilePrefab,
                startPos,
                Quaternion.identity
            );

            Projectile proj = projGO.GetComponent<Projectile>();

            // se isso aqui for null, quer dizer que SEU PREFAB não tem o script Projectile na raiz
            // isso daria NullReference na hora e o jogo travaria depois de alguns tiros.
            // então é muito importante que o script Projectile esteja no GameObject raiz do prefab.
            proj.Initialize(
                initialVelocity,
                gravityStrength,
                projectileRotationSpeed
            );
        }
    }
}
