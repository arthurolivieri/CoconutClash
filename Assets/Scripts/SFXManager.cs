using UnityEngine;

/// <summary>
/// Manages sound effects (SFX) across the game.
/// This is a singleton that persists across scene loads.
/// </summary>
public class SFXManager : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Sound Effects")]
    [Tooltip("Som quando um projétil causa dano")]
    [SerializeField] private AudioClip damageSound;

    [Tooltip("Som quando um botão é clicado")]
    [SerializeField] private AudioClip buttonSound;

    [Header("Settings")]
    [Tooltip("Volume dos efeitos sonoros (0 a 1)")]
    [SerializeField][Range(0f, 1f)] private float sfxVolume = 0.8f;

    [Tooltip("Se true, os efeitos sonoros serão desativados")]
    [SerializeField] private bool muteSFX = false;

    // Singleton
    private static SFXManager instance;

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

    private void ConfigureAudioSource()
    {
        if (audioSource == null) return;

        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.volume = muteSFX ? 0f : sfxVolume;
        audioSource.spatialBlend = 0f; // 2D sound
    }

    /// <summary>
    /// Toca o som de dano
    /// </summary>
    public void PlayDamageSound()
    {
        PlaySound(damageSound);
    }

    /// <summary>
    /// Toca o som de botão
    /// </summary>
    public void PlayButtonSound()
    {
        PlaySound(buttonSound);
    }

    /// <summary>
    /// Toca um som específico
    /// </summary>
    public void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null || muteSFX) return;

        audioSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Toca um som em uma posição 3D específica
    /// </summary>
    public void PlaySoundAtPosition(AudioClip clip, Vector3 position)
    {
        if (clip == null || muteSFX) return;

        AudioSource.PlayClipAtPoint(clip, position, sfxVolume);
    }

    // Métodos públicos para controle externo

    /// <summary>
    /// Define o volume dos efeitos sonoros
    /// </summary>
    public void SetVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (audioSource != null && !muteSFX)
        {
            audioSource.volume = sfxVolume;
        }
    }

    /// <summary>
    /// Ativa/desativa o mute dos efeitos sonoros
    /// </summary>
    public void SetMute(bool mute)
    {
        muteSFX = mute;
        if (audioSource != null)
        {
            audioSource.volume = mute ? 0f : sfxVolume;
        }
    }

    /// <summary>
    /// Obtém a instância do singleton
    /// </summary>
    public static SFXManager Instance => instance;

    // Context menu para debug no editor
    [ContextMenu("Play Damage Sound")]
    private void DebugPlayDamage()
    {
        PlayDamageSound();
    }

    [ContextMenu("Play Button Sound")]
    private void DebugPlayButton()
    {
        PlayButtonSound();
    }
}

