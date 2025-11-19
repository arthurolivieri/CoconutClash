using TMPro;
using UnityEngine;

/// <summary>
/// Handles the lifetime and animation of a floating damage number.
/// Use DamageNumberPopup.Spawn(...) to show one.
/// </summary>
public class DamageNumberPopup : MonoBehaviour
{
    [Header("Popup Settings")]
    [SerializeField] private float lifetime = 0.9f;
    [SerializeField] private float riseSpeed = 1.5f;
    [SerializeField] private float horizontalDrift = 0.35f;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float fontSize = 2.5f;

    private TMP_Text text;
    private float timer;
    private Color initialColor;
    private Vector3 velocity;

    private static readonly Vector3 defaultSpawnOffset = new Vector3(0f, 0.4f, 0f);

    public static DamageNumberPopup Spawn(float amount, Vector3 worldPosition, Color color, int sortingOrder = 400)
    {
        GameObject go = new GameObject("DamageNumberPopup");
        go.transform.position = worldPosition + defaultSpawnOffset;

        var popup = go.AddComponent<DamageNumberPopup>();
        popup.Initialize(amount, color, sortingOrder);
        return popup;
    }

    private void Initialize(float amount, Color color, int sortingOrder)
    {
        text = gameObject.AddComponent<TextMeshPro>();
        text.text = Mathf.RoundToInt(amount).ToString();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        if (text.TryGetComponent<MeshRenderer>(out var renderer))
        {
            renderer.sortingOrder = sortingOrder;
        }

        initialColor = color;

        // Give a subtle random drift so numbers don't stack perfectly.
        float dir = Random.value < 0.5f ? -1f : 1f;
        velocity = new Vector3(horizontalDrift * dir, riseSpeed, 0f);
    }

    private void Update()
    {
        if (text == null)
        {
            Destroy(gameObject);
            return;
        }

        timer += Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        velocity = Vector3.Lerp(velocity, new Vector3(0f, riseSpeed * 0.5f, 0f), Time.deltaTime * 3f);

        if (timer >= lifetime)
        {
            float fadeT = Mathf.Clamp01((timer - lifetime) / Mathf.Max(0.001f, fadeDuration));
            Color c = initialColor;
            c.a = Mathf.Lerp(initialColor.a, 0f, fadeT);
            text.color = c;

            if (timer >= lifetime + fadeDuration)
            {
                Destroy(gameObject);
            }
        }
    }
}
