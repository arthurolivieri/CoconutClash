using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Build Indices")]
    [Tooltip("Índice da cena do menu principal no Build Settings.")]
    [SerializeField] private int menuSceneIndex = 0;

    [Tooltip("Índice da primeira fase (fase atual).")]
    [SerializeField] private int firstLevelSceneIndex = 1;

    [Tooltip("Índice da segunda fase.")]
    [SerializeField] private int secondLevelSceneIndex = 4;

    [Tooltip("Índice da VictoryScene2 (após vencer a fase 2).")]
    [SerializeField] private int victoryScene2Index = 5;

    [Tooltip("Índice da terceira fase.")]
    [SerializeField] private int thirdLevelSceneIndex = 6;

    [Tooltip("Índice da FinalVictoryScene (após vencer a fase 3).")]
    [SerializeField] private int finalVictorySceneIndex = 7;

    // --------- MENU PRINCIPAL ---------
    public void PlayGame()
    {
        PlayButtonSound();
        SceneManager.LoadScene(firstLevelSceneIndex);
    }

    public void QuitGame()
    {
        PlayButtonSound();
        Debug.Log("[MenuManager] Quit Game");
        Application.Quit();
    }

    // --------- GAME OVER SCENE (3) ---------
    public void GoToMainMenu()
    {
        PlayButtonSound();
        SceneManager.LoadScene(menuSceneIndex);
    }

    public void RestartFirstLevel()
    {
        PlayButtonSound();
        SceneManager.LoadScene(firstLevelSceneIndex);
    }

    // --------- VICTORY SCENE 1 ---------
    public void GoToSecondLevel()
    {
        PlayButtonSound();
        SceneManager.LoadScene(secondLevelSceneIndex);
    }

    // --------- VICTORY SCENE 2 ---------
    public void GoToVictoryScene2()
    {
        PlayButtonSound();
        SceneManager.LoadScene(victoryScene2Index);
    }

    // --------- VICTORY SCENE 2 → LEVEL 3 ---------
    public void GoToThirdLevel()
    {
        PlayButtonSound();
        SceneManager.LoadScene(thirdLevelSceneIndex);
    }

    // --------- FINAL VICTORY SCENE ---------
    public void GoToMainMenuFromFinal()
    {
        PlayButtonSound();
        SceneManager.LoadScene(menuSceneIndex);
    }

    // --------- GAME OVER (can restart from any level) ---------
    public void RestartCurrentLevel()
    {
        PlayButtonSound();
        // Try to detect which level we came from and restart it
        // For now, just restart Level 1 - you can enhance this later
        SceneManager.LoadScene(firstLevelSceneIndex);
    }

    // --------- HELPER: Button Sound ---------
    private void PlayButtonSound()
    {
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayButtonSound();
        }
    }
}
