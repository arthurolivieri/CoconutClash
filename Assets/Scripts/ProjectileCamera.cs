using UnityEngine;

/// <summary>
/// Makes the camera smoothly follow an active projectile.
/// Falls back to following the player when no projectile exists.
/// </summary>
public class ProjectileCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform fallbackTarget; // Player ou ponto fixo
    
    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private bool followProjectile = true;
    
    [Header("Bounds (opcional)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minBounds = new Vector2(-20f, -10f);
    [SerializeField] private Vector2 maxBounds = new Vector2(20f, 10f);

    [Header("Return Settings")]
    [Tooltip("Quanto tempo a câmera fica parada no ponto do impacto antes de voltar para o fallback.")]
    [SerializeField] private float returnDelay = 0.5f;

    // Estado interno
    private Projectile currentProjectile;
    private Transform targetTransform;

    private bool returningFromProjectile = false;
    private float returnTimer = 0f;
    private Vector3 lastProjectilePosition;
    private bool hadProjectileLastFrame = false;

    private void LateUpdate()
    {
        Projectile projectileFound = null;

        // Enquanto estamos em "delay após impacto", NÃO procuramos novo projétil
        if (followProjectile && !returningFromProjectile)
        {
            projectileFound = FindObjectOfType<Projectile>();
        }

        bool hasProjectileNow = projectileFound != null;

        // Se existe projétil neste frame, usamos ele como atual e guardamos posição
        if (hasProjectileNow)
        {
            currentProjectile = projectileFound;
            lastProjectilePosition = currentProjectile.transform.position;
        }

        // Se NÃO existe projétil agora, mas no frame passado existia → acabou de acontecer um impacto
        if (!hasProjectileNow && hadProjectileLastFrame && !returningFromProjectile)
        {
            if (returnDelay > 0f)
            {
                returningFromProjectile = true;
                returnTimer = returnDelay;
            }

            // Não precisamos mais da referência ao projétil destruído
            currentProjectile = null;
        }

        hadProjectileLastFrame = hasProjectileNow;

        // Atualiza o timer do delay pós-impacto
        if (returningFromProjectile)
        {
            returnTimer -= Time.deltaTime;
            if (returnTimer <= 0f)
            {
                returningFromProjectile = false;
            }
        }

        Vector3 desiredPosition;

        if (returningFromProjectile)
        {
            // Durante o delay, fica olhando para o último ponto onde o projétil existiu
            desiredPosition = lastProjectilePosition + offset;
        }
        else
        {
            // Fora do delay: segue projétil se tiver, senão o fallback
            targetTransform = (followProjectile && currentProjectile != null)
                ? currentProjectile.transform
                : fallbackTarget;

            if (targetTransform == null) return;

            desiredPosition = targetTransform.position + offset;
        }

        // Aplica bounds se habilitado
        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minBounds.y, maxBounds.y);
        }

        // Suaviza movimento
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        smoothedPosition.z = offset.z; // Garante Z da câmera

        transform.position = smoothedPosition;
    }

    // Método público para forçar seguir um projétil específico (se você quiser usar em outro script)
    public void SetProjectile(Projectile projectile)
    {
        currentProjectile = projectile;

        if (projectile != null)
        {
            lastProjectilePosition = projectile.transform.position;
            returningFromProjectile = false;
            returnTimer = 0f;
            hadProjectileLastFrame = true;
        }
        else
        {
            hadProjectileLastFrame = false;
        }
    }

    // Método para voltar ao fallback imediatamente (sem delay)
    public void ResetToFallback()
    {
        currentProjectile = null;
        returningFromProjectile = false;
        returnTimer = 0f;
        hadProjectileLastFrame = false;
    }

    // Método para trocar o fallback target
    public void SetFallbackTarget(Transform newTarget)
    {
        fallbackTarget = newTarget;
    }
}
