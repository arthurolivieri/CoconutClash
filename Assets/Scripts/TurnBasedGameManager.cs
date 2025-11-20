using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class TurnBasedGameManager : MonoBehaviour
{
    [Header("Player and Enemy References")]
    [SerializeField] private Shooter playerShooter;
    [SerializeField] private EnemyShooterAdvancedAI enemyShooter;

    [Header("Turn Settings")]
    [SerializeField] private float enemyTurnDelay = 1f; // Delay inicial quando turno do inimigo começa
    [SerializeField] private float enemyShootDelayAfterStandUp = 0.5f; // Delay após ficar em pé antes de atirar
    [SerializeField] private float turnTransitionDelay = 0.5f; // Delay entre turnos

	[Header("Stand Up Settings")]
	[SerializeField] private float playerStandUpDuration = 0.25f;
	[SerializeField] private float enemyStandUpDuration = 0.25f;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI currentPlayerText; // Texto mostrando de quem é o turno
    [SerializeField] private Button endTurnButton; // Botão para pular turno (opcional)

    [Header("Game Settings")]
    [SerializeField] private bool autoStartGame = true;
    [SerializeField] private bool allowTurnSkip = false;
    
    [Header("Health Settings")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Health[] enemyHealths;

    [Header("Camera")]
    [SerializeField] private ProjectileCamera projectileCamera;

    public enum TurnState
    {
        PlayerTurn,
        EnemyTurn,
        TurnTransition,
        GameOver,
        StageCleared
    }

    [SerializeField] private TurnState currentTurnState = TurnState.PlayerTurn;
    private bool gameStarted = false;
    private bool playerWasDefeated = false;
    private int remainingEnemies = 0;
    
    // Projectile tracking
    private Projectile currentProjectile;
    private bool waitingForProjectileDestruction = false;

    // Events
    public System.Action<TurnState> OnTurnChanged;
    public System.Action OnGameStarted;
    public System.Action OnGameEnded;
    public System.Action OnStageCleared;
    public System.Action OnPlayerDefeated;

    private void Awake()
    {
        // Validar referências
        if (playerShooter == null)
            playerShooter = FindObjectOfType<Shooter>();
        
        if (enemyShooter == null)
            enemyShooter = FindObjectOfType<EnemyShooterAdvancedAI>();

        AssignDefaultHealthReferences();
    }

    private void Start()
    {
        SetupUI();
        
        if (autoStartGame)
        {
            StartGame();
        }
    }

    private void SetupUI()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(EndCurrentTurn);
            endTurnButton.gameObject.SetActive(allowTurnSkip);
        }
    }

    private void OnEnable()
    {
        // Subscrever aos eventos do jogador
        if (playerShooter != null)
        {
            playerShooter.ManualShotFired += OnPlayerShotFired;
            playerShooter.ProjectileCreated += OnProjectileCreated;
        }
        
        // Subscrever aos eventos do inimigo
        if (enemyShooter != null)
        {
            enemyShooter.ProjectileCreated += OnProjectileCreated;
        }

        SubscribeToHealthEvents();
    }

    private void OnDisable()
    {
        // Desinscrever dos eventos
        if (playerShooter != null)
        {
            playerShooter.ManualShotFired -= OnPlayerShotFired;
            playerShooter.ProjectileCreated -= OnProjectileCreated;
        }
        
        if (enemyShooter != null)
        {
            enemyShooter.ProjectileCreated -= OnProjectileCreated;
        }
        
        // Limpar referência do projétil
        UntrackCurrentProjectile();

        UnsubscribeFromHealthEvents();
    }

    public void StartGame()
    {
        if (gameStarted) return;

        Debug.Log("[TurnBasedGameManager] Starting turn-based game!");
        
        gameStarted = true;
        playerWasDefeated = false;
        
        // Configurar modo de turnos
        if (playerShooter != null)
        {
            playerShooter.SetRestrictManualShootingToTurn(true);
        }
        
        if (enemyShooter != null)
        {
            enemyShooter.SetTurnBasedControl(true);
        }

        ResetHealthPools();
        remainingEnemies = CountAliveEnemies();

        // Começar com o turno do jogador
        StartPlayerTurn();
        
        OnGameStarted?.Invoke();
    }

    public void EndGame(TurnState endState = TurnState.GameOver)
    {
        if (!gameStarted) return;

        Debug.Log("[TurnBasedGameManager] Game ended!");
        
        gameStarted = false;
        currentTurnState = endState;
        
        // Desabilitar controles
        if (playerShooter != null)
        {
            playerShooter.SetManualTurnEnabled(false);
        }
        
        UpdateUI();
        OnGameEnded?.Invoke();
    }

	private void StartPlayerTurn()
    {
        if (!gameStarted) return;

        Debug.Log("[TurnBasedGameManager] Player turn started!");
        
        currentTurnState = TurnState.PlayerTurn;
        
		// Desabilitar tiro do jogador até ficar em pé
		if (playerShooter != null) playerShooter.SetManualTurnEnabled(false);
        
        // Focar câmera no player
        if (projectileCamera != null)
        {
            projectileCamera.SetFallbackTarget(playerShooter.transform);
            projectileCamera.ResetToFallback();
        }
        
        UpdateUI();
        OnTurnChanged?.Invoke(currentTurnState);

		// Fluxo: levantar e só depois habilitar a mira/tiro
		StartCoroutine(BeginPlayerTurnFlow());
    }

    private void StartEnemyTurn()
    {
        if (!gameStarted) return;

        Debug.Log("[TurnBasedGameManager] Enemy turn started!");
        
        currentTurnState = TurnState.EnemyTurn;
        
        // Desabilitar tiro do jogador
        if (playerShooter != null)
        {
            playerShooter.SetManualTurnEnabled(false);
        }
        
        // Focar câmera no inimigo
        if (projectileCamera != null)
        {
            projectileCamera.SetFallbackTarget(enemyShooter.transform);
            projectileCamera.ResetToFallback();
        }
        
        UpdateUI();
        OnTurnChanged?.Invoke(currentTurnState);
        
        // Executar turno do inimigo após delay
        StartCoroutine(ExecuteEnemyTurnWithDelay());
    }

    private IEnumerator ExecuteEnemyTurnWithDelay()
    {
        yield return new WaitForSeconds(enemyTurnDelay);
		
		// Garantir que o inimigo fique em pé antes de atirar
		yield return StandActorUprightIfPossible(enemyShooter ? enemyShooter.transform : null, enemyStandUpDuration);

        // Delay adicional após ficar em pé antes de atirar
        if (enemyShootDelayAfterStandUp > 0f)
        {
            Debug.Log($"[TurnBasedGameManager] Enemy standing ready, waiting {enemyShootDelayAfterStandUp}s before shooting...");
            yield return new WaitForSeconds(enemyShootDelayAfterStandUp);
        }

        if (enemyShooter != null && enemyShooter.CanExecuteTurnShot())
        {
            Debug.Log("[TurnBasedGameManager] Enemy executing shot!");
            enemyShooter.ExecuteTurnShot();
        }
        else
        {
            Debug.LogWarning("[TurnBasedGameManager] Enemy cannot execute shot!");
            // Se o inimigo não pode atirar, voltar para o jogador após delay
            yield return new WaitForSeconds(turnTransitionDelay);
            StartPlayerTurn();
        }
        
        // Não chamamos StartPlayerTurn() aqui mais, pois isso acontecerá
        // quando o projétil do inimigo for destruído (via OnCurrentProjectileDestroyed)
    }

	private IEnumerator BeginPlayerTurnFlow()
	{
		// Fica em pé primeiro
		yield return StandActorUprightIfPossible(playerShooter ? playerShooter.transform : null, playerStandUpDuration);
		
		// Agora habilita o tiro manual
		if (playerShooter != null)
		{
			playerShooter.SetManualTurnEnabled(true);
		}
	}

	private IEnumerator StandActorUprightIfPossible(Transform actorTransform, float duration)
	{
		if (actorTransform == null || duration <= 0f)
		{
			yield break;
		}

		var stand = actorTransform.GetComponentInParent<StandUprightController>();
		if (stand == null)
		{
			stand = actorTransform.gameObject.AddComponent<StandUprightController>();
		}
		yield return stand.StandUprightRoutine(duration);
	}

    private void OnProjectileCreated(Projectile projectile)
    {
        if (projectile == null) return;
        
        Debug.Log("[TurnBasedGameManager] Projectile created, tracking for turn end.");
        
        // Untrack previous projectile if any
        UntrackCurrentProjectile();
        
        // Track new projectile
        currentProjectile = projectile;
        waitingForProjectileDestruction = true;
        currentProjectile.OnProjectileDestroyed += OnCurrentProjectileDestroyed;
    }

    private void UntrackCurrentProjectile()
    {
        if (currentProjectile != null)
        {
            currentProjectile.OnProjectileDestroyed -= OnCurrentProjectileDestroyed;
            currentProjectile = null;
        }
        waitingForProjectileDestruction = false;
    }

    private void OnCurrentProjectileDestroyed()
    {
        Debug.Log("[TurnBasedGameManager] Projectile destroyed!");
        
        UntrackCurrentProjectile();
        
        // If we were waiting for the projectile to end the turn, do it now
        if (currentTurnState == TurnState.PlayerTurn)
        {
            StartCoroutine(TransitionToEnemyTurn());
        }
        else if (currentTurnState == TurnState.EnemyTurn)
        {
            StartCoroutine(TransitionToPlayerTurn());
        }
    }

    private void OnPlayerShotFired()
    {
        if (currentTurnState != TurnState.PlayerTurn) return;
        
        Debug.Log("[TurnBasedGameManager] Player shot fired! Waiting for projectile to disappear...");
        
        // Disable shooting immediately to prevent multiple shots in the same turn
        if (playerShooter != null)
        {
            playerShooter.SetManualTurnEnabled(false);
        }
        
        // Turn will transition when the projectile is destroyed (handled by OnCurrentProjectileDestroyed)
    }

    private IEnumerator TransitionToEnemyTurn()
    {
        currentTurnState = TurnState.TurnTransition;
        UpdateUI();
        
        yield return new WaitForSeconds(turnTransitionDelay);
        StartEnemyTurn();
    }

    private IEnumerator TransitionToPlayerTurn()
    {
        currentTurnState = TurnState.TurnTransition;
        UpdateUI();
        
        yield return new WaitForSeconds(turnTransitionDelay);
        StartPlayerTurn();
    }

    public void EndCurrentTurn()
    {
        if (!allowTurnSkip || currentTurnState != TurnState.PlayerTurn) return;
        
        Debug.Log("[TurnBasedGameManager] Player skipped turn!");
        StartCoroutine(TransitionToEnemyTurn());
    }

    private void UpdateUI()
    {
        if (currentPlayerText == null) return;
        
        switch (currentTurnState)
        {
            case TurnState.PlayerTurn:
                currentPlayerText.text = "YOUR TURN - Click to shoot!";
                currentPlayerText.color = Color.black;
                break;
            case TurnState.EnemyTurn:
                currentPlayerText.text = "ENEMY TURN";
                currentPlayerText.color = Color.black;
                break;
            case TurnState.TurnTransition:
                currentPlayerText.text = "...";
                currentPlayerText.color = Color.black;
                break;
            case TurnState.GameOver:
                currentPlayerText.text = "GAME OVER";
                currentPlayerText.color = Color.black;
                break;
            case TurnState.StageCleared:
                currentPlayerText.text = "STAGE CLEARED!";
                currentPlayerText.color = Color.black;
                break;
        }
    }

    // Métodos públicos para controle externo
    public TurnState GetCurrentTurnState() => currentTurnState;
    public bool IsGameActive() => gameStarted && currentTurnState != TurnState.GameOver && currentTurnState != TurnState.StageCleared;
    public bool IsPlayerTurn() => currentTurnState == TurnState.PlayerTurn;
    public bool IsEnemyTurn() => currentTurnState == TurnState.EnemyTurn;

    // Para debug
    [ContextMenu("Force Start Game")]
    public void ForceStartGame() => StartGame();
    
    [ContextMenu("Force End Game")]
    public void ForceEndGame() => EndGame();
    
    [ContextMenu("Force Player Turn")]
    public void ForcePlayerTurn() => StartPlayerTurn();
    
    [ContextMenu("Force Enemy Turn")]
    public void ForceEnemyTurn() => StartEnemyTurn();

    private void OnValidate()
    {
        // Validações no editor
        if (enemyTurnDelay < 0f) enemyTurnDelay = 0f;
        if (enemyShootDelayAfterStandUp < 0f) enemyShootDelayAfterStandUp = 0f;
        if (turnTransitionDelay < 0f) turnTransitionDelay = 0f;
        if (playerStandUpDuration < 0f) playerStandUpDuration = 0f;
        if (enemyStandUpDuration < 0f) enemyStandUpDuration = 0f;
    }

    private void AssignDefaultHealthReferences()
    {
        if (playerHealth == null && playerShooter != null)
        {
            playerHealth = playerShooter.GetComponentInParent<Health>();
        }

        bool hasEnemyHealth = enemyHealths != null && enemyHealths.Length > 0;
        if (!hasEnemyHealth && enemyShooter != null)
        {
            var enemyHealth = enemyShooter.GetComponentInParent<Health>();
            if (enemyHealth != null)
            {
                enemyHealths = new[] { enemyHealth };
            }
        }

        remainingEnemies = CountAliveEnemies();
    }

    private void SubscribeToHealthEvents()
    {
        if (playerHealth != null)
        {
            playerHealth.Died += OnPlayerHealthDepleted;
        }

        if (enemyHealths == null) return;

        foreach (var enemy in enemyHealths)
        {
            if (enemy == null) continue;
            enemy.Died += OnEnemyHealthDepleted;
        }
    }

    private void UnsubscribeFromHealthEvents()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerHealthDepleted;
        }

        if (enemyHealths == null) return;

        foreach (var enemy in enemyHealths)
        {
            if (enemy == null) continue;
            enemy.Died -= OnEnemyHealthDepleted;
        }
    }

    private void ResetHealthPools()
    {
        if (playerHealth != null)
        {
            playerHealth.ResetHealth();
        }

        if (enemyHealths == null) return;

        foreach (var enemy in enemyHealths)
        {
            if (enemy == null) continue;
            enemy.ResetHealth();
        }
    }

    private int CountAliveEnemies()
    {
        if (enemyHealths == null || enemyHealths.Length == 0) return 0;

        int alive = 0;
        foreach (var enemy in enemyHealths)
        {
            if (enemy == null) continue;
            if (!enemy.IsDead) alive++;
        }
        return alive;
    }

    private void OnPlayerHealthDepleted(Health _)
    {
        if (playerWasDefeated) return;
        playerWasDefeated = true;
        HandlePlayerDefeat();
    }

    private void OnEnemyHealthDepleted(Health enemy)
    {
        remainingEnemies = Mathf.Max(0, remainingEnemies - 1);
        if (remainingEnemies <= 0)
        {
            HandleStageCleared();
        }
    }

    private void HandleStageCleared()
    {
        if (!gameStarted) return;

        Debug.Log("[TurnBasedGameManager] Stage cleared! All enemies defeated.");
        EndGame(TurnState.StageCleared);
        OnStageCleared?.Invoke();
    }

    private void HandlePlayerDefeat()
    {
        if (!gameStarted) return;

        Debug.Log("[TurnBasedGameManager] Player defeated!");
        EndGame(TurnState.GameOver);
        OnPlayerDefeated?.Invoke();
    }
}
