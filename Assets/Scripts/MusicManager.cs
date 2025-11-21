using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Manages background music across all scenes in the game.
/// This is a singleton that persists across scene loads.
/// </summary>
public class MusicManager : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Main Menu Music")]
    [Tooltip("Música para o menu principal")]
    [SerializeField] private AudioClip mainMenuMusic;

    [Header("Game Over Music")]
    [Tooltip("Música para a tela de Game Over")]
    [SerializeField] private AudioClip gameOverMusic;

    [Header("Level Music")]
    [Tooltip("Músicas para as fases do jogo (serão tocadas em ordem ou aleatoriamente)")]
    [SerializeField] private AudioClip[] levelMusic;

    [Header("Settings")]
    [Tooltip("Volume da música (0 a 1)")]
    [SerializeField][Range(0f, 1f)] private float musicVolume = 0.7f;

    [Tooltip("Se true, as músicas de fase serão tocadas em ordem sequencial. Se false, serão escolhidas aleatoriamente.")]
    [SerializeField] private bool playLevelMusicInOrder = true;

    [Tooltip("Duração do fade in/out quando trocar de música (em segundos)")]
    [SerializeField] private float fadeTransitionDuration = 1.5f;

    [Tooltip("Se true, a música será desativada. Útil para debug.")]
    [SerializeField] private bool muteMusic = false;

    // Singleton
    private static MusicManager instance;
    
    // Track current level for sequential music playback
    private int currentLevelMusicIndex = 0;
    private string currentSceneName = "";
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        // Implementação do Singleton
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Validar ou criar AudioSource
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            
            ConfigureAudioSource();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Inscrever-se no evento de mudança de cena
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Tocar música da cena inicial
        PlayMusicForCurrentScene();
    }

    private void OnDestroy()
    {
        // Desinscrever-se do evento
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null) return;
        
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = muteMusic ? 0f : musicVolume;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Tocar música apropriada quando uma nova cena carregar
        PlayMusicForCurrentScene();
    }

    private void PlayMusicForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        
        // Evitar tocar a mesma música novamente se já estiver tocando
        if (sceneName == currentSceneName && audioSource.isPlaying)
        {
            return;
        }
        
        currentSceneName = sceneName;
        AudioClip clipToPlay = null;

        // Determinar qual música tocar baseado no nome da cena
        if (IsMainMenuScene(sceneName))
        {
            clipToPlay = mainMenuMusic;
            Debug.Log($"[MusicManager] Playing Main Menu music for scene: {sceneName}");
        }
        else if (IsGameOverScene(sceneName))
        {
            clipToPlay = gameOverMusic;
            Debug.Log($"[MusicManager] Playing Game Over music for scene: {sceneName}");
        }
        else if (IsLevelScene(sceneName))
        {
            clipToPlay = GetLevelMusic(sceneName);
            Debug.Log($"[MusicManager] Playing Level music for scene: {sceneName}");
        }
        else if (IsVictoryScene(sceneName))
        {
            // Victory scenes can continue playing level music or fade out
            // For now, we'll keep the current music playing
            Debug.Log($"[MusicManager] Victory scene detected, keeping current music: {sceneName}");
            return;
        }

        // Tocar a música com fade
        if (clipToPlay != null)
        {
            PlayMusicWithFade(clipToPlay);
        }
        else
        {
            Debug.LogWarning($"[MusicManager] No music clip assigned for scene: {sceneName}");
        }
    }

    private bool IsMainMenuScene(string sceneName)
    {
        // Detectar cenas de menu principal
        return sceneName.ToLower().Contains("menu") || 
               sceneName.ToLower().Contains("mainmenu") ||
               sceneName == "MainMenu";
    }

    private bool IsGameOverScene(string sceneName)
    {
        // Detectar cenas de game over
        return sceneName.ToLower().Contains("gameover") || 
               sceneName == "GameOver";
    }

    private bool IsLevelScene(string sceneName)
    {
        // Detectar cenas de level/fase
        return sceneName.ToLower().Contains("level") || 
               sceneName.Contains("Level1") || 
               sceneName.Contains("Level2") || 
               sceneName.Contains("Level3");
    }

    private bool IsVictoryScene(string sceneName)
    {
        // Detectar cenas de vitória
        return sceneName.ToLower().Contains("victory") || 
               sceneName.ToLower().Contains("win");
    }

    private AudioClip GetLevelMusic(string sceneName)
    {
        if (levelMusic == null || levelMusic.Length == 0)
        {
            Debug.LogWarning("[MusicManager] No level music clips assigned!");
            return null;
        }

        if (playLevelMusicInOrder)
        {
            // Determinar índice baseado no level
            if (sceneName.Contains("Level1"))
            {
                currentLevelMusicIndex = 0;
            }
            else if (sceneName.Contains("Level2"))
            {
                currentLevelMusicIndex = 1;
            }
            else if (sceneName.Contains("Level3"))
            {
                currentLevelMusicIndex = 2;
            }
            
            // Garantir que o índice está dentro dos limites
            currentLevelMusicIndex = Mathf.Clamp(currentLevelMusicIndex, 0, levelMusic.Length - 1);
            return levelMusic[currentLevelMusicIndex];
        }
        else
        {
            // Escolher música aleatória
            int randomIndex = Random.Range(0, levelMusic.Length);
            return levelMusic[randomIndex];
        }
    }

    private void PlayMusicWithFade(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;

        // Se já há um fade acontecendo, pará-lo
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeToNewTrack(clip));
    }

    private IEnumerator FadeToNewTrack(AudioClip newClip)
    {
        float targetVolume = muteMusic ? 0f : musicVolume;

        // Fade out da música atual
        if (audioSource.isPlaying)
        {
            float startVolume = audioSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeTransitionDuration / 2f)
            {
                elapsed += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (fadeTransitionDuration / 2f));
                yield return null;
            }

            audioSource.Stop();
        }

        // Trocar o clip e começar a tocar
        audioSource.clip = newClip;
        audioSource.Play();

        // Fade in da nova música
        float fadeInElapsed = 0f;
        while (fadeInElapsed < fadeTransitionDuration / 2f)
        {
            fadeInElapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, targetVolume, fadeInElapsed / (fadeTransitionDuration / 2f));
            yield return null;
        }

        audioSource.volume = targetVolume;
        fadeCoroutine = null;
    }

    // Métodos públicos para controle externo

    /// <summary>
    /// Define o volume da música
    /// </summary>
    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (audioSource != null && !muteMusic)
        {
            audioSource.volume = musicVolume;
        }
    }

    /// <summary>
    /// Ativa/desativa o mute da música
    /// </summary>
    public void SetMute(bool mute)
    {
        muteMusic = mute;
        if (audioSource != null)
        {
            audioSource.volume = mute ? 0f : musicVolume;
        }
    }

    /// <summary>
    /// Pausa a música
    /// </summary>
    public void PauseMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }

    /// <summary>
    /// Resume a música
    /// </summary>
    public void ResumeMusic()
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.UnPause();
        }
    }

    /// <summary>
    /// Para completamente a música
    /// </summary>
    public void StopMusic()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Obtém a instância do singleton
    /// </summary>
    public static MusicManager Instance => instance;

    // Context menu para debug no editor
    [ContextMenu("Play Main Menu Music")]
    private void DebugPlayMainMenu()
    {
        if (mainMenuMusic != null)
            PlayMusicWithFade(mainMenuMusic);
    }

    [ContextMenu("Play Game Over Music")]
    private void DebugPlayGameOver()
    {
        if (gameOverMusic != null)
            PlayMusicWithFade(gameOverMusic);
    }

    [ContextMenu("Play Random Level Music")]
    private void DebugPlayRandomLevel()
    {
        if (levelMusic != null && levelMusic.Length > 0)
        {
            int randomIndex = Random.Range(0, levelMusic.Length);
            PlayMusicWithFade(levelMusic[randomIndex]);
        }
    }
}

