using System;
using UnityEngine;

/// <summary>
/// Basic health component that can be reused by both players and enemies.
/// Tracks hit points, fires events upon damage/death and knows to which team the actor belongs.
/// </summary>
public class Health : MonoBehaviour
{
    public enum Team
    {
        Neutral,
        Player,
        Enemy
    }

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool resetHealthOnEnable = true;

    [Header("Death Handling")]
    [SerializeField] private Team ownerTeam = Team.Neutral;
    [SerializeField] private bool disableGameObjectOnDeath = true;
    [SerializeField] private bool destroyGameObjectOnDeath = false;

    public event Action<float, float> HealthChanged; // current, max
    public event Action<float> Damaged;
    public event Action<Health> Died;

    private float currentHealth;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public Team OwnerTeam => ownerTeam;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        if (!resetHealthOnEnable)
        {
            currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        }
    }

    private void OnEnable()
    {
        if (resetHealthOnEnable || currentHealth <= 0f)
        {
            ResetHealth();
        }
    }

    public void ResetHealth()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        isDead = false;
        currentHealth = maxHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damageAmount, Team attackerTeam = Team.Neutral, GameObject damageSource = null)
    {
        if (isDead) return;
        if (damageAmount <= 0f) return;
        if (!CanTakeDamageFrom(attackerTeam)) return;

        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);
        Damaged?.Invoke(damageAmount);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        // Play damage sound effect
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayDamageSound();
        }

        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
    }

    public void Heal(float healAmount)
    {
        if (healAmount <= 0f || isDead) return;
        float prev = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + healAmount, 0f, maxHealth);
        if (!Mathf.Approximately(prev, currentHealth))
        {
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    private bool CanTakeDamageFrom(Team attackerTeam)
    {
        if (ownerTeam == Team.Neutral || attackerTeam == Team.Neutral) return true;
        return ownerTeam != attackerTeam;
    }

    private void HandleDeath()
    {
        if (isDead) return;

        isDead = true;
        Died?.Invoke(this);

        if (destroyGameObjectOnDeath)
        {
            Destroy(gameObject);
        }
        else if (disableGameObjectOnDeath)
        {
            gameObject.SetActive(false);
        }
    }
}
