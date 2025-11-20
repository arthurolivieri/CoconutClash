using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Build Indices")]
    [Tooltip("Índice da cena do menu principal no Build Settings.")]
    [SerializeField] private int menuSceneIndex = 0;

    [Tooltip("Índice da primeira fase (fase atual).")]
    [SerializeField] private int firstLevelSceneIndex = 1;

    [Tooltip("Índice da segunda fase (para onde a VictoryScene deve mandar).")]
    [SerializeField] private int secondLevelSceneIndex = 4;

    // --------- MENU PRINCIPAL ---------
    // Botão "Play" no menu principal -> começa a fase 1
    public void PlayGame()
    {
        SceneManager.LoadScene(firstLevelSceneIndex);
    }

    // Botão "Quit" (opcional, se quiser sair do jogo)
    public void QuitGame()
    {
        Debug.Log("[MenuManager] Quit Game");
        Application.Quit();
    }

    // --------- GAME OVER SCENE (3) ---------
    // Botão "Voltar para o menu principal"
    public void GoToMainMenu()
    {
        SceneManager.LoadScene(menuSceneIndex);
    }

    // Botão "Tentar de novo / Voltar para a fase"
    public void RestartFirstLevel()
    {
        SceneManager.LoadScene(firstLevelSceneIndex);
    }

    // --------- VICTORY SCENE (2) ---------
    // Botão "Próxima fase" na VictoryScene -> vai para a fase 2
    public void GoToSecondLevel()
    {
        SceneManager.LoadScene(secondLevelSceneIndex);
    }
}
