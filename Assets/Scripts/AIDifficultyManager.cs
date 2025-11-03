using UnityEngine;

public class AIDifficultyManager : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private EnemyShooterAdvancedAI aiController;
    [SerializeField] private bool assignControllerOnAwake = true;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private DifficultyLevel defaultDifficulty = DifficultyLevel.Medium;
    [SerializeField] private bool logDifficultyChanges = true;

    [Header("Difficulty Profiles")]
    [SerializeField] private DifficultyProfile[] difficultyProfiles;

    [Header("Optional Override")]
    [SerializeField] private bool applyOverrideAfterPreset = false;
    [SerializeField] private EnemyShooterAdvancedAI.ShooterSettings overrideSettings;

    private DifficultyLevel currentDifficulty;

    public enum DifficultyLevel
    {
        VeryEasy,
        Easy,
        Medium,
        Hard,
        VeryHard,
        Expert
    }

    [System.Serializable]
    public class DifficultyProfile
    {
        public DifficultyLevel level;
        public string note;
        public EnemyShooterAdvancedAI.ShooterSettings settings;
    }

    [System.Serializable]
    public struct DifficultyModifier
    {
        [Tooltip("Positive values make the AI more accurate, negative values less accurate.")]
        public float accuracyOffset;

        [Tooltip("Multiply miss radius (values < 1 tighten the spread). 0 leaves it unchanged.")]
        public float missRadiusMultiplier;

        [Tooltip("Multiply shooting interval. < 1 shoots faster, > 1 slower. 0 leaves unchanged.")]
        public float shootIntervalMultiplier;

        [Tooltip("Multiply projectile flight speed. 0 leaves unchanged.")]
        public float projectileSpeedMultiplier;

        [Tooltip("Multiply arc height noise. 0 leaves unchanged.")]
        public float heightNoiseMultiplier;
    }

    private void Awake()
    {
        if (assignControllerOnAwake && aiController == null)
        {
            aiController = GetComponent<EnemyShooterAdvancedAI>();
        }
    }

    private void Start()
    {
        if (!applyOnStart || aiController == null)
        {
            return;
        }

        ApplyDifficulty(defaultDifficulty, resetShootTimer: true);

        if (applyOverrideAfterPreset)
        {
            ApplyCustomSettings(overrideSettings, resetShootTimer: false);
        }
    }

    public void ApplyDifficulty(DifficultyLevel level, bool resetShootTimer = true)
    {
        if (!EnsureController())
        {
            return;
        }

        DifficultyProfile profile = GetProfile(level);
        if (profile == null)
        {
            Debug.LogWarning($"AIDifficultyManager: Missing profile for {level}, keeping current settings.");
            return;
        }

        aiController.ApplySettings(profile.settings, resetShootTimer);
        currentDifficulty = level;

        if (logDifficultyChanges)
        {
            Debug.Log($"AIDifficultyManager: Applied '{level}' difficulty to {aiController.name}.");
        }
    }

    public void ApplyCustomSettings(EnemyShooterAdvancedAI.ShooterSettings settings, bool resetShootTimer = true)
    {
        if (!EnsureController())
        {
            return;
        }

        aiController.ApplySettings(settings, resetShootTimer);

        if (logDifficultyChanges)
        {
            Debug.Log($"AIDifficultyManager: Applied custom settings to {aiController.name}.");
        }
    }

    public void BlendDifficulty(DifficultyLevel from, DifficultyLevel to, float blend, bool resetShootTimer = true)
    {
        blend = Mathf.Clamp01(blend);

        DifficultyProfile fromProfile = GetProfile(from);
        DifficultyProfile toProfile = GetProfile(to);

        if (fromProfile == null || toProfile == null)
        {
            Debug.LogWarning("AIDifficultyManager: Unable to blend difficulties because one or both profiles are missing.");
            return;
        }

        EnemyShooterAdvancedAI.ShooterSettings blended = EnemyShooterAdvancedAI.ShooterSettings.Lerp(fromProfile.settings, toProfile.settings, blend);
        ApplyCustomSettings(blended, resetShootTimer);

        currentDifficulty = blend < 0.5f ? from : to;

        if (logDifficultyChanges)
        {
            Debug.Log($"AIDifficultyManager: Blended difficulty from {from} to {to} at {blend:P0}.");
        }
    }

    public void ApplyModifier(DifficultyModifier modifier, bool resetShootTimer = true)
    {
        if (!EnsureController())
        {
            return;
        }

        EnemyShooterAdvancedAI.ShooterSettings settings = aiController.GetCurrentSettings();

        settings.accuracy = Mathf.Clamp01(settings.accuracy + modifier.accuracyOffset);

        float missMultiplier = GetMultiplierOrOne(modifier.missRadiusMultiplier);
        settings.minMissDistance *= missMultiplier;
        settings.maxMissDistance *= missMultiplier;

        float shootMultiplier = GetMultiplierOrOne(modifier.shootIntervalMultiplier);
        settings.shootInterval = Mathf.Max(0.1f, settings.shootInterval * shootMultiplier);

        float speedMultiplier = GetMultiplierOrOne(modifier.projectileSpeedMultiplier);
        settings.projectileSpeed = Mathf.Max(0.1f, settings.projectileSpeed * speedMultiplier);

        float heightMultiplier = GetMultiplierOrOne(modifier.heightNoiseMultiplier);
        settings.heightNoise = Mathf.Clamp01(settings.heightNoise * heightMultiplier);

        ApplyCustomSettings(settings, resetShootTimer);
    }

    public EnemyShooterAdvancedAI.ShooterSettings GetSettingsForDifficulty(DifficultyLevel level)
    {
        DifficultyProfile profile = GetProfile(level);
        if (profile != null)
        {
            return profile.settings;
        }

        return aiController != null ? aiController.GetCurrentSettings() : default;
    }

    public DifficultyLevel GetCurrentDifficulty()
    {
        return currentDifficulty;
    }

    public EnemyShooterAdvancedAI.ShooterSettings GetCurrentRuntimeSettings()
    {
        return aiController != null ? aiController.GetCurrentSettings() : default;
    }

    public void CycleDifficulty(bool resetShootTimer = true)
    {
        DifficultyLevel[] values = (DifficultyLevel[])System.Enum.GetValues(typeof(DifficultyLevel));
        int index = System.Array.IndexOf(values, currentDifficulty);
        int nextIndex = (index + 1) % values.Length;
        ApplyDifficulty(values[nextIndex], resetShootTimer);
    }

    private DifficultyProfile GetProfile(DifficultyLevel level)
    {
        if (difficultyProfiles == null)
        {
            return null;
        }

        for (int i = 0; i < difficultyProfiles.Length; i++)
        {
            DifficultyProfile profile = difficultyProfiles[i];
            if (profile != null && profile.level == level)
            {
                return profile;
            }
        }

        return null;
    }

    private bool EnsureController()
    {
        if (aiController != null)
        {
            return true;
        }

        aiController = GetComponent<EnemyShooterAdvancedAI>();
        if (aiController == null)
        {
            Debug.LogError("AIDifficultyManager: No EnemyShooterAdvancedAI assigned or found on the same GameObject.");
            return false;
        }

        return true;
    }

    private static float GetMultiplierOrOne(float value)
    {
        return value <= 0f ? 1f : value;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        var defaults = new DifficultyProfile[6];
        defaults[0] = CreateProfile(DifficultyLevel.VeryEasy, 0.25f, 2.5f, 4.5f, 3.8f, 0.35f, 8f, 0.55f, 300f, 0.35f);
        defaults[1] = CreateProfile(DifficultyLevel.Easy, 0.45f, 2f, 4f, 3.2f, 0.3f, 9f, 0.5f, 320f, 0.3f);
        defaults[2] = CreateProfile(DifficultyLevel.Medium, 0.65f, 1.6f, 3.2f, 2.6f, 0.28f, 10f, 0.5f, 360f, 0.25f);
        defaults[3] = CreateProfile(DifficultyLevel.Hard, 0.8f, 1.2f, 2.5f, 2f, 0.25f, 11f, 0.45f, 420f, 0.2f);
        defaults[4] = CreateProfile(DifficultyLevel.VeryHard, 0.9f, 1f, 2f, 1.8f, 0.2f, 12f, 0.4f, 460f, 0.15f);
        defaults[5] = CreateProfile(DifficultyLevel.Expert, 0.95f, 0.8f, 1.5f, 1.5f, 0.15f, 13f, 0.35f, 500f, 0.12f);
        difficultyProfiles = defaults;
    }

    private DifficultyProfile CreateProfile(
        DifficultyLevel level,
        float accuracy,
        float minMiss,
        float maxMiss,
        float shootInterval,
        float shootJitter,
        float projectileSpeed,
        float arcHeight,
        float spin,
        float heightNoise)
    {
        return new DifficultyProfile
        {
            level = level,
            settings = new EnemyShooterAdvancedAI.ShooterSettings
            {
                accuracy = accuracy,
                minMissDistance = minMiss,
                maxMissDistance = maxMiss,
                shootInterval = shootInterval,
                shootIntervalJitter = shootJitter,
                projectileSpeed = projectileSpeed,
                projectileArcHeight = arcHeight,
                projectileSpin = spin,
                heightNoise = heightNoise
            }
        };
    }
#endif
}
