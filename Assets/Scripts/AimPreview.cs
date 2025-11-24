using System.Collections.Generic;
using UnityEngine;

public class AimPreview : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Shooter shooter; // opcional; se nulo, tentamos achar no pai

    [Header("Dotted Trajectory")]
    [SerializeField] private int dotCount = 16;
    [SerializeField] private float timeStep = 0.06f; // delta t entre pontos
    [SerializeField] private float dotSizeStart = 0.18f; // em unidades do mundo
    [SerializeField] private float dotSizeEnd = 0.08f;   // em unidades do mundo
    [SerializeField] private Color dotColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private bool stopAtCollision = true;
    [SerializeField] private LayerMask collisionMask = ~0; // tudo por padrão
    [SerializeField] private float collisionRadius = 0.05f;
    [SerializeField] private float startSkipTime = 0.03f; // evita colisão com o próprio jogador
    [SerializeField] private bool ignoreOwnerColliders = true;

    [Header("Pixel Art Style")]
    [SerializeField] private bool pixelArtFilter = true;
    [SerializeField] private int outlineWidthPx = 1;
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField] private bool enforceWhite = true;
    [SerializeField, Range(0f, 1f)] private float minOpacity = 0.9f;

    [Header("Mobile Settings")]
    [SerializeField] private bool requireActiveAimOnMobile = true; // Only show preview when actively dragging on mobile

    [Header("Fallback (se Shooter não estiver setado)")]
    [SerializeField] private float fallbackPreviewSpeed = 12f;
    [SerializeField] private float fallbackGravity = 9.81f;

    private const int DotSpritePixels = 16;

    private readonly List<SpriteRenderer> dotRenderers = new List<SpriteRenderer>();
    private Sprite dotSprite;
    private Camera mainCam;
    private LineRenderer existingLine; // desativado se existir
    private readonly System.Collections.Generic.HashSet<Collider2D> ownerColliders = new System.Collections.Generic.HashSet<Collider2D>();
    private bool hideUntilNextAim = false;
    private bool lastCanAim = false;

    private void Awake()
    {
        mainCam = Camera.main;
        if (shooter == null) shooter = GetComponentInParent<Shooter>();

        existingLine = GetComponent<LineRenderer>();
        if (existingLine != null) existingLine.enabled = false; // ocultar linha reta antiga

        EnsureDotSprite();
        EnsureDots(dotCount);

        if (ignoreOwnerColliders)
        {
            CollectOwnerColliders();
        }
    }

    private void OnEnable()
    {
        if (shooter != null)
        {
            shooter.ManualShotFired += OnManualShotFired;
            shooter.OnAimCancelled += OnAimCancelled;
        }
    }

    private void OnDisable()
    {
        if (shooter != null)
        {
            shooter.ManualShotFired -= OnManualShotFired;
            shooter.OnAimCancelled -= OnAimCancelled;
        }
    }

    private void OnManualShotFired()
    {
        // Oculta o preview até que a próxima janela de mira comece (ex: próximo turno)
        hideUntilNextAim = true;
        HideAllDots();
    }

    private void OnAimCancelled()
    {
        // Hide the preview when aim is cancelled
        HideAllDots();
    }

    private void Update()
    {
        if (mainCam == null) mainCam = Camera.main;

        bool canAimNow = shooter == null ? true : (shooter.IsManualMode() && shooter.IsManualAimAllowed());

        // Detecta início de uma nova janela de mira (ex: quando turno muda de inimigo -> player)
        if (canAimNow && !lastCanAim)
        {
            hideUntilNextAim = false;
        }
        lastCanAim = canAimNow;

        if (!canAimNow || hideUntilNextAim)
        {
            HideAllDots();
            return;
        }

        // Check if we're using mobile controls and if we should only show preview while actively aiming
        bool isMobile = shooter != null && shooter.IsMobileControlsEnabled();
        if (isMobile && requireActiveAimOnMobile && !shooter.IsAiming)
        {
            HideAllDots();
            return;
        }

        Vector3 startPos = shooter ? shooter.GetMuzzleOrPosition() : transform.position;
        startPos.z = 0f;

        // Get the target position - use shooter's aim position on mobile, mouse position otherwise
        Vector3 targetWorldPos;
        if (isMobile && shooter.IsAiming)
        {
            targetWorldPos = shooter.AimTargetPosition;
        }
        else if (MobileInputManager.Instance != null && MobileInputManager.Instance.IsMobileInputActive)
        {
            // Use MobileInputManager's world position if available
            targetWorldPos = MobileInputManager.Instance.InputWorldPosition;
        }
        else
        {
            // Fallback to mouse position
            targetWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        }
        targetWorldPos.z = 0f;

        Vector2 initialVelocity;
        float gravity;

        if (shooter != null)
        {
            initialVelocity = shooter.PredictInitialVelocity(startPos, targetWorldPos);
            gravity = Mathf.Max(0.0001f, shooter.GetGravityStrength());
        }
        else
        {
            Vector2 dir = ((Vector2)targetWorldPos - (Vector2)startPos).normalized;
            initialVelocity = dir * fallbackPreviewSpeed;
            gravity = Mathf.Max(0.0001f, fallbackGravity);
        }

        // Integração analítica: p(t) = p0 + v0 * t + 0.5 * a * t^2, com a = (0, -g)
        Vector2 a = new Vector2(0f, -gravity);

        // começa levemente à frente do cano para não colidir com o próprio jogador
        float tPrev = Mathf.Max(0f, startSkipTime);
        Vector2 prev = (Vector2)startPos + initialVelocity * tPrev + 0.5f * a * (tPrev * tPrev);
        int visible = 0;

        for (int i = 0; i < dotRenderers.Count; i++)
        {
            float t = tPrev + (i + 1) * timeStep;
            Vector2 pos = (Vector2)startPos + initialVelocity * t + 0.5f * a * (t * t);

            bool place = true;
            if (stopAtCollision)
            {
                if (TryHitSegment(prev, pos, out RaycastHit2D hit))
                {
                    pos = hit.point;
                    place = true;
                    // posiciona este ponto no impacto e para
                    SetDot(i, pos, i);
                    visible = i + 1;
                    // desabilita restantes
                    for (int j = visible; j < dotRenderers.Count; j++) dotRenderers[j].enabled = false;
                    return;
                }
            }

            if (place)
            {
                SetDot(i, pos, i);
                visible = i + 1;
            }

            prev = pos;
        }

        // se não colidiu, apenas garantir que todos até "visible" estão ativos
        for (int i = 0; i < dotRenderers.Count; i++)
        {
            dotRenderers[i].enabled = i < visible;
        }
    }

    private bool TryHitSegment(Vector2 from, Vector2 to, out RaycastHit2D bestHit)
    {
        bestHit = default;
        float segDist = Vector2.Distance(from, to);
        if (segDist <= 0.0001f) return false;

        Vector2 dir = (to - from).normalized;
        float bestDist = float.PositiveInfinity;

        if (collisionRadius > 0.001f)
        {
            var hits = Physics2D.CircleCastAll(from, collisionRadius, dir, segDist, collisionMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                if (ignoreOwnerColliders && ownerColliders.Contains(h.collider)) continue;
                float d = Vector2.Distance(from, h.point);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestHit = h;
                }
            }
        }
        else
        {
            var hits = Physics2D.LinecastAll(from, to, collisionMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                if (ignoreOwnerColliders && ownerColliders.Contains(h.collider)) continue;
                float d = Vector2.Distance(from, h.point);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestHit = h;
                }
            }
        }

        return bestDist < float.PositiveInfinity;
    }

    private void CollectOwnerColliders()
    {
        ownerColliders.Clear();
        var root = shooter ? shooter.transform.root : transform.root;
        if (root == null) root = transform;
        var cols = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            ownerColliders.Add(cols[i]);
        }
    }

    private void HideAllDots()
    {
        for (int i = 0; i < dotRenderers.Count; i++)
        {
            if (dotRenderers[i] != null) dotRenderers[i].enabled = false;
        }
    }

    private void SetDot(int index, Vector2 worldPos, int orderIndex)
    {
        var sr = dotRenderers[index];
        if (sr == null)
        {
            // Recria caso algo externo tenha destruído o renderer
            sr = RecreateDotRenderer(index);
        }

        if (sr.sprite == null)
        {
            EnsureDotSprite();
            sr.sprite = dotSprite;
        }

        sr.enabled = true;
        sr.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

        float t = (dotRenderers.Count <= 1) ? 1f : (index / Mathf.Max(1f, (dotRenderers.Count - 1)));
        float size = Mathf.Lerp(dotSizeStart, dotSizeEnd, t);
        float baseSize = sr.sprite.bounds.size.x; // tamanho em unidades do mundo para escala 1x
        float scale = (baseSize <= 0.00001f) ? 1f : size / baseSize;
        sr.transform.localScale = new Vector3(scale, scale, 1f);

        // Cor fixa (pode-se suavemente diminuir o alpha nos últimos pontos se quiser)
        Color desired = enforceWhite ? new Color(1f, 1f, 1f, Mathf.Max(minOpacity, dotColor.a))
                                     : new Color(dotColor.r, dotColor.g, dotColor.b, Mathf.Max(minOpacity, dotColor.a));
        sr.color = desired;

        // Garantir ordem de renderização para ficar acima do cenário
        sr.sortingOrder = 100 + orderIndex;
        sr.sortingLayerName = "Player";
    }

    private void EnsureDots(int count)
    {
        // Cria/ajusta pool de pontos
        EnsureDotSprite();
        while (dotRenderers.Count < count)
        {
            dotRenderers.Add(CreateDotRenderer(dotRenderers.Count));
        }

        for (int i = count; i < dotRenderers.Count; i++)
        {
            dotRenderers[i].enabled = false;
        }

        for (int i = 0; i < dotRenderers.Count; i++)
        {
            if (dotRenderers[i] != null && dotRenderers[i].sprite == null)
            {
                dotRenderers[i].sprite = dotSprite;
            }
        }
    }

    private SpriteRenderer CreateDotRenderer(int index)
    {
        var go = new GameObject($"AimDot_{index}");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = dotSprite;
        sr.color = dotColor;
        sr.enabled = false;
        sr.sortingOrder = 100 + index;
        sr.sortingLayerName = "Player";
        return sr;
    }

    private SpriteRenderer RecreateDotRenderer(int index)
    {
        var sr = CreateDotRenderer(index);
        if (index < dotRenderers.Count)
        {
            dotRenderers[index] = sr;
        }
        return sr;
    }

    private void EnsureDotSprite()
    {
        if (dotSprite != null) return;
        dotSprite = CreateCircleSprite(DotSpritePixels, outlineWidthPx, pixelArtFilter, outlineColor);
    }

    private static Sprite CreateCircleSprite(int diameterPx, int outlineWidthPx, bool pixelArtFilter, Color outlineColor)
    {
        int d = Mathf.Max(2, diameterPx);
        int r = d / 2;
        int ow = Mathf.Clamp(outlineWidthPx, 0, Mathf.Max(1, r));

        var tex = new Texture2D(d, d, TextureFormat.ARGB32, false);
        tex.filterMode = pixelArtFilter ? FilterMode.Point : FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32 transparent = new Color(1f, 1f, 1f, 0f);
        Color32 fill = Color.white;
        Color32 border = outlineColor;

        for (int y = 0; y < d; y++)
        {
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Outline ring
                if (dist > r - ow && dist <= r)
                {
                    tex.SetPixel(x, y, border);
                }
                else if (dist <= r - 0.5f)
                {
                    tex.SetPixel(x, y, fill);
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply(false, true);

        // Pixels por unidade = 100 → sprite.bounds ≈ d/100
        return Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnValidate()
    {
        dotCount = Mathf.Clamp(dotCount, 3, 64);
        timeStep = Mathf.Max(0.01f, timeStep);
        dotSizeStart = Mathf.Max(0.001f, dotSizeStart);
        dotSizeEnd = Mathf.Max(0.001f, dotSizeEnd);

        if (dotRenderers != null)
        {
            EnsureDotSprite();
            EnsureDots(dotCount);
        }
    }
}
