using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AimPreview : MonoBehaviour
{
    [SerializeField] private float maxDistance = 4f; // comprimento da mira

    private LineRenderer lr;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();

        // a linha só precisa de 2 pontos: origem e fim
        lr.positionCount = 2;

        // desenhar em world space (sim, queremos isso)
        lr.useWorldSpace = true;

        // deixar a linha sempre "virada pra câmera" no 2D
        lr.alignment = LineAlignment.View;
    }

    private void Update()
    {
        // ponto inicial = posição do jogador / arma
        Vector3 startPos = transform.position;
        startPos.z = 0f;

        // pega posição do mouse em coordenadas de mundo
        Vector3 mouseScreenPos = Input.mousePosition;
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPos.z = 0f;

        // direção normalizada
        Vector3 dir = (mouseWorldPos - startPos).normalized;

        // ponto final da linha = início + direção * alcance
        Vector3 endPos = startPos + dir * maxDistance;

        // aplica nos pontos do LineRenderer
        lr.SetPosition(0, startPos);
        lr.SetPosition(1, endPos);
    }
}
