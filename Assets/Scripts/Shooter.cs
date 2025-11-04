using UnityEngine;

public class Shooter : MonoBehaviour
{
    [Header("Prefab do projétil")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Boca do tiro (opcional)")]
    [SerializeField] private Transform muzzleTransform;

    [Header("Força do disparo (controlada pela distância do mouse)")]
    [SerializeField] private float minLaunchSpeed = 5f;     // tiro fraquinho
    [SerializeField] private float maxLaunchSpeed = 20f;    // tiro forte
    [SerializeField] private float maxChargeDistance = 10f; // distância pra já ser força máxima

    [Header("Gravidade manual aplicada no projétil")]
    [SerializeField] private float gravityStrength = 9f;

    [Header("Rotação visual do projétil")]
    [SerializeField] private float projectileRotationSpeed = 180f;

    // ===== Integração com Animator =====
    [Header("Animation")]
    [SerializeField] private Animator animator;                    // arraste o Animator do Player
    [SerializeField] private string shootBoolName = "is_shooting"; // mesmo parâmetro do controller
    [SerializeField] private string shootStateName = "Shoot";      // nome do state de disparo
    [Tooltip("Failsafe: se o evento final não disparar, desliga o bool após este tempo (s).")]
    [SerializeField] private float shootClipMaxLength = 0.6f;

    [Tooltip("Se o Animation Event de disparo não vier, dispara automaticamente após este atraso (s).")]
    [SerializeField] private bool fireFailsafeIfNoEvent = true;
    [SerializeField] private float fireFailsafeDelay = 0.15f;

    // ===== Controle interno =====
    private bool firedThisCycle = false;           // garante 1 projétil por animação
    private Coroutine shootFailsafeCoro;
    private Coroutine fireFailsafeCoro;

    // Guardamos a mira no momento do clique para usar no frame do evento
    private Vector3 cachedStartPos;
    private Vector3 cachedMouseWorldPos;

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Update()
    {
        // Clique para iniciar o disparo (liga a animação; NÃO instancia ainda)
        if (Input.GetMouseButtonDown(0))
        {
            // Cache da posição de saída e da mira
            cachedStartPos = muzzleTransform ? muzzleTransform.position : transform.position;
            cachedStartPos.z = 0f;

            Vector3 mouseScreenPos = Input.mousePosition;
            cachedMouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            cachedMouseWorldPos.z = 0f;

            StartShootCycle();
        }
    }

    private void StartShootCycle()
    {
        // Se não tem animator, dispare direto (fallback)
        if (!animator)
        {
            Fire();
            return;
        }

        // Evita reentrar no meio do Shoot
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName(shootStateName)) return;

        firedThisCycle = false;

        animator.SetBool(shootBoolName, true);

        // Failsafe: se o fim da animação não resetar o bool
        if (shootFailsafeCoro != null) StopCoroutine(shootFailsafeCoro);
        shootFailsafeCoro = StartCoroutine(ResetShootBoolFailsafe(shootClipMaxLength));

        // Failsafe: se o Animation Event de tiro não vier, dispara sozinho
        if (fireFailsafeIfNoEvent)
        {
            if (fireFailsafeCoro != null) StopCoroutine(fireFailsafeCoro);
            fireFailsafeCoro = StartCoroutine(FireFailsafeAfter(fireFailsafeDelay));
        }
    }

    private System.Collections.IEnumerator ResetShootBoolFailsafe(float t)
    {
        yield return new WaitForSeconds(t);
        if (animator) animator.SetBool(shootBoolName, false);
        shootFailsafeCoro = null;
    }

    private System.Collections.IEnumerator FireFailsafeAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (!firedThisCycle) Fire();
        fireFailsafeCoro = null;
    }

    // ===== Animation Events (adicione no clip do Player) =====
    // Coloque este evento no frame do disparo
    public void OnShootFire()
    {
        if (firedThisCycle) return;
        Fire();
    }

    // Coloque este evento no final da animação
    public void OnShootAnimEnd()
    {
        if (animator) animator.SetBool(shootBoolName, false);
        if (shootFailsafeCoro != null) { StopCoroutine(shootFailsafeCoro); shootFailsafeCoro = null; }
    }

    // ===== Disparo real do projétil =====
    private void Fire()
    {
        firedThisCycle = true;

        // Direção da mira a partir do cache do clique
        Vector2 dir = (cachedMouseWorldPos - cachedStartPos).normalized;

        // Distância até o mouse → controla a força
        float distToMouse = Vector2.Distance(cachedStartPos, cachedMouseWorldPos);
        float t = Mathf.Clamp01(distToMouse / maxChargeDistance);
        float launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, t);

        Vector2 initialVelocity = dir * launchSpeed;

        // Instancia projétil
        Vector3 spawnPos = cachedStartPos; // já é muzzle ou posição do player
        Quaternion spawnRot = Quaternion.identity;

        GameObject projGO = Instantiate(projectilePrefab, spawnPos, spawnRot);
        Projectile proj = projGO.GetComponent<Projectile>();

        // Importante: seu prefab deve ter o script Projectile no GO raiz
        proj.Initialize(
            initialVelocity,
            gravityStrength,
            projectileRotationSpeed
        );
    }
}
