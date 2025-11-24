using System;
using UnityEngine;

/// <summary>
/// Handles mobile touch input for drag-to-aim and tap interactions.
/// This acts as a central input hub that other scripts can query for touch state.
/// Automatically bootstraps itself into any scene that doesn't have one.
/// </summary>
public class MobileInputManager : MonoBehaviour
{
    public static MobileInputManager Instance { get; private set; }

    /// <summary>
    /// Automatically creates a MobileInputManager if one doesn't exist.
    /// This runs before any scene loads.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("MobileInputManager");
            go.AddComponent<MobileInputManager>();
            DontDestroyOnLoad(go);
        }
    }

    [Header("Settings")]
    [SerializeField] private bool forceMobileInput = false;
    [SerializeField] private float minDragDistance = 10f; // In pixels, to distinguish tap from drag
    [SerializeField] private float tapMaxDuration = 0.3f; // Max time for a touch to count as tap

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Current touch state
    private bool isTouching = false;
    private bool isDragging = false;
    private Vector2 touchStartPosition;
    private Vector2 currentTouchPosition;
    private float touchStartTime;
    private int activeTouchId = -1;

    // Events for other scripts to subscribe to
    public event Action<Vector2> OnTouchBegan;
    public event Action<Vector2> OnTouchMoved;
    public event Action<Vector2> OnTouchEnded;
    public event Action<Vector2> OnTap;
    public event Action<Vector2, Vector2> OnDragEnded; // startPos, endPos

    // Properties for external access
    public bool IsTouching => isTouching;
    public bool IsDragging => isDragging;
    public Vector2 TouchStartPosition => touchStartPosition;
    public Vector2 CurrentTouchPosition => currentTouchPosition;
    public bool IsMobileInputActive => forceMobileInput || Application.isMobilePlatform;

    /// <summary>
    /// Returns the current input position (touch or mouse) in screen coordinates.
    /// </summary>
    public Vector2 InputPosition
    {
        get
        {
            if (IsMobileInputActive && isTouching)
            {
                return currentTouchPosition;
            }
            return Input.mousePosition;
        }
    }

    /// <summary>
    /// Returns the current input position in world coordinates (z = 0).
    /// </summary>
    public Vector3 InputWorldPosition
    {
        get
        {
            Camera cam = Camera.main;
            if (cam == null) return Vector3.zero;

            Vector3 screenPos = InputPosition;
            screenPos.z = -cam.transform.position.z;
            Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);
            worldPos.z = 0f;
            return worldPos;
        }
    }

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure this persists across scene loads
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (IsMobileInputActive)
        {
            HandleTouchInput();
        }
        else
        {
            HandleMouseAsTouchInput();
        }
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 0)
        {
            if (isTouching)
            {
                EndTouch(currentTouchPosition);
            }
            return;
        }

        // Find our tracked touch or get a new one
        Touch? activeTouch = null;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (activeTouchId == -1 || t.fingerId == activeTouchId)
            {
                activeTouch = t;
                break;
            }
        }

        if (!activeTouch.HasValue)
        {
            if (isTouching)
            {
                EndTouch(currentTouchPosition);
            }
            return;
        }

        Touch touch = activeTouch.Value;

        switch (touch.phase)
        {
            case TouchPhase.Began:
                StartTouch(touch.fingerId, touch.position);
                break;

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                UpdateTouch(touch.position);
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                EndTouch(touch.position);
                break;
        }
    }

    /// <summary>
    /// Allows mouse input to simulate touch for editor testing.
    /// </summary>
    private void HandleMouseAsTouchInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartTouch(0, Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && isTouching)
        {
            UpdateTouch(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0) && isTouching)
        {
            EndTouch(Input.mousePosition);
        }
    }

    private void StartTouch(int fingerId, Vector2 position)
    {
        activeTouchId = fingerId;
        isTouching = true;
        isDragging = false;
        touchStartPosition = position;
        currentTouchPosition = position;
        touchStartTime = Time.time;

        OnTouchBegan?.Invoke(position);

        if (showDebugInfo)
        {
            Debug.Log($"[MobileInput] Touch started at {position}");
        }
    }

    private void UpdateTouch(Vector2 position)
    {
        currentTouchPosition = position;

        // Check if we've started dragging
        if (!isDragging)
        {
            float distance = Vector2.Distance(touchStartPosition, currentTouchPosition);
            if (distance >= minDragDistance)
            {
                isDragging = true;
                if (showDebugInfo)
                {
                    Debug.Log($"[MobileInput] Drag started");
                }
            }
        }

        OnTouchMoved?.Invoke(position);
    }

    private void EndTouch(Vector2 position)
    {
        currentTouchPosition = position;
        float touchDuration = Time.time - touchStartTime;
        float dragDistance = Vector2.Distance(touchStartPosition, position);

        if (showDebugInfo)
        {
            Debug.Log($"[MobileInput] Touch ended at {position}, duration: {touchDuration:F2}s, drag: {dragDistance:F1}px");
        }

        OnTouchEnded?.Invoke(position);

        // Determine if this was a tap or drag
        if (!isDragging && touchDuration <= tapMaxDuration)
        {
            OnTap?.Invoke(position);
            if (showDebugInfo)
            {
                Debug.Log($"[MobileInput] Tap detected");
            }
        }
        else if (isDragging)
        {
            OnDragEnded?.Invoke(touchStartPosition, position);
            if (showDebugInfo)
            {
                Debug.Log($"[MobileInput] Drag ended: {touchStartPosition} -> {position}");
            }
        }

        // Reset state
        isTouching = false;
        isDragging = false;
        activeTouchId = -1;
    }

    /// <summary>
    /// Checks if a UI element is being touched (to avoid gameplay input when tapping UI).
    /// </summary>
    public bool IsPointerOverUI()
    {
        if (IsMobileInputActive && Input.touchCount > 0)
        {
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }
        return UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>
    /// Gets the drag vector from start to current position in screen coordinates.
    /// </summary>
    public Vector2 GetDragVector()
    {
        if (!isTouching) return Vector2.zero;
        return currentTouchPosition - touchStartPosition;
    }

    /// <summary>
    /// Gets the drag vector in world coordinates.
    /// </summary>
    public Vector2 GetDragVectorWorld()
    {
        if (!isTouching) return Vector2.zero;

        Camera cam = Camera.main;
        if (cam == null) return Vector2.zero;

        Vector3 startWorld = cam.ScreenToWorldPoint(new Vector3(touchStartPosition.x, touchStartPosition.y, -cam.transform.position.z));
        Vector3 currentWorld = cam.ScreenToWorldPoint(new Vector3(currentTouchPosition.x, currentTouchPosition.y, -cam.transform.position.z));

        return (Vector2)(currentWorld - startWorld);
    }

    /// <summary>
    /// Gets the normalized drag distance (0 to 1 based on maxDistance).
    /// </summary>
    public float GetNormalizedDragDistance(float maxDistance)
    {
        if (maxDistance <= 0) return 0f;
        return Mathf.Clamp01(GetDragVectorWorld().magnitude / maxDistance);
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Mobile Input Active: {IsMobileInputActive}");
        GUILayout.Label($"Is Touching: {isTouching}");
        GUILayout.Label($"Is Dragging: {isDragging}");
        GUILayout.Label($"Touch Position: {currentTouchPosition}");
        GUILayout.Label($"Input World Pos: {InputWorldPosition}");
        GUILayout.Label($"Over UI: {IsPointerOverUI()}");
        GUILayout.EndArea();
    }
}
