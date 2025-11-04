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
    [SerializeField] private float enemyTurnDelay = 1f; // Delay antes do AI atirar
    [SerializeField] private float turnTransitionDelay = 0.5f; // Delay entre turnos

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI currentPlayerText; // Texto mostrando de quem é o turno
    [SerializeField] private Button endTurnButton; // Botão para pular turno (opcional)

    [Header("Game Settings")]
    [SerializeField] private bool autoStartGame = true;
    [SerializeField] private bool allowTurnSkip = false;

    public enum TurnState
    {
        PlayerTurn,
        EnemyTurn,
        TurnTransition,
        GameOver
    }

    [SerializeField] private TurnState currentTurnState = TurnState.PlayerTurn;
    private bool gameStarted = false;

    // Events
    public System.Action<TurnState> OnTurnChanged;
    public System.Action OnGameStarted;
    public System.Action OnGameEnded;

    private void Awake()
    {
        // Validar referências
        if (playerShooter == null)
            playerShooter = FindObjectOfType<Shooter>();
        
        if (enemyShooter == null)
            enemyShooter = FindObjectOfType<EnemyShooterAdvancedAI>();
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
        }
    }

    private void OnDisable()
    {
        // Desinscrever dos eventos
        if (playerShooter != null)
        {
            playerShooter.ManualShotFired -= OnPlayerShotFired;
        }
    }

    public void StartGame()
    {
        if (gameStarted) return;

        Debug.Log("[TurnBasedGameManager] Starting turn-based game!");
        
        gameStarted = true;
        
        // Configurar modo de turnos
        if (playerShooter != null)
        {
            playerShooter.SetRestrictManualShootingToTurn(true);
        }
        
        if (enemyShooter != null)
        {
            enemyShooter.SetTurnBasedControl(true);
        }

        // Começar com o turno do jogador
        StartPlayerTurn();
        
        OnGameStarted?.Invoke();
    }

    public void EndGame()
    {
        if (!gameStarted) return;

        Debug.Log("[TurnBasedGameManager] Game ended!");
        
        gameStarted = false;
        currentTurnState = TurnState.GameOver;
        
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
        
        // Habilitar tiro do jogador
        if (playerShooter != null)
        {
            playerShooter.SetManualTurnEnabled(true);
        }
        
        UpdateUI();
        OnTurnChanged?.Invoke(currentTurnState);
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
        
        UpdateUI();
        OnTurnChanged?.Invoke(currentTurnState);
        
        // Executar turno do inimigo após delay
        StartCoroutine(ExecuteEnemyTurnWithDelay());
    }

    private IEnumerator ExecuteEnemyTurnWithDelay()
    {
        yield return new WaitForSeconds(enemyTurnDelay);
        
        if (enemyShooter != null && enemyShooter.CanExecuteTurnShot())
        {
            Debug.Log("[TurnBasedGameManager] Enemy executing shot!");
            enemyShooter.ExecuteTurnShot();
        }
        else
        {
            Debug.LogWarning("[TurnBasedGameManager] Enemy cannot execute shot!");
        }
        
        // Após o tiro do inimigo, voltar para o jogador
        yield return new WaitForSeconds(turnTransitionDelay);
        StartPlayerTurn();
    }

    private void OnPlayerShotFired()
    {
        if (currentTurnState != TurnState.PlayerTurn) return;
        
        Debug.Log("[TurnBasedGameManager] Player shot fired! Switching to enemy turn.");
        
        // Pequeno delay antes de mudar para o turno do inimigo
        StartCoroutine(TransitionToEnemyTurn());
    }

    private IEnumerator TransitionToEnemyTurn()
    {
        currentTurnState = TurnState.TurnTransition;
        UpdateUI();
        
        yield return new WaitForSeconds(turnTransitionDelay);
        StartEnemyTurn();
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
                currentPlayerText.color = Color.green;
                break;
            case TurnState.EnemyTurn:
                currentPlayerText.text = "ENEMY TURN";
                currentPlayerText.color = Color.red;
                break;
            case TurnState.TurnTransition:
                currentPlayerText.text = "...";
                currentPlayerText.color = Color.yellow;
                break;
            case TurnState.GameOver:
                currentPlayerText.text = "GAME OVER";
                currentPlayerText.color = Color.gray;
                break;
        }
    }

    // Métodos públicos para controle externo
    public TurnState GetCurrentTurnState() => currentTurnState;
    public bool IsGameActive() => gameStarted && currentTurnState != TurnState.GameOver;
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
        if (turnTransitionDelay < 0f) turnTransitionDelay = 0f;
    }
}