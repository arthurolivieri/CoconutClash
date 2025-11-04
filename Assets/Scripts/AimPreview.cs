using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AimPreview : MonoBehaviour
{
    [SerializeField] private float maxDistance = 4f; // tamanho máximo da linha

    private LineRenderer lr;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();

        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;

        // largura inicial básica (se você não setou no Inspector ainda)
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
    }

    private void Update()
    {
        // ponto inicial (jogador / arma)
        Vector3 startPos = transform.position;
        startPos.z = 0f;

        // posição do mouse no mundo
        Vector3 mouseScreenPos = Input.mousePosition;
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPos.z = 0f;

        // direção normalizada
        Vector3 dir = (mouseWorldPos - startPos).normalized;

        // distância real até o mouse
        float distToMouse = Vector3.Distance(startPos, mouseWorldPos);

        // quanto da linha vamos mostrar:
        // - se o mouse está mais perto do que maxDistance → usa distToMouse
        // - se está mais longe → trava em maxDistance
        float visibleLength = Mathf.Min(distToMouse, maxDistance);

        // ponto final visível da mira
        Vector3 endPos = startPos + dir * visibleLength;

        // aplica no LineRenderer
        lr.SetPosition(0, startPos);
        lr.SetPosition(1, endPos);
    }
}
