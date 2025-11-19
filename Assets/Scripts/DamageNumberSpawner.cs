using UnityEngine;

/// <summary>
/// Listens to a Health component and spawns floating damage numbers whenever it takes damage.
/// </summary>
public class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector2 randomJitter = new Vector2(0.25f, 0.15f);
    [SerializeField] private Color playerDamageColor = new Color(0.95f, 0.35f, 0.35f);
    [SerializeField] private Color enemyDamageColor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private Color neutralDamageColor = new Color(1f, 1f, 1f);
    [SerializeField] private int sortingOrder = 450;

    private void Awake()
    {
        if (health == null)
            health = GetComponentInParent<Health>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null)
            health.Damaged -= OnDamaged;
    }

    private void OnDamaged(float amount)
    {
        if (amount <= 0f) return;

        Vector3 jitter = new Vector3(
            Random.Range(-randomJitter.x, randomJitter.x),
            Random.Range(0f, randomJitter.y),
            0f
        );

        Vector3 position = transform.position + spawnOffset + jitter;
        Color color = GetColorForTeam(health != null ? health.OwnerTeam : Health.Team.Neutral);

        DamageNumberPopup.Spawn(amount, position, color, sortingOrder);
    }

    private Color GetColorForTeam(Health.Team team)
    {
        switch (team)
        {
            case Health.Team.Player:
                return playerDamageColor;
            case Health.Team.Enemy:
                return enemyDamageColor;
            default:
                return neutralDamageColor;
        }
    }
}
