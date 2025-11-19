using UnityEngine;

/// <summary>
/// Simple 2D-style health bar that spawns a bar above a character and tracks a Health component.
/// Works entirely with SpriteRenderers so it fits pixel-art scenes without a Canvas.
/// </summary>
[ExecuteAlways]
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Vector3 barOffset = new Vector3(0f, 1.25f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(1.2f, 0.15f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Gradient fillGradient = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(new Color(0.85f, 0.18f, 0.18f), 0f),
            new GradientColorKey(new Color(0.96f, 0.75f, 0.2f), 0.5f),
            new GradientColorKey(new Color(0.2f, 0.8f, 0.3f), 1f)
        },
        alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
    };
    [SerializeField] private bool hideWhenFull = false;
    [SerializeField] private int sortingOrder = 350;
    [SerializeField] private string sortingLayerName = "Default";

    [SerializeField, HideInInspector] private Transform barRoot;
    [SerializeField, HideInInspector] private SpriteRenderer backgroundRenderer;
    [SerializeField, HideInInspector] private SpriteRenderer fillRenderer;
    private float currentNormalized = 1f;

    private static Sprite pixelSprite;
    private const string GeneratedRootName = "__WorldHealthBar";
    private const string GeneratedBackgroundName = "__WHB_Background";
    private const string GeneratedFillName = "__WHB_Fill";
    private static readonly string[] LegacyRootNames = { "HealthBar" };
    private static readonly string[] LegacyBackgroundNames = { GeneratedBackgroundName, "Background" };
    private static readonly string[] LegacyFillNames = { GeneratedFillName, "Fill" };

    private void Awake()
    {
        if (health == null)
            health = GetComponentInParent<Health>();

        EnsureVisuals();
        UpdateBarImmediately();
        ApplyVisibility();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.HealthChanged += OnHealthChanged;
            health.Died += OnHealthDied;
        }

        UpdateBarImmediately();
        ApplyVisibility();
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
            health.Died -= OnHealthDied;
        }
    }

    private void LateUpdate()
    {
        if (barRoot == null) return;

        barRoot.localPosition = barOffset;
        barRoot.localRotation = Quaternion.identity;
    }

    private void OnValidate()
    {
        if (barSize.x < 0.1f) barSize.x = 0.1f;
        if (barSize.y < 0.02f) barSize.y = 0.02f;
        if (sortingOrder < 0) sortingOrder = 0;

        if (!Application.isPlaying)
        {
            EnsureVisuals();
            UpdateBarImmediately();
            ApplyVisibility();
        }
    }

    private void EnsureVisuals()
    {
        if (!CanModifyHierarchy())
            return;

        EnsurePixelSprite();
        EnsureBarRoot();
        backgroundRenderer = EnsureRenderer(backgroundRenderer, GeneratedBackgroundName, backgroundColor * 0.9f, -0.02f);
        fillRenderer = EnsureRenderer(fillRenderer, GeneratedFillName, fillGradient.Evaluate(1f), -0.01f);

        ConfigureRenderer(backgroundRenderer, backgroundColor);
        ConfigureRenderer(fillRenderer, fillGradient.Evaluate(currentNormalized));
        ApplyBarSize();
    }

    private void EnsurePixelSprite()
    {
        if (pixelSprite != null) return;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "WorldHealthBarPixel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private void EnsureBarRoot()
    {
        if (!IsValidBarRoot(barRoot))
        {
            barRoot = FindExistingBarRoot();
        }

        if (barRoot == null)
        {
            barRoot = new GameObject(GeneratedRootName).transform;
        }

        barRoot.name = GeneratedRootName;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = barOffset;
        barRoot.localRotation = Quaternion.identity;
        barRoot.localScale = Vector3.one;
    }

    private Transform FindExistingBarRoot()
    {
        Transform existing = transform.Find(GeneratedRootName);
        if (IsValidBarRoot(existing))
        {
            return existing;
        }

        foreach (var legacyName in LegacyRootNames)
        {
            existing = transform.Find(legacyName);
            if (IsLikelyLegacyBarRoot(existing))
            {
                return existing;
            }
        }

        return null;
    }

    private bool IsValidBarRoot(Transform candidate)
    {
        if (candidate == null) return false;
        if (candidate == transform) return false;
        if (candidate.parent != transform) return false;
        return true;
    }

    private bool IsLikelyLegacyBarRoot(Transform candidate)
    {
        if (candidate == null) return false;
        if (candidate == transform) return false;

        Transform background = FindChildByNames(candidate, LegacyBackgroundNames);
        Transform fill = FindChildByNames(candidate, LegacyFillNames);

        if (background == null || fill == null) return false;

        var bgRenderer = background.GetComponent<SpriteRenderer>();
        var fillRenderer = fill.GetComponent<SpriteRenderer>();
        if (bgRenderer == null || fillRenderer == null) return false;

        EnsurePixelSprite();

        bool UsesOurSprite(SpriteRenderer r) =>
            r != null &&
            r.sprite != null &&
            (r.sprite == pixelSprite || r.sprite.name == pixelSprite.name);

        if (!UsesOurSprite(bgRenderer) && !UsesOurSprite(fillRenderer)) return false;

        return true;
    }

    private Transform FindChildByNames(Transform parent, string[] names)
    {
        if (parent == null || names == null) return null;

        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name)) continue;
            var child = parent.Find(name);
            if (child != null) return child;
        }

        return null;
    }

    private bool CanModifyHierarchy()
    {
        return gameObject.scene.IsValid();
    }

    private SpriteRenderer EnsureRenderer(SpriteRenderer renderer, string childName, Color color, float zOffset)
    {
        if (barRoot == null) return null;

        if (renderer == null)
        {
            string[] lookupNames = childName == GeneratedBackgroundName ? LegacyBackgroundNames : LegacyFillNames;
            var existing = FindChildByNames(barRoot, lookupNames);
            if (existing != null)
            {
                renderer = existing.GetComponent<SpriteRenderer>();
            }
        }

        if (renderer == null)
        {
            renderer = CreateRenderer(childName, color, zOffset);
        }
        else if (renderer.transform.name != childName)
        {
            renderer.transform.name = childName;
        }

        return renderer;
    }

    private SpriteRenderer CreateRenderer(string name, Color color, float zOffset)
    {
        var t = new GameObject(name).transform;
        t.SetParent(barRoot, false);
        t.localPosition = new Vector3(0f, 0f, zOffset);
        var renderer = t.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = pixelSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.sortingLayerName = sortingLayerName;
        renderer.drawMode = SpriteDrawMode.Simple;
        
        // In URP, we need to ensure the default sprite material is used
        // Unity should assign it automatically, but we explicitly ensure it's not null
        #if UNITY_EDITOR
        if (renderer.sharedMaterial == null)
        {
            // Force Unity to assign the default sprite material
            var defaultSpriteMaterial = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null
                ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial
                : null;
                
            // If still null, let Unity handle it by accessing the material property
            // This triggers Unity's internal default material assignment
            if (defaultSpriteMaterial == null)
            {
                _ = renderer.material; // Access material to trigger default assignment
            }
        }
        #endif
        
        return renderer;
    }

    private void OnDrawGizmosSelected()
    {
        if (health != null || transform.parent != null)
        {
            Gizmos.color = Color.green;
            Vector3 center = transform.position + barOffset;
            Gizmos.DrawWireCube(center, new Vector3(barSize.x, barSize.y, 0.05f));
        }
    }


    private void ConfigureRenderer(SpriteRenderer renderer, Color color)
    {
        if (renderer == null) return;

        renderer.sprite = pixelSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.sortingLayerName = sortingLayerName;
    }

    private void ApplyBarSize()
    {
        if (backgroundRenderer != null)
        {
            backgroundRenderer.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
        }

        UpdateFillTransform(currentNormalized);
    }

    private void OnHealthChanged(float current, float max)
    {
        float normalized = max <= 0f ? 0f : current / max;
        normalized = Mathf.Clamp01(normalized);
        UpdateFillTransform(normalized);
        ApplyVisibility();
    }

    private void OnHealthDied(Health _)
    {
        UpdateFillTransform(0f);
        ApplyVisibility();
    }

    private void UpdateFillTransform(float normalized)
    {
        currentNormalized = normalized;

        if (fillRenderer == null) return;

        fillRenderer.color = fillGradient.Evaluate(normalized);

        float width = barSize.x * Mathf.Clamp01(normalized);
        width = Mathf.Max(0f, width);
        fillRenderer.transform.localScale = new Vector3(width, barSize.y, 1f);

        float xOffset = -0.5f * (barSize.x - width);
        fillRenderer.transform.localPosition = new Vector3(xOffset, 0f, fillRenderer.transform.localPosition.z);
    }

    private void UpdateBarImmediately()
    {
        if (health == null)
        {
            UpdateFillTransform(0f);
            return;
        }

        UpdateFillTransform(Mathf.Clamp01(health.CurrentHealth / health.MaxHealth));
    }

    private void ApplyVisibility()
    {
        bool visible = true;
        if (hideWhenFull && currentNormalized >= 0.999f)
            visible = false;

        if (backgroundRenderer != null)
            backgroundRenderer.enabled = visible;

        if (fillRenderer != null)
            fillRenderer.enabled = visible;
    }
}
