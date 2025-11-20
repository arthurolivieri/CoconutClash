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
    
    private Projectile currentProjectile;
    private Transform targetTransform;
    
    private void LateUpdate()
    {
        // Remove a busca automática - agora é controlado externamente
        if (followProjectile && currentProjectile == null)
        {
            currentProjectile = FindObjectOfType<Projectile>();
        }
        
        // Se projétil foi destruído, limpa referência
        if (currentProjectile != null && currentProjectile.gameObject == null)
        {
            currentProjectile = null;
        }
        
        // Define target
        targetTransform = (followProjectile && currentProjectile != null) 
            ? currentProjectile.transform 
            : fallbackTarget;
        
        if (targetTransform == null) return;
        
        // Calcula posição desejada
        Vector3 desiredPosition = targetTransform.position + offset;
        
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
    
    // Método público para forçar seguir um projétil específico
    public void SetProjectile(Projectile projectile)
    {
        currentProjectile = projectile;
    }
    
    // Método para voltar ao fallback
    public void ResetToFallback()
    {
        currentProjectile = null;
    }

    // Método para trocar o fallback target
    public void SetFallbackTarget(Transform newTarget)
    {
        fallbackTarget = newTarget;
    }
}